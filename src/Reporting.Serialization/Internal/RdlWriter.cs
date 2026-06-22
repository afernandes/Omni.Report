using System.Globalization;
using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

/// <summary>
/// Builds the RDL (<c>&lt;Report&gt;</c>) XML tree for a <see cref="ReportDefinition"/> — the structural
/// inverse of <see cref="RdlImporter"/>. Emits the official SSRS namespace
/// (<c>…/sqlserver/reporting/2016/01/reportdefinition</c>), the SAME one the importer reads, so a report can
/// round-trip <c>.rdl → import → edit → export → .rdl</c> and interoperate with SSRS / Report Builder.
/// <para>PR1: page skeleton. PR2: simple report items (Textbox/Label, Line, Rectangle + nested children,
/// Image) with their <c>&lt;Style&gt;</c>, mapping the static bands to <c>Body</c>/<c>PageHeader</c>/
/// <c>PageFooter</c>. Data regions (Tablix/Chart/Gauge from Detail + Groups), DataSets and parameters arrive
/// in later phases — they are recorded in the <c>warnings</c> list meanwhile (never a silent drop).</para>
/// </summary>
internal static class RdlWriter
{
    internal static readonly XNamespace Rdl =
        "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";

    public static XDocument Write(ReportDefinition def, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(warnings);
        var page = def.PageSetup;
        var report = new XElement(Rdl + "Report");

        // The Body is RDL's flat design canvas. The importer turns free Body items into a ReportHeader band,
        // so the inverse writes ReportHeader (and any static ReportFooter) items back into Body.ReportItems.
        var bodyItems = new XElement(Rdl + "ReportItems");
        if (def.ReportHeader is { } rh)
        {
            WriteReportItems(bodyItems, rh.Elements, warnings);
        }
        if (def.ReportFooter is { Elements.Count: > 0 } rf)
        {
            WriteReportItems(bodyItems, rf.Elements, warnings);
        }
        // A data-bound Detail (or any Groups) is a data region → <Tablix>, a later phase. A purely static
        // Detail (no dataset, no groups) is written as Body items so static reports round-trip now.
        if (def.Groups.Count > 0)
        {
            warnings.Add("Grupos (Groups) → <Tablix> hierarchy: exportação é uma fase posterior (PR4).");
        }
        if (def.Detail.Elements.Count > 0)
        {
            if (def.Detail.DataSetName is null && def.Groups.Count == 0)
            {
                WriteReportItems(bodyItems, def.Detail.Elements, warnings);
            }
            else
            {
                warnings.Add("DetailBand vinculada a dados → <Tablix>: exportação é uma fase posterior (PR4).");
            }
        }

        // The importer maps <Body><Height> onto the ReportHeader band's height, so the inverse writes that
        // band's height back (preserving the round-trip). Falls back to the printable area / a default.
        var bodyHeight = def.ReportHeader is { } rhBand && rhBand.Height.Mils > 0 ? rhBand.Height
            : page.ContentHeight.ToMm() > 0 ? page.ContentHeight : Unit.FromMm(100);
        report.Add(new XElement(Rdl + "Body",
            new XElement(Rdl + "Height", Size(bodyHeight)),
            bodyItems));

        report.Add(new XElement(Rdl + "Width", Size(page.ContentWidth)));

        var pageEl = new XElement(Rdl + "Page",
            WriteBandSection(def.PageHeader, "PageHeader", warnings),
            new XElement(Rdl + "PageHeight", Size(page.PageHeight)),
            new XElement(Rdl + "PageWidth", Size(page.PageWidth)),
            new XElement(Rdl + "LeftMargin", Size(page.Margins.Left)),
            new XElement(Rdl + "RightMargin", Size(page.Margins.Right)),
            new XElement(Rdl + "TopMargin", Size(page.Margins.Top)),
            new XElement(Rdl + "BottomMargin", Size(page.Margins.Bottom)),
            WriteBandSection(def.PageFooter, "PageFooter", warnings));
        if (page.Columns > 1)
        {
            pageEl.Add(new XElement(Rdl + "Columns", page.Columns.ToString(CultureInfo.InvariantCulture)));
            pageEl.Add(new XElement(Rdl + "ColumnSpacing", Size(page.ColumnSpacing)));
        }
        report.Add(pageEl);

        AddIfPresent(report, def, "Language", "Language");
        AddIfPresent(report, def, "Description", "Description");
        AddIfPresent(report, def, "Author", "Author");

        return new XDocument(new XDeclaration("1.0", "utf-8", null), report);
    }

