using System.Globalization;
using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Serialization.Internal;
using Reporting.Styling;

namespace Reporting.Serialization;

/// <summary>
/// Imports an SSRS <c>.rdl</c> (Report Definition Language, XML) into an OmniReport
/// <see cref="ReportDefinition"/> — the literal "RDL compatibility" path, enabling migration from
/// SSRS. The result is a normal definition: it renders through the engine, opens in the Designer, and
/// is equivalent to what code-first / low-level authoring would produce.
/// </summary>
/// <remarks>
/// <para>First-cut coverage: page setup (size + margins), report parameters (incl. Available Values —
/// static <c>&lt;ParameterValues&gt;</c> and query <c>&lt;DataSetReference&gt;</c>), and free-form
/// report items in the Body / Page header / Page footer — <c>Textbox</c> (→ TextBox/Label),
/// <c>Line</c>, <c>Rectangle</c> (shape + nested items, offset to absolute), and external
/// <c>Image</c>. VB expressions (<c>=Fields!X.Value</c>, …) are translated by
/// <see cref="RdlExpression"/>.</para>
/// <para>Not yet imported (follow-ups): Tablix/Matrix and Chart data regions, embedded-image bytes,
/// dataset queries, subreports, and rich styling. These are skipped, not errored — the structural
/// import always succeeds.</para>
/// </remarks>
public sealed class RdlImporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public ReportDefinition Import(Stream stream, string? reportName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Import(XDocument.Load(stream, LoadOptions.None), reportName);
    }

    public ReportDefinition ImportXml(string xml, string? reportName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(xml);
        return Import(XDocument.Parse(xml), reportName);
    }

    public ReportDefinition Import(XDocument document, string? reportName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var report = document.Root ?? throw new FormatException("Empty RDL document.");
        if (report.Name.LocalName != "Report")
        {
            throw new FormatException($"Root element must be <Report>, found <{report.Name.LocalName}>.");
        }

        var page = El(report, "Page");
        var marginHost = page ?? report;
        var pageWidth = ParseSize(Val(marginHost, "PageWidth")) ?? PaperSize.A4.Width;
        var pageHeight = ParseSize(Val(marginHost, "PageHeight")) ?? PaperSize.A4.Height;
        var margins = new Thickness(
            ParseSize(Val(marginHost, "LeftMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "TopMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "RightMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "BottomMargin")) ?? Unit.Zero);
        var pageSetup = new PageSetup(new PaperSize("Imported", pageWidth, pageHeight), Orientation.Portrait, margins);

        var parameters = (El(report, "ReportParameters")?.Elements()
                .Where(e => e.Name.LocalName == "ReportParameter")
                .Select(ReadParameter).ToArray())
            ?? Array.Empty<ReportParameter>();

        // Body's free report items render once → a ReportHeader band. Page header/footer map directly.
        var body = El(report, "Body");
        var reportHeader = BandFrom(El(body, "ReportItems"), Val(body, "Height"), BandKind.ReportHeader);
        var pageHeaderEl = El(page, "PageHeader");
        var pageHeader = BandFrom(El(pageHeaderEl, "ReportItems"), Val(pageHeaderEl, "Height"), BandKind.PageHeader);
        var pageFooterEl = El(page, "PageFooter");
        var pageFooter = BandFrom(El(pageFooterEl, "ReportItems"), Val(pageFooterEl, "Height"), BandKind.PageFooter);

        return new ReportDefinition(reportName ?? "RdlReport", pageSetup, DetailBand.Empty)
        {
            Parameters = new EquatableArray<ReportParameter>(parameters),
            ReportHeader = reportHeader,
            PageHeader = pageHeader,
            PageFooter = pageFooter,
        };
    }

    private static ReportBand? BandFrom(XElement? reportItems, string? heightRaw, BandKind kind)
    {
        if (reportItems is null)
        {
            return null;
        }
        var elements = new List<ReportElement>();
        foreach (var item in reportItems.Elements())
        {
            AddItem(item, Unit.Zero, Unit.Zero, elements);
        }
        if (elements.Count == 0)
        {
            return null;
        }
        var height = ParseSize(heightRaw) ?? BoundsHeight(elements);
        return new ReportBand(kind, height, new EquatableArray<ReportElement>(elements));
    }

    // Maps one RDL report item to OmniReport element(s), offsetting by (dx, dy) so nested items inside a
    // Rectangle become absolute. Unknown item kinds are skipped (structural import never fails on them).
    private static void AddItem(XElement item, Unit dx, Unit dy, List<ReportElement> into)
    {
        var bounds = Bounds(item, dx, dy);
        switch (item.Name.LocalName)
        {
            case "Textbox":
                into.Add(ApplyCommon(TextItem(item, bounds), item));
                break;
            case "Line":
                into.Add(ApplyCommon(new LineElement { Bounds = bounds }, item));
                break;
            case "Image":
                into.Add(ApplyCommon(ImageItem(item, bounds), item));
                break;
            case "Rectangle":
                into.Add(ApplyCommon(new RectangleElement { Bounds = bounds }, item));
                // Recurse: nested items are positioned relative to the rectangle in RDL.
                foreach (var child in El(item, "ReportItems")?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    AddItem(child, bounds.X, bounds.Y, into);
                }
                break;
            // Tablix, Chart, Subreport, … — follow-ups; skipped, not errored.
        }
    }

    // Applies the report-item attributes common to every element — the RDL <Style>, Visibility/Hidden,
    // Bookmark, DocumentMapLabel and Action — that the model already supports but the importer ignored.
    private static ReportElement ApplyCommon(ReportElement el, XElement item)
    {
        var style = ReadStyle(item);
        var (visible, visExpr) = ReadVisibility(item);
        var bookmark = ConvertOpt(Val(item, "Bookmark"));
        var docMap = ConvertOpt(Val(item, "DocumentMapLabel"));
        var action = ReadAction(El(item, "Action"));
        return el switch
        {
            TextBoxElement t => t with { Style = style ?? t.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            LabelElement l => l with { Style = style ?? l.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            RectangleElement r => r with { Style = style ?? r.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            ImageElement im => im with { Style = style ?? im.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            // A line's color/width come from its style border; map the first visible side to the Pen.
            LineElement ln => ln with { Pen = StyleBorderToPen(style) ?? ln.Pen, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            // INVARIANT: every element type AddItem can produce must have an arm above — otherwise its
            // Style/Visibility/Bookmark/Action would be silently dropped. Keep this in sync with AddItem.
            _ => el,
        };
    }

    // RDL <Visibility><Hidden> is the inverse of OmniReport's Visible/VisibleExpression: a constant maps
    // to the Visible bool; an expression becomes VisibleExpression = !(converted Hidden).
    private static (bool Visible, string? VisibleExpression) ReadVisibility(XElement item)
    {
        var hidden = Val(El(item, "Visibility"), "Hidden");
        if (string.IsNullOrWhiteSpace(hidden))
        {
            return (true, null);
        }
        if (bool.TryParse(hidden, out var h))
        {
            return (!h, null);
        }
        return (true, $"!({RdlExpression.Convert(hidden)})");
    }

    private static string? ConvertOpt(string? raw)
        => string.IsNullOrEmpty(raw) ? null : RdlExpression.Convert(raw);

    private static ReportElement TextItem(XElement item, Rectangle bounds)
    {
        var raw = TextboxValue(item);
        if (RdlExpression.IsExpression(raw))
        {
            return new TextBoxElement
            {
                Expression = RdlExpression.Convert(raw),
                Bounds = bounds,
                CanGrow = ParseBool(Val(item, "CanGrow")),
                CanShrink = ParseBool(Val(item, "CanShrink")),
            };
        }
        return new LabelElement { Text = raw ?? string.Empty, Bounds = bounds };
    }

    private static bool ParseBool(string? raw) => bool.TryParse(raw, out var b) && b;

    // RDL 2016 nests the value under Paragraphs/Paragraph/TextRuns/TextRun/Value; RDL 2008 uses a direct
    // <Value>. Returns the first value found.
    private static string? TextboxValue(XElement textbox)
    {
        var run = El(El(El(El(textbox, "Paragraphs"), "Paragraph"), "TextRuns"), "TextRun");
        return Val(run, "Value") ?? Val(textbox, "Value");
    }

    private static ReportElement ImageItem(XElement item, Rectangle bounds)
    {
        var source = Val(item, "Source");
        var value = Val(item, "Value");
        if (string.Equals(source, "External", StringComparison.OrdinalIgnoreCase))
        {
            return RdlExpression.IsExpression(value)
                ? new ImageElement { Source = ImageSourceKind.Expression, Expression = RdlExpression.Convert(value), Bounds = bounds }
                : new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds };
        }
        // Embedded/Database image bytes are a follow-up — keep the reference name as the path.
        return new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds };
    }

    private static ReportParameter ReadParameter(XElement el)
    {
        var name = el.Attribute("Name")?.Value ?? throw new FormatException("ReportParameter missing Name.");
        var type = MapType(Val(el, "DataType"));
        var prompt = Val(el, "Prompt");
        var nullable = string.Equals(Val(el, "Nullable"), "true", StringComparison.OrdinalIgnoreCase);
        var multiValue = string.Equals(Val(el, "MultiValue"), "true", StringComparison.OrdinalIgnoreCase);

        var defaultEl = El(el, "DefaultValue");
        object? defaultValue = null;
        var defaultRaw = El(El(defaultEl, "Values"), "Value")?.Value;
        if (!string.IsNullOrEmpty(defaultRaw) && !RdlExpression.IsExpression(defaultRaw))
        {
            try { defaultValue = System.Convert.ChangeType(defaultRaw, type, Inv); }
            catch (FormatException) { }
            catch (InvalidCastException) { }
        }

        ParameterAvailableValues? available = ReadAvailableValues(El(el, "ValidValues"));

        // RDL: a parameter is required only when it's neither nullable nor has a default supplied.
        var required = !nullable && defaultEl is null;
        return new ReportParameter(name, type, prompt, defaultValue, multiValue, required, available);
    }

    private static ParameterAvailableValues? ReadAvailableValues(XElement? validValues)
    {
        if (validValues is null)
        {
            return null;
        }
        // Query-driven: <DataSetReference><DataSetName/><ValueField/><LabelField/>.
        var dsRef = El(validValues, "DataSetReference");
        if (dsRef is not null && Val(dsRef, "DataSetName") is { Length: > 0 } dsName)
        {
            return ParameterAvailableValues.FromQuery(dsName, Val(dsRef, "ValueField") ?? string.Empty, Val(dsRef, "LabelField"));
        }
        // Static: <ParameterValues><ParameterValue><Value/><Label/>.
        var statics = El(validValues, "ParameterValues")?.Elements()
            .Where(e => e.Name.LocalName == "ParameterValue")
            .Select(pv => new ParameterValue(Val(pv, "Value") ?? string.Empty, Val(pv, "Label")))
            .ToArray();
        return statics is { Length: > 0 }
            ? new ParameterAvailableValues { Values = new EquatableArray<ParameterValue>(statics) }
            : null;
    }

    private static Type MapType(string? dataType) => dataType switch
    {
        "Integer" => typeof(int),
        "Float" => typeof(double),
        "Decimal" => typeof(decimal),
        "Boolean" => typeof(bool),
        "DateTime" => typeof(DateTime),
        _ => typeof(string),
    };

    // ── Style + Action ────────────────────────────────────────────────────────────

    // Reads an item's RDL <Style> child into an OmniReport Style (font, colors, alignment, format,
    // padding, border). Returns null when there's no <Style> or it contributes nothing.
    private static Style? ReadStyle(XElement item)
    {
        var s = El(item, "Style");
        if (s is null)
        {
            return null;
        }
        var fontFamily = Val(s, "FontFamily");
        var fontSizePt = ParsePoints(Val(s, "FontSize"));
        var fontStyle = FontStyle.Regular;
        if (IsBold(Val(s, "FontWeight"))) { fontStyle |= FontStyle.Bold; }
        if (string.Equals(Val(s, "FontStyle"), "Italic", StringComparison.OrdinalIgnoreCase)) { fontStyle |= FontStyle.Italic; }
        var decoration = Val(s, "TextDecoration");
        if (string.Equals(decoration, "Underline", StringComparison.OrdinalIgnoreCase)) { fontStyle |= FontStyle.Underline; }
        if (string.Equals(decoration, "LineThrough", StringComparison.OrdinalIgnoreCase)) { fontStyle |= FontStyle.Strikeout; }
        Font? font = (fontFamily is not null || fontSizePt is not null || fontStyle != FontStyle.Regular)
            ? new Font(fontFamily ?? Font.Default.Family, fontSizePt ?? Font.Default.Size, fontStyle)
            : null;

        var style = new Style(
            Font: font,
            ForeColor: ParseColor(Val(s, "Color")),
            BackColor: ParseColor(Val(s, "BackgroundColor")),
            Border: ReadBorder(s),
            Padding: ReadPadding(s),
            HorizontalAlignment: ParseHAlign(Val(s, "TextAlign")),
            VerticalAlignment: ParseVAlign(Val(s, "VerticalAlign")),
            Format: Val(s, "Format") is { Length: > 0 } fmt ? fmt : null);
        return style == Style.Default ? null : style;
    }

    private static Border? ReadBorder(XElement style)
    {
        // RDL: a default <Border> applies to all sides; <TopBorder>/<BottomBorder>/<LeftBorder>/
        // <RightBorder> override per side. Each carries <Color>/<Style>/<Width>.
        var def = ReadBorderSide(El(style, "Border"));
        var left = ReadBorderSide(El(style, "LeftBorder")) ?? def;
        var top = ReadBorderSide(El(style, "TopBorder")) ?? def;
        var right = ReadBorderSide(El(style, "RightBorder")) ?? def;
        var bottom = ReadBorderSide(El(style, "BottomBorder")) ?? def;
        if (left is null && top is null && right is null && bottom is null)
        {
            return null;
        }
        return new Border(left ?? BorderSide.None, top ?? BorderSide.None, right ?? BorderSide.None, bottom ?? BorderSide.None);
    }

    private static BorderSide? ReadBorderSide(XElement? border)
    {
        if (border is null)
        {
            return null;
        }
        var lineStyle = (Val(border, "Style") ?? "Solid") switch
        {
            "None" => BorderLineStyle.None,
            "Dotted" => BorderLineStyle.Dotted,
            "Dashed" => BorderLineStyle.Dashed,
            "Double" => BorderLineStyle.Double,
            _ => BorderLineStyle.Solid,
        };
        var width = ParseSize(Val(border, "Width")) ?? Unit.FromPoint(1);
        var color = ParseColor(Val(border, "Color")) ?? Color.Black;
        return new BorderSide(lineStyle, width, color);
    }

    private static Thickness? ReadPadding(XElement style)
    {
        var l = ParseSize(Val(style, "PaddingLeft"));
        var t = ParseSize(Val(style, "PaddingTop"));
        var r = ParseSize(Val(style, "PaddingRight"));
        var b = ParseSize(Val(style, "PaddingBottom"));
        if (l is null && t is null && r is null && b is null)
        {
            return null;
        }
        return new Thickness(l ?? Unit.Zero, t ?? Unit.Zero, r ?? Unit.Zero, b ?? Unit.Zero);
    }

    private static BorderSide? StyleBorderToPen(Style? style)
    {
        var b = style?.Border;
        if (b is null)
        {
            return null;
        }
        foreach (var side in new[] { b.Top, b.Left, b.Right, b.Bottom })
        {
            if (side.IsVisible) { return side; }
        }
        return null;
    }

    // Reads an RDL <Action> (Hyperlink / Drillthrough / BookmarkLink) into an ElementAction.
    private static ElementAction? ReadAction(XElement? action)
    {
        if (action is null)
        {
            return null;
        }
        if (Val(action, "Hyperlink") is { Length: > 0 } url)
        {
            return ElementAction.ToUrl(RdlExpression.Convert(url));
        }
        if (Val(action, "BookmarkLink") is { Length: > 0 } bm)
        {
            return ElementAction.ToBookmark(RdlExpression.Convert(bm));
        }
        var drill = El(action, "Drillthrough");
        if (drill is not null && Val(drill, "ReportName") is { Length: > 0 } report)
        {
            return ElementAction.ToDrillthrough(report);
        }
        return null;
    }

    private static bool IsBold(string? weight)
    {
        if (weight is null)
        {
            return false;
        }
        if (int.TryParse(weight, out var w))
        {
            return w >= 600; // RDL numeric weights 100–900
        }
        return weight.ToLowerInvariant() is "bold" or "bolder" or "semibold" or "demibold" or "extrabold" or "heavy" or "black";
    }

    // Font sizes are kept in points as a double — parse directly (not via Unit, whose integer mils would
    // round 14pt to 13.968). RDL font sizes are virtually always "pt".
    private static double? ParsePoints(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var s = raw.Trim();
        int i = 0;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] is '.' or '-' or '+')) { i++; }
        if (i == 0 || !double.TryParse(s[..i], NumberStyles.Float, Inv, out var value))
        {
            return null;
        }
        return s[i..].Trim().ToLowerInvariant() switch
        {
            "pt" or "" => value,
            "px" => value * 72.0 / 96.0,
            "in" => value * 72.0,
            "cm" => value * 72.0 / 2.54,
            "mm" => value * 72.0 / 25.4,
            "pc" => value * 12.0,
            _ => value,
        };
    }

    private static HorizontalAlignment ParseHAlign(string? a) => a switch
    {
        "Center" => HorizontalAlignment.Center,
        "Right" => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Left,
    };

    private static VerticalAlignment ParseVAlign(string? a) => a switch
    {
        "Middle" => VerticalAlignment.Middle,
        "Bottom" => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Top,
    };

    // RDL colors are "#RRGGBB"/"#AARRGGBB" or a named color. Returns null on empty/unknown (inherit).
    private static Color? ParseColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var c = raw.Trim();
        if (c.StartsWith('#'))
        {
            try { return Color.FromHex(c); }
            catch (FormatException) { return null; }
        }
        return c.ToLowerInvariant() switch
        {
            "black" => Color.FromRgb(0, 0, 0),
            "white" => Color.FromRgb(255, 255, 255),
            "red" => Color.FromRgb(255, 0, 0),
            "green" => Color.FromRgb(0, 128, 0),
            "lime" => Color.FromRgb(0, 255, 0),
            "blue" => Color.FromRgb(0, 0, 255),
            "yellow" => Color.FromRgb(255, 255, 0),
            "gray" or "grey" => Color.FromRgb(128, 128, 128),
            "silver" => Color.FromRgb(192, 192, 192),
            "lightgray" or "lightgrey" => Color.FromRgb(211, 211, 211),
            "navy" => Color.FromRgb(0, 0, 128),
            "orange" => Color.FromRgb(255, 165, 0),
            "purple" => Color.FromRgb(128, 0, 128),
            "transparent" => Color.Transparent,
            _ => null,
        };
    }

    // ── XML + size helpers (namespace-agnostic via LocalName) ─────────────────────

    private static XElement? El(XElement? parent, string name)
        => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static string? Val(XElement? parent, string name) => El(parent, name)?.Value;

    private static Rectangle Bounds(XElement item, Unit dx, Unit dy)
    {
        var left = (ParseSize(Val(item, "Left")) ?? Unit.Zero) + dx;
        var top = (ParseSize(Val(item, "Top")) ?? Unit.Zero) + dy;
        var width = ParseSize(Val(item, "Width")) ?? Unit.FromMm(25);
        var height = ParseSize(Val(item, "Height")) ?? Unit.FromMm(6);
        return new Rectangle(left, top, width, height);
    }

    private static Unit BoundsHeight(IEnumerable<ReportElement> elements)
    {
        Unit max = Unit.Zero;
        foreach (var e in elements)
        {
            if (e.Bounds.Bottom > max) { max = e.Bounds.Bottom; }
        }
        return max;
    }

    /// <summary>Parses an RDL size string (e.g. "2.5in", "21cm", "10mm", "20pt", "96px") into a
    /// <see cref="Unit"/>. Returns null when the input is empty/unparseable OR carries no/unknown unit
    /// suffix — the RDL schema mandates a unit, so a bare/odd value is treated as unspecified (the caller
    /// falls back) rather than silently guessed.</summary>
    internal static Unit? ParseSize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var s = raw.Trim();
        int i = 0;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] is '.' or '-' or '+')) { i++; }
        if (i == 0 || !double.TryParse(s[..i], NumberStyles.Float, Inv, out var value))
        {
            return null;
        }
        var unit = s[i..].Trim().ToLowerInvariant();
        return unit switch
        {
            "in" => Unit.FromInch(value),
            "cm" => Unit.FromCm(value),
            "mm" => Unit.FromMm(value),
            "pt" => Unit.FromPoint(value),
            "pc" => Unit.FromPoint(value * 12.0),
            "px" => Unit.FromPixels(value),
            _ => null, // no/unknown unit — RDL requires one; let the caller's fallback decide
        };
    }
}