    // <PageHeader>/<PageFooter> carry Height + PrintOnFirstPage/PrintOnLastPage + ReportItems.
    private static XElement? WriteBandSection(ReportBand? band, string element, List<string> warnings)
    {
        if (band is null || band.Elements.Count == 0)
        {
            return null;
        }
        var items = new XElement(Rdl + "ReportItems");
        WriteReportItems(items, band.Elements, warnings);
        var section = new XElement(Rdl + element,
            new XElement(Rdl + "Height", Size(band.Height)),
            new XElement(Rdl + "PrintOnFirstPage", Bool(band.PrintOnFirstPage)),
            new XElement(Rdl + "PrintOnLastPage", Bool(band.PrintOnLastPage)),
            items);
        return section;
    }

    private static void WriteReportItems(XElement parent, IEnumerable<ReportElement> elements, List<string> warnings)
    {
        foreach (var el in elements)
        {
            var item = WriteItem(el, warnings);
            if (item is not null)
            {
                parent.Add(item);
            }
        }
    }

    // One <ReportItem> for a model element. Returns null (with a warning) for kinds not yet supported.
    private static XElement? WriteItem(ReportElement el, List<string> warnings)
    {
        XElement? item = el switch
        {
            LabelElement lbl => Textbox(TextRunValue(lbl.Text ?? string.Empty)),
            TextBoxElement tb => WriteTextBox(tb),
            LineElement line => WriteLine(line),
            RectangleElement rect => WriteRectangle(rect, warnings),
            ImageElement img => WriteImage(img, warnings),
            _ => Unsupported(el, warnings),
        };
        if (item is null)
        {
            return null;
        }
        WriteCommon(item, el);
        return item;
    }

    private static XElement? Unsupported(ReportElement el, List<string> warnings)
    {
        warnings.Add($"{el.GetType().Name} '{el.Name ?? el.Id}': exportação RDL é uma fase posterior.");
        return null;
    }

    private static XElement Textbox(params object[] content)
        => new(Rdl + "Textbox", new XElement(Rdl + "Paragraphs",
            new XElement(Rdl + "Paragraph", new XElement(Rdl + "TextRuns", content))));

    private static XElement TextRunValue(string value)
        => new(Rdl + "TextRun", new XElement(Rdl + "Value", value));

    private static XElement WriteTextBox(TextBoxElement tb)
    {
        var runs = new XElement(Rdl + "TextRuns");
        if (tb.TextRuns.Count > 0)
        {
            foreach (var run in tb.TextRuns)
            {
                var rdlRun = new XElement(Rdl + "TextRun", new XElement(Rdl + "Value", ValueOf(run.Value)));
                var runStyle = run.Style is null ? null : StyleElement(run.Style);
                if (runStyle is not null)
                {
                    rdlRun.Add(runStyle);
                }
                runs.Add(rdlRun);
            }
        }
        else
        {
            runs.Add(new XElement(Rdl + "TextRun", new XElement(Rdl + "Value", ValueOf(tb.Expression))));
        }
        var box = new XElement(Rdl + "Textbox",
            new XElement(Rdl + "Paragraphs", new XElement(Rdl + "Paragraph", runs)));
        if (tb.CanGrow)
        {
            box.Add(new XElement(Rdl + "CanGrow", "true"));
        }
        if (tb.CanShrink)
        {
            box.Add(new XElement(Rdl + "CanShrink", "true"));
        }
        return box;
    }

    private static XElement WriteLine(LineElement line)
    {
        var pen = line.Pen;
        // The importer maps a Line's <Style><Border> (first visible side) to its Pen; write it back so a
        // non-default pen survives the round-trip.
        return new XElement(Rdl + "Line",
            new XElement(Rdl + "Style", new XElement(Rdl + "Border",
                new XElement(Rdl + "Color", pen.Color.ToHex()),
                new XElement(Rdl + "Style", BorderStyleName(pen.Style)),
                new XElement(Rdl + "Width", Size(pen.Thickness)))));
    }

    private static XElement WriteRectangle(RectangleElement rect, List<string> warnings)
    {
        var el = new XElement(Rdl + "Rectangle");
        if (rect.Children.Count > 0)
        {
            var items = new XElement(Rdl + "ReportItems");
            WriteReportItems(items, rect.Children, warnings); // children carry relative bounds, as RDL expects
            el.Add(items);
        }
        return el;
    }

    private static XElement WriteImage(ImageElement img, List<string> warnings)
    {
        var el = new XElement(Rdl + "Image");
        switch (img.Source)
        {
            case ImageSourceKind.Path:
                el.Add(new XElement(Rdl + "Source", "External"), new XElement(Rdl + "Value", img.Path ?? string.Empty));
                break;
            case ImageSourceKind.Expression:
                el.Add(new XElement(Rdl + "Source", "External"),
                    new XElement(Rdl + "Value", RdlExpressionReverse.ToRdl(img.Expression)));
                break;
            default: // Inline/Embedded bytes — <EmbeddedImages> wiring is a later phase.
                el.Add(new XElement(Rdl + "Source", "External"), new XElement(Rdl + "Value", string.Empty));
                warnings.Add($"Image '{img.Name ?? img.Id}': bytes embutidos → <EmbeddedImages> é uma fase posterior.");
                break;
        }
        el.Add(new XElement(Rdl + "Sizing", img.Sizing switch
        {
            ImageSizing.Stretch => "Fit",
            ImageSizing.Fit => "FitProportional",
            ImageSizing.Native => "Clip",
            _ => "AutoSize",
        }));
        return el;
    }

    // The RDL <Value> for an OmniReport text/expression: a pure expression becomes "=…"; otherwise literal.
    private static string ValueOf(string? expr)
    {
        if (string.IsNullOrEmpty(expr))
        {
            return string.Empty;
        }
        return RdlExpressionReverse.ToRdl(expr);
    }

    // ── Common attributes: Name, bounds, style, visibility, bookmark, action ──────

    private static void WriteCommon(XElement item, ReportElement el)
    {
        if (!string.IsNullOrEmpty(el.Name))
        {
            item.SetAttributeValue("Name", el.Name);
        }
        var style = StyleElement(el.Style);
        // Style sub-properties driven by an expression (conditional formatting) — inverse of ReadStyleExpressions.
        WriteStyleExpressions(ref style, el.PropertyExpressions);
        if (style is not null)
        {
            item.Add(style);
        }
        // Position (RDL uses Top/Left). Lines and nested items keep their bounds; a Rectangle's children are
        // already relative in the model, matching RDL's relative nesting.
        item.Add(new XElement(Rdl + "Top", Size(el.Bounds.Y)),
                 new XElement(Rdl + "Left", Size(el.Bounds.X)),
                 new XElement(Rdl + "Height", Size(el.Bounds.Height)),
                 new XElement(Rdl + "Width", Size(el.Bounds.Width)));
        if (!el.Visible)
        {
            item.Add(new XElement(Rdl + "Visibility", new XElement(Rdl + "Hidden", "true")));
        }
        else if (!string.IsNullOrEmpty(el.VisibleExpression))
        {
            // OmniReport stores Visible; RDL stores Hidden (the inverse).
            var visExpr = RdlExpressionReverse.ToRdl(el.VisibleExpression)[1..]; // drop the leading '='
            item.Add(new XElement(Rdl + "Visibility", new XElement(Rdl + "Hidden", "=Not(" + visExpr + ")")));
        }
        if (!string.IsNullOrEmpty(el.Bookmark))
        {
            item.Add(new XElement(Rdl + "Bookmark", RdlExpressionReverse.ToRdl(el.Bookmark)));
        }
        if (!string.IsNullOrEmpty(el.DocumentMapLabel))
        {
            item.Add(new XElement(Rdl + "DocumentMapLabel", RdlExpressionReverse.ToRdl(el.DocumentMapLabel)));
        }
        var action = WriteAction(el.Action);
        if (action is not null)
        {
            item.Add(action);
        }
    }

    private static XElement? WriteAction(ElementAction? action)
    {
        if (action is null)
        {
            return null;
        }
        return action.Kind switch
        {
            ActionKind.Hyperlink when !string.IsNullOrEmpty(action.Hyperlink) =>
                new XElement(Rdl + "Action", new XElement(Rdl + "Hyperlink", RdlExpressionReverse.ToRdl(action.Hyperlink))),
            ActionKind.BookmarkLink when !string.IsNullOrEmpty(action.BookmarkId) =>
                new XElement(Rdl + "Action", new XElement(Rdl + "BookmarkLink", RdlExpressionReverse.ToRdl(action.BookmarkId))),
            // Drillthrough (with parameters) is a later phase.
            _ => null,
        };
    }

    // ── Style ─────────────────────────────────────────────────────────────────────

    private static XElement? StyleElement(Style style)
    {
        if (style == Style.Default)
        {
            return null;
        }
        var s = new XElement(Rdl + "Style");
        if (style.Font is { } font)
        {
            if (!string.IsNullOrEmpty(font.Family) && font.Family != Font.Default.Family)
            {
                s.Add(new XElement(Rdl + "FontFamily", font.Family));
            }
            s.Add(new XElement(Rdl + "FontSize", font.Size.ToString("0.###", CultureInfo.InvariantCulture) + "pt"));
            if ((font.Style & FontStyle.Bold) != 0)
            {
                s.Add(new XElement(Rdl + "FontWeight", "Bold"));
            }
            if ((font.Style & FontStyle.Italic) != 0)
            {
                s.Add(new XElement(Rdl + "FontStyle", "Italic"));
            }
            if ((font.Style & FontStyle.Underline) != 0)
            {
                s.Add(new XElement(Rdl + "TextDecoration", "Underline"));
            }
            else if ((font.Style & FontStyle.Strikeout) != 0)
            {
                s.Add(new XElement(Rdl + "TextDecoration", "LineThrough"));
            }
        }
        if (style.ForeColor is { } fore)
        {
            s.Add(new XElement(Rdl + "Color", fore.ToHex()));
        }
        if (style.BackColor is { } back)
        {
            s.Add(new XElement(Rdl + "BackgroundColor", back.ToHex()));
        }
        WriteBorder(s, style.Border);
        WritePadding(s, style.Padding);
        if (style.HorizontalAlignment != HorizontalAlignment.Left)
        {
            s.Add(new XElement(Rdl + "TextAlign", style.HorizontalAlignment.ToString()));
        }
        if (style.VerticalAlignment != VerticalAlignment.Top)
        {
            s.Add(new XElement(Rdl + "VerticalAlign", style.VerticalAlignment.ToString()));
        }
        if (!style.WordWrap)
        {
            s.Add(new XElement(Rdl + "WrapMode", "NoWrap"));
        }
        if (!string.IsNullOrEmpty(style.Format))
        {
            s.Add(new XElement(Rdl + "Format", style.Format));
        }
        if (style.BackgroundImage is { } bg)
        {
            s.Add(new XElement(Rdl + "BackgroundImage",
                new XElement(Rdl + "Source", "External"),
                new XElement(Rdl + "Value", bg.IsExpression ? RdlExpressionReverse.ToRdl(bg.Expression) : bg.Path ?? string.Empty)));
        }
        return s.HasElements ? s : null;
    }

    private static void WriteBorder(XElement style, Border? border)
    {
        if (border is null)
        {
            return;
        }
        // Emit a default <Border> from the top side (the common case is a uniform border). Per-side overrides
        // are a refinement; the importer reads a default <Border> applying to all sides.
        var side = border.Top;
        if (!side.IsVisible)
        {
            return;
        }
        style.Add(new XElement(Rdl + "Border",
            new XElement(Rdl + "Color", side.Color.ToHex()),
            new XElement(Rdl + "Style", BorderStyleName(side.Style)),
            new XElement(Rdl + "Width", Size(side.Thickness))));
    }

    private static string BorderStyleName(BorderLineStyle style) => style switch
    {
        BorderLineStyle.None => "None",
        BorderLineStyle.Dotted => "Dotted",
        BorderLineStyle.Dashed => "Dashed",
        BorderLineStyle.Double => "Double",
        _ => "Solid",
    };

    private static void WritePadding(XElement style, Thickness? padding)
    {
        if (padding is not { } p || (p.Left == Unit.Zero && p.Top == Unit.Zero && p.Right == Unit.Zero && p.Bottom == Unit.Zero))
        {
            return;
        }
        style.Add(new XElement(Rdl + "PaddingLeft", Size(p.Left)),
                  new XElement(Rdl + "PaddingTop", Size(p.Top)),
                  new XElement(Rdl + "PaddingRight", Size(p.Right)),
                  new XElement(Rdl + "PaddingBottom", Size(p.Bottom)));
    }

    // Inverse of ReadStyleExpressions: a PropertyExpression on a known Style path → a <Style> sub-element
    // whose value is the reversed (=…) expression (conditional formatting round-trip).
    private static void WriteStyleExpressions(ref XElement? style, Reporting.Common.EquatableDictionary<string, string> bindings)
    {
        if (bindings.Count == 0)
        {
            return;
        }
        foreach (var (path, rdlProp) in StyleExpressionPaths)
        {
            if (bindings.TryGetValue(path, out var expr) && !string.IsNullOrEmpty(expr))
            {
                style ??= new XElement(Rdl + "Style");
                // Replace any literal value already written for this property with the expression.
                style.Element(Rdl + rdlProp)?.Remove();
                style.Add(new XElement(Rdl + rdlProp, RdlExpressionReverse.ToRdl(expr)));
            }
        }
    }

    private static readonly (string Path, string Rdl)[] StyleExpressionPaths =
    {
        ("Style.ForeColor", "Color"),
        ("Style.BackColor", "BackgroundColor"),
        ("Style.Format", "Format"),
        ("Style.HorizontalAlignment", "TextAlign"),
        ("Style.VerticalAlignment", "VerticalAlign"),
        ("Style.Font.Family", "FontFamily"),
    };

    private static void AddIfPresent(XElement report, ReportDefinition def, string metaKey, string rdlElement)
    {
        if (def.Metadata.TryGetValue(metaKey, out var value) && !string.IsNullOrEmpty(value))
        {
            report.Add(new XElement(Rdl + rdlElement, value));
        }
    }

    /// <summary>An RDL size string (e.g. <c>"210mm"</c>) — RDL's <c>RdlSize</c> accepts mm/cm/in/pt/pc.
    /// Always invariant-formatted so the XML is culture-stable.</summary>
    internal static string Size(Unit u) =>
        u.ToMm().ToString("0.####", CultureInfo.InvariantCulture) + "mm";

    private static string Bool(bool b) => b ? "true" : "false";
}
