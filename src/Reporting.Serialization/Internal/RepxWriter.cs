using System.Globalization;
using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

/// <summary>Serializes a <see cref="ReportDefinition"/> into an <see cref="XDocument"/>.</summary>
internal static class RepxWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static XDocument Write(ReportDefinition definition, SchemaVersion version)
    {
        var root = new XElement("Report",
            new XAttribute("SchemaVersion", version.ToString()),
            new XAttribute("Name", definition.Name));

        root.Add(WritePageSetup(definition.PageSetup));

        if (definition.Parameters.Count > 0)
        {
            root.Add(new XElement("Parameters",
                definition.Parameters.Select(WriteParameter)));
        }

        if (definition.DataSources.Count > 0)
        {
            root.Add(new XElement("DataSources",
                definition.DataSources.Select(WriteDataSource)));
        }

        if (definition.Variables.Count > 0)
        {
            root.Add(new XElement("Variables",
                definition.Variables.Select(WriteVariable)));
        }

        if (definition.ReportHeader is not null)
        {
            root.Add(WriteBand("ReportHeader", definition.ReportHeader));
        }
        if (definition.PageHeader is not null)
        {
            root.Add(WriteBand("PageHeader", definition.PageHeader));
        }
        if (definition.Groups.Count > 0)
        {
            root.Add(new XElement("Groups", definition.Groups.Select(WriteGroup)));
        }

        root.Add(WriteDetail(definition.Detail));

        if (definition.PageFooter is not null)
        {
            root.Add(WriteBand("PageFooter", definition.PageFooter));
        }
        if (definition.ReportFooter is not null)
        {
            root.Add(WriteBand("ReportFooter", definition.ReportFooter));
        }

        if (definition.Metadata.Count > 0)
        {
            root.Add(new XElement("Metadata",
                definition.Metadata.Select(kv =>
                    new XElement("Entry", new XAttribute("Key", kv.Key), new XAttribute("Value", kv.Value)))));
        }

        if (definition.NamedStyles.Count > 0)
        {
            root.Add(new XElement("NamedStyles",
                definition.NamedStyles.Select(kv =>
                    new XElement("NamedStyle", new XAttribute("Name", kv.Key), WriteStyle(kv.Value)))));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    // ── PageSetup ───────────────────────────────────────────────────────────────

    private static XElement WritePageSetup(PageSetup setup)
    {
        var el = new XElement("PageSetup",
            new XAttribute("Orientation", setup.Orientation),
            new XAttribute("Margins", Formats.FormatThickness(setup.Margins)),
            new XAttribute("Columns", setup.Columns.ToString(Inv)),
            new XAttribute("ColumnSpacing", Formats.FormatUnit(setup.ColumnSpacing)),
            new XElement("Paper",
                new XAttribute("Name", setup.Paper.Name),
                new XAttribute("Width", Formats.FormatUnit(setup.Paper.Width)),
                new XAttribute("Height", Formats.FormatUnit(setup.Paper.Height))));
        return el;
    }

    // ── Parameters / Variables / DataSources ────────────────────────────────────

    private static XElement WriteParameter(ReportParameter p)
    {
        var el = new XElement("Parameter",
            new XAttribute("Name", p.Name),
            new XAttribute("Type", Formats.FormatType(p.ValueType)),
            new XAttribute("Required", p.Required),
            new XAttribute("AllowMultiple", p.AllowMultiple));
        if (p.Prompt is not null)
        {
            el.SetAttributeValue("Prompt", p.Prompt);
        }
        if (p.Nullable) { el.SetAttributeValue("Nullable", "true"); }
        if (p.AllowBlank) { el.SetAttributeValue("AllowBlank", "true"); }
        if (p.Hidden) { el.SetAttributeValue("Hidden", "true"); }
        if (p.DefaultValue is not null)
        {
            el.Add(new XElement("DefaultValue",
                new XAttribute("Value", Convert.ToString(p.DefaultValue, Inv) ?? string.Empty)));
        }
        if (p.DefaultValueExpression is not null)
        {
            el.SetAttributeValue("DefaultValueExpression", p.DefaultValueExpression);
        }
        if (p.AvailableValues is { } av && (av.Values.Count > 0 || av.IsQuery))
        {
            var ave = new XElement("AvailableValues");
            if (av.DataSet is not null) { ave.SetAttributeValue("DataSet", av.DataSet); }
            if (av.ValueField is not null) { ave.SetAttributeValue("ValueField", av.ValueField); }
            if (av.LabelField is not null) { ave.SetAttributeValue("LabelField", av.LabelField); }
            if (av.FilterField is not null) { ave.SetAttributeValue("FilterField", av.FilterField); }
            if (av.DependsOn is not null) { ave.SetAttributeValue("DependsOn", av.DependsOn); }
            foreach (var v in av.Values)
            {
                var ve = new XElement("Value", new XAttribute("Value", v.Value));
                if (v.Label is not null) { ve.SetAttributeValue("Label", v.Label); }
                ave.Add(ve);
            }
            el.Add(ave);
        }
        return el;
    }

    private static XElement WriteVariable(ReportVariable v)
        => new("Variable",
            new XAttribute("Name", v.Name),
            new XAttribute("Expression", v.Expression),
            new XAttribute("Scope", v.Scope));

    private static XElement WriteDataSource(DataSourceDefinition ds)
    {
        var el = new XElement("DataSource", new XAttribute("Name", ds.Name));
        if (ds.DataMember is not null)
        {
            el.SetAttributeValue("DataMember", ds.DataMember);
        }
        if (ds.FilterExpression is not null)
        {
            el.SetAttributeValue("FilterExpression", ds.FilterExpression);
        }
        if (ds.Fields.Count > 0)
        {
            el.Add(new XElement("Fields", ds.Fields.Select(f =>
            {
                var fe = new XElement("Field", new XAttribute("Name", f.Name));
                if (f.FieldType is not null)
                {
                    fe.SetAttributeValue("Type", Formats.FormatType(f.FieldType));
                }
                if (f.DisplayName is not null)
                {
                    fe.SetAttributeValue("DisplayName", f.DisplayName);
                }
                return fe;
            })));
        }
        // RDL-style calculated fields: round-trip Name + Expression, plus optional ResultType.
        if (ds.CalculatedFields.Count > 0)
        {
            el.Add(new XElement("CalculatedFields", ds.CalculatedFields.Select(cf =>
            {
                var ce = new XElement("CalculatedField",
                    new XAttribute("Name", cf.Name),
                    new XAttribute("Expression", cf.Expression));
                if (cf.ResultType is not null)
                {
                    ce.SetAttributeValue("Type", Formats.FormatType(cf.ResultType));
                }
                return ce;
            })));
        }
        if (ds.Relations.Count > 0)
        {
            el.Add(new XElement("Relations", ds.Relations.Select(r => new XElement("Relation",
                new XAttribute("Name", r.Name),
                new XAttribute("ParentSource", r.ParentSource),
                new XAttribute("ParentField", r.ParentField),
                new XAttribute("ChildSource", r.ChildSource),
                new XAttribute("ChildField", r.ChildField)))));
        }
        if (ds.SortExpressions.Count > 0)
        {
            el.Add(WriteSortExpressions(ds.SortExpressions));
        }
        if (ds.Parameters.Count > 0)
        {
            el.Add(new XElement("Parameters", ds.Parameters.Select(kv =>
                new XElement("Parameter", new XAttribute("Key", kv.Key), new XAttribute("Value", kv.Value)))));
        }
        return el;
    }

    // ── Shared Sort/Filter helpers (data sources, bands, groups all use them) ───

    private static XElement WriteSortExpressions(EquatableArray<SortDescriptor> sorts)
        => new("SortExpressions", sorts.Select(s => new XElement("Sort",
            new XAttribute("Expression", s.Expression),
            new XAttribute("Direction", s.Direction))));

    // ── Bands ───────────────────────────────────────────────────────────────────

    private static XElement WriteBand(string tagName, ReportBand band)
    {
        var el = new XElement(tagName,
            new XAttribute("Kind", band.Kind),
            new XAttribute("Height", Formats.FormatUnit(band.Height)),
            new XAttribute("Visible", band.Visible),
            new XAttribute("PrintOnFirstPage", band.PrintOnFirstPage),
            new XAttribute("PrintOnLastPage", band.PrintOnLastPage));
        if (band.VisibleExpression is not null)
        {
            el.SetAttributeValue("VisibleExpression", band.VisibleExpression);
        }
        if (band.PageBreak != PageBreak.None)
        {
            el.SetAttributeValue("PageBreak", band.PageBreak);
        }
        el.Add(new XElement("Elements", band.Elements.Select(WriteElement)));
        return el;
    }

    private static XElement WriteDetail(DetailBand band)
    {
        var el = new XElement("Detail",
            new XAttribute("Height", Formats.FormatUnit(band.Height)),
            new XAttribute("Visible", band.Visible),
            new XAttribute("CanGrow", band.CanGrow),
            new XAttribute("CanShrink", band.CanShrink));
        if (band.VisibleExpression is not null)
        {
            el.SetAttributeValue("VisibleExpression", band.VisibleExpression);
        }
        if (band.PageBreak != PageBreak.None)
        {
            el.SetAttributeValue("PageBreak", band.PageBreak);
        }
        if (band.NoRowsMessage is not null)
        {
            el.SetAttributeValue("NoRowsMessage", band.NoRowsMessage);
        }
        if (band.DataSetName is not null)
        {
            el.SetAttributeValue("DataSetName", band.DataSetName);
        }
        if (band.FilterExpression is not null)
        {
            el.SetAttributeValue("FilterExpression", band.FilterExpression);
        }
        if (band.SortExpressions.Count > 0)
        {
            el.Add(WriteSortExpressions(band.SortExpressions));
        }
        el.Add(new XElement("Elements", band.Elements.Select(WriteElement)));
        // Sub-detail bands round-trip alongside the Detail so master-detail reports survive
        // .repx save/load. Each sub-band carries its own DataMember + elements + optional
        // Header/Footer sub-trees.
        if (band.SubDetails.Count > 0)
        {
            el.Add(new XElement("SubDetails", band.SubDetails.Select(WriteSubDetail)));
        }
        return el;
    }

    private static XElement WriteSubDetail(SubDetailBand sd)
    {
        var el = new XElement("SubDetail",
            new XAttribute("Name", sd.Name),
            new XAttribute("DataMember", sd.DataMember),
            new XAttribute("Height", Formats.FormatUnit(sd.Height)),
            new XAttribute("Visible", sd.Visible),
            new XAttribute("PrintIfEmpty", sd.PrintIfEmpty));
        if (sd.VisibleExpression is not null)
        {
            el.SetAttributeValue("VisibleExpression", sd.VisibleExpression);
        }
        if (sd.NoRowsMessage is not null)
        {
            el.SetAttributeValue("NoRowsMessage", sd.NoRowsMessage);
        }
        if (sd.FilterExpression is not null)
        {
            el.SetAttributeValue("FilterExpression", sd.FilterExpression);
        }
        if (sd.SortExpressions.Count > 0)
        {
            el.Add(WriteSortExpressions(sd.SortExpressions));
        }
        if (sd.Header is { } h)
        {
            // Header/footer reuse the ReportBand serialization shape — they're attached as
            // child elements rather than separate top-level entries to keep the tree compact.
            el.Add(new XElement("Header",
                new XAttribute("Height", Formats.FormatUnit(h.Height)),
                new XElement("Elements", h.Elements.Select(WriteElement))));
        }
        el.Add(new XElement("Elements", sd.Elements.Select(WriteElement)));
        if (sd.Footer is { } f)
        {
            el.Add(new XElement("Footer",
                new XAttribute("Height", Formats.FormatUnit(f.Height)),
                new XElement("Elements", f.Elements.Select(WriteElement))));
        }
        return el;
    }

    private static XElement WriteGroup(GroupBand g)
    {
        var el = new XElement("Group",
            new XAttribute("Name", g.Name),
            new XAttribute("GroupExpression", g.GroupExpression),
            new XAttribute("KeepTogether", g.KeepTogether),
            new XAttribute("NewPageBefore", g.NewPageBefore),
            new XAttribute("NewPageAfter", g.NewPageAfter),
            new XAttribute("RepeatHeaderOnNewPage", g.RepeatHeaderOnNewPage),
            new XAttribute("Visible", g.Visible));
        if (g.VisibleExpression is not null)
        {
            el.SetAttributeValue("VisibleExpression", g.VisibleExpression);
        }
        if (g.PageBreak != PageBreak.None)
        {
            el.SetAttributeValue("PageBreak", g.PageBreak);
        }
        if (g.FilterExpression is not null)
        {
            el.SetAttributeValue("FilterExpression", g.FilterExpression);
        }
        if (g.SortExpressions.Count > 0)
        {
            el.Add(WriteSortExpressions(g.SortExpressions));
        }
        if (g.Variables.Count > 0)
        {
            el.Add(new XElement("Variables", g.Variables.Select(WriteVariable)));
        }
        if (g.Header is not null)
        {
            el.Add(WriteBand("Header", g.Header));
        }
        if (g.Footer is not null)
        {
            el.Add(WriteBand("Footer", g.Footer));
        }
        return el;
    }

    // ── Elements ────────────────────────────────────────────────────────────────

    private static XElement WriteElement(ReportElement element)
    {
        var (tag, payload) = element switch
        {
            LabelElement lbl => ("Label", new XElement[] { new("Text", lbl.Text) }),
            TextBoxElement tb => ("TextBox", WriteTextBoxContent(tb)),
            LineElement line => ("Line",
                new XElement[]
                {
                    new("Direction", line.Direction.ToString()),
                    new("Pen",
                        new XAttribute("Style", line.Pen.Style),
                        new XAttribute("Thickness", Formats.FormatUnit(line.Pen.Thickness)),
                        new XAttribute("Color", Formats.FormatColor(line.Pen.Color))),
                }),
            RectangleElement rect => ("Rectangle",
                new XElement[]
                {
                    new("FillColor", rect.FillColor is null ? "" : Formats.FormatColor(rect.FillColor.Value)),
                    new("CornerRadius", Formats.FormatUnit(rect.CornerRadius)),
                    // Nested container children (relative bounds) — recurse via the same element writer
                    // (precedent: TablixCell.Content). Always emitted (an empty <Children/> reads back to the
                    // empty default, so round-trip equality holds either way).
                    new("Children", rect.Children.Select(WriteElement)),
                }),
            EllipseElement ellipse => ("Ellipse",
                new XElement[]
                {
                    new("FillColor", ellipse.FillColor is null ? "" : Formats.FormatColor(ellipse.FillColor.Value)),
                }),
            ImageElement img => ("Image", WriteImageContent(img)),
            BarcodeElement bc => ("Barcode",
                new XElement[]
                {
                    new("Symbology", bc.Symbology.ToString()),
                    new("Expression", bc.Expression),
                    new("ShowText", bc.ShowText.ToString()),
                    new("QrEcc", bc.QrEcc.ToString()),
                }),
            ChartElement chart => ("Chart", WriteChartContent(chart)),
            SubreportElement sub => ("Subreport", WriteSubreportContent(sub)),
            TableElement table => ("Table", WriteTableContent(table)),
            // ── RDL F2 advanced elements (scaffold) ─────────────────────────────
            TablixElement tablix => ("Tablix", WriteTablixContent(tablix)),
            CodeElement code => ("Code", WriteCodeContent(code)),
            MapElement map => ("Map", WriteMapContent(map)),
            GaugeElement gauge => ("Gauge", WriteGaugeContent(gauge)),
            DataBarElement bar => ("DataBar", WriteDataBarContent(bar)),
            SparklineElement spark => ("Sparkline", WriteSparklineContent(spark)),
            IndicatorElement ind => ("Indicator", WriteIndicatorContent(ind)),
            // Convention-based fallback: a new all-scalar element serializes without a hand-written arm.
            // TagFor throws the same "unsupported element type" error for a non-auto-serializable type.
            _ => (ElementSerializationRegistry.TagFor(element),
                  ElementSerializationRegistry.WriteXml(element).ToArray()),
        };

        var el = new XElement(tag,
            new XAttribute("Id", element.Id),
            new XAttribute("Bounds", Formats.FormatRectangle(element.Bounds)),
            new XAttribute("Visible", element.Visible));
        if (element.Name is not null)
        {
            el.SetAttributeValue("Name", element.Name);
        }
        if (element.VisibleExpression is not null)
        {
            el.SetAttributeValue("VisibleExpression", element.VisibleExpression);
        }
        // TextBox autosize — read back as attributes (see RepxReader.ReadTextBoxElement); emitted
        // only when set so the common element shape stays cheap.
        if (element is TextBoxElement tbAuto)
        {
            if (tbAuto.CanGrow) el.SetAttributeValue("CanGrow", true);
            if (tbAuto.CanShrink) el.SetAttributeValue("CanShrink", true);
        }
        // RDL-style extensions on the abstract base — emitted as attrs when set so the
        // common shape stays cheap for elements that don't use them.
        if (element.Bookmark is not null)
        {
            el.SetAttributeValue("Bookmark", element.Bookmark);
        }
        if (element.DocumentMapLabel is not null)
        {
            el.SetAttributeValue("DocumentMapLabel", element.DocumentMapLabel);
        }
        if (element.ToggleItemId is not null)
        {
            el.SetAttributeValue("ToggleItemId", element.ToggleItemId);
        }
        if (element.InitiallyHidden)
        {
            el.SetAttributeValue("InitiallyHidden", element.InitiallyHidden);
        }
        el.Add(WriteStyle(element.Style));
        if (element.ConditionalFormats.Count > 0)
        {
            el.Add(new XElement("ConditionalFormats", element.ConditionalFormats.Select(cf =>
                new XElement("ConditionalFormat", new XAttribute("Condition", cf.Condition), WriteStyle(cf.Style)))));
        }
        if (element.PropertyExpressions.Count > 0)
        {
            el.Add(new XElement("PropertyExpressions", element.PropertyExpressions.Select(kv =>
                new XElement("PropertyExpression", new XAttribute("Path", kv.Key), new XAttribute("Expression", kv.Value)))));
        }
        // Action is serialized as a child element (rather than attribute) because it has
        // its own sub-tree for drillthrough parameters.
        if (element.Action is { } act)
        {
            el.Add(WriteAction(act));
        }
        foreach (var child in payload)
        {
            el.Add(child);
        }
        return el;
    }

    private static XElement WriteAction(ElementAction action)
    {
        var el = new XElement("Action", new XAttribute("Kind", action.Kind));
        if (action.Hyperlink is not null)
        {
            el.SetAttributeValue("Hyperlink", action.Hyperlink);
        }
        if (action.BookmarkId is not null)
        {
            el.SetAttributeValue("BookmarkId", action.BookmarkId);
        }
        if (action.DrillthroughReportName is not null)
        {
            el.SetAttributeValue("DrillthroughReportName", action.DrillthroughReportName);
        }
        if (action.DrillthroughParameters.Count > 0)
        {
            el.Add(new XElement("Parameters", action.DrillthroughParameters.Select(p =>
                new XElement("Parameter",
                    new XAttribute("Name", p.Name),
                    new XAttribute("Value", p.Value),
                    new XAttribute("Omit", p.Omit)))));
        }
        return el;
    }

    private static XElement[] WriteImageContent(ImageElement img)
    {
        var content = new List<XElement>
        {
            new("Source", img.Source.ToString()),
            new("Sizing", img.Sizing.ToString()),
        };
        if (img.Path is not null)
        {
            content.Add(new XElement("Path", img.Path));
        }
        if (img.Expression is not null)
        {
            content.Add(new XElement("Expression", img.Expression));
        }
        if (img.InlineData.Count > 0)
        {
            var bytes = new byte[img.InlineData.Count];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = img.InlineData[i];
            }
            content.Add(new XElement("InlineData", Convert.ToBase64String(bytes)));
        }
        return [.. content];
    }

    private static XElement[] WriteChartContent(ChartElement chart)
    {
        var content = new List<XElement>
        {
            new("Kind", chart.Kind.ToString()),
            new("ShowLegend", chart.ShowLegend.ToString()),
        };
        if (chart.Title is not null)
        {
            content.Add(new XElement("Title", chart.Title));
        }
        if (chart.Series.Count > 0)
        {
            content.Add(new XElement("Series", chart.Series.Select(s =>
            {
                var se = new XElement("Series",
                    new XAttribute("Name", s.Name),
                    new XAttribute("CategoryExpression", s.CategoryExpression),
                    new XAttribute("ValueExpression", s.ValueExpression));
                if (s.Color is not null)
                {
                    se.SetAttributeValue("Color", Formats.FormatColor(s.Color.Value));
                }
                if (s.SizeExpression is not null) se.SetAttributeValue("SizeExpression", s.SizeExpression);
                if (s.HighExpression is not null) se.SetAttributeValue("HighExpression", s.HighExpression);
                if (s.LowExpression is not null) se.SetAttributeValue("LowExpression", s.LowExpression);
                return se;
            })));
        }
        return [.. content];
    }

    private static XElement[] WriteSubreportContent(SubreportElement sub)
    {
        var content = new List<XElement>();
        if (sub.ReportId is not null)
        {
            content.Add(new XElement("ReportId", sub.ReportId));
        }
        if (sub.InlineDefinition is not null)
        {
            content.Add(new XElement("InlineDefinition", Write(sub.InlineDefinition, SchemaVersion.Current).Root!));
        }
        if (sub.DataExpression is not null)
        {
            content.Add(new XElement("DataExpression", sub.DataExpression));
        }
        if (sub.ParameterBindings.Count > 0)
        {
            content.Add(new XElement("ParameterBindings", sub.ParameterBindings.Select(kv =>
                new XElement("Binding", new XAttribute("Key", kv.Key), new XAttribute("Value", kv.Value)))));
        }
        return [.. content];
    }

    private static XElement[] WriteTableContent(TableElement table)
    {
        var content = new List<XElement>
        {
            new("HeaderHeight", Formats.FormatUnit(table.HeaderHeight)),
            new("DetailHeight", Formats.FormatUnit(table.DetailHeight)),
            new("FooterHeight", Formats.FormatUnit(table.FooterHeight)),
            new("Columns", table.Columns.Select(c =>
            {
                var ce = new XElement("Column",
                    new XAttribute("Name", c.Name),
                    new XAttribute("Width", Formats.FormatUnit(c.Width)));
                if (c.HeaderText is not null)
                {
                    ce.SetAttributeValue("HeaderText", c.HeaderText);
                }
                if (c.DetailExpression is not null)
                {
                    ce.SetAttributeValue("DetailExpression", c.DetailExpression);
                }
                if (c.FooterExpression is not null)
                {
                    ce.SetAttributeValue("FooterExpression", c.FooterExpression);
                }
                return ce;
            })),
        };
        if (table.DataExpression is not null)
        {
            content.Add(new XElement("DataExpression", table.DataExpression));
        }
        return [.. content];
    }

    // ── Styling ─────────────────────────────────────────────────────────────────

    private static XElement WriteStyle(Style style)
    {
        var el = new XElement("Style",
            new XAttribute("HorizontalAlignment", style.HorizontalAlignment),
            new XAttribute("VerticalAlignment", style.VerticalAlignment),
            new XAttribute("WordWrap", style.WordWrap));
        if (style.Format is not null)
        {
            el.SetAttributeValue("Format", style.Format);
        }
        if (style.Font is not null)
        {
            el.Add(WriteFont(style.Font));
        }
        if (style.ForeColor is not null)
        {
            el.Add(new XElement("ForeColor", Formats.FormatColor(style.ForeColor.Value)));
        }
        if (style.BackColor is not null)
        {
            el.Add(new XElement("BackColor", Formats.FormatColor(style.BackColor.Value)));
        }
        if (style.Padding is not null)
        {
            el.Add(new XElement("Padding", Formats.FormatThickness(style.Padding.Value)));
        }
        if (style.Border is not null)
        {
            el.Add(WriteBorder(style.Border));
        }
        if (style.BackgroundImage is { } bg)
        {
            var bgEl = new XElement("BackgroundImage");
            if (bg.Path is not null) bgEl.SetAttributeValue("Path", bg.Path);
            if (bg.Expression is not null) bgEl.SetAttributeValue("Expression", bg.Expression);
            el.Add(bgEl);
        }
        // Gradient: emit only when active, so existing solid-fill styles round-trip byte-identical.
        if (style.BackgroundGradient != BackgroundGradientType.None)
        {
            el.SetAttributeValue("BackgroundGradient", style.BackgroundGradient);
        }
        if (style.BackColorEnd is not null)
        {
            el.Add(new XElement("BackColorEnd", Formats.FormatColor(style.BackColorEnd.Value)));
        }
        if (style.BasedOn is not null)
        {
            el.SetAttributeValue("BasedOn", style.BasedOn);
        }
        return el;
    }

    private static XElement WriteFont(Font font)
        => new("Font",
            new XAttribute("Family", font.Family),
            new XAttribute("Size", font.Size.ToString(Inv)),
            new XAttribute("Style", font.Style));

    private static XElement WriteBorder(Border border)
        => new("Border",
            WriteSide("Left", border.Left),
            WriteSide("Top", border.Top),
            WriteSide("Right", border.Right),
            WriteSide("Bottom", border.Bottom));

    private static XElement WriteSide(string position, BorderSide side)
        => new("Side",
            new XAttribute("Position", position),
            new XAttribute("Style", side.Style),
            new XAttribute("Thickness", Formats.FormatUnit(side.Thickness)),
            new XAttribute("Color", Formats.FormatColor(side.Color)));

    // ── RDL F1.8 TextRun: rich text inside a TextBox ─────────────────────────────

    private static XElement[] WriteTextBoxContent(TextBoxElement tb)
    {
        // Legacy single-expression payload remains the primary surface. TextRuns are
        // additive — emitted only when populated so existing .repx round-trip
        // unchanged (no spurious empty <TextRuns/> noise).
        var content = new List<XElement> { new("Expression", tb.Expression) };
        if (tb.TextRuns.Count > 0)
        {
            content.Add(new XElement("TextRuns", tb.TextRuns.Select(WriteTextRun)));
        }
        return [.. content];
    }

    private static XElement WriteTextRun(TextRun run)
    {
        var el = new XElement("TextRun", new XElement("Value", run.Value));
        if (run.Style is not null) el.Add(WriteStyle(run.Style));
        if (run.Action is not null) el.Add(WriteAction(run.Action));
        return el;
    }

    // ── RDL F2 advanced elements — round-trip-only serialization ─────────────────
    // The renderer doesn't draw these yet; the goal is to preserve every field so a
    // .repx authored elsewhere (SSRS, third-party designer) survives a load/save
    // cycle through OmniReport without losing the element's configuration. Render
    // support lands incrementally without changing this wire format.

    private static XElement[] WriteTablixContent(TablixElement t)
    {
        var content = new List<XElement>();
        if (t.DataSetName is not null) content.Add(new XElement("DataSetName", t.DataSetName));
        if (t.RowSubtotals) content.Add(new XElement("RowSubtotals", "true"));
        if (t.ColumnSubtotals) content.Add(new XElement("ColumnSubtotals", "true"));
        if (t.SubtotalLabel is not null) content.Add(new XElement("SubtotalLabel", t.SubtotalLabel));
        if (t.GrandTotalLabel is not null) content.Add(new XElement("GrandTotalLabel", t.GrandTotalLabel));
        if (t.NoRowsMessage is not null) content.Add(new XElement("NoRowsMessage", t.NoRowsMessage));
        if (!t.RepeatColumnHeaders) content.Add(new XElement("RepeatColumnHeaders", "false")); // default true
        if (t.KeepTogether) content.Add(new XElement("KeepTogether", "true")); // default false
        if (t.ColumnWidths.Count > 0)
        {
            content.Add(new XElement("ColumnWidths",
                t.ColumnWidths.Select(wt => new XElement("W", wt.ToString(Inv)))));
        }
        if (t.RowGroups.Count > 0)
        {
            content.Add(new XElement("RowGroups", t.RowGroups.Select(WriteTablixGroup)));
        }
        if (t.ColumnGroups.Count > 0)
        {
            content.Add(new XElement("ColumnGroups", t.ColumnGroups.Select(WriteTablixGroup)));
        }
        if (t.Cells.Count > 0)
        {
            content.Add(new XElement("Cells", t.Cells.Select(c =>
            {
                var ce = new XElement("Cell",
                    new XAttribute("RowIndex", c.RowIndex.ToString(Inv)),
                    new XAttribute("ColumnIndex", c.ColumnIndex.ToString(Inv)));
                if (c.ColumnSpan != 1)
                {
                    ce.Add(new XAttribute("ColumnSpan", c.ColumnSpan.ToString(Inv)));
                }
                if (c.RowSpan != 1)
                {
                    ce.Add(new XAttribute("RowSpan", c.RowSpan.ToString(Inv)));
                }
                if (c.Content is not null)
                {
                    ce.Add(new XElement("Content", WriteElement(c.Content)));
                }
                return ce;
            })));
        }
        return [.. content];
    }

    private static XElement WriteTablixGroup(TablixGroup g)
    {
        var ge = new XElement("Group", new XAttribute("Name", g.Name));
        if (g.GroupExpression is not null) ge.SetAttributeValue("GroupExpression", g.GroupExpression);
        if (g.SortExpression is not null)  ge.SetAttributeValue("SortExpression", g.SortExpression);
        if (g.SortDescending)              ge.SetAttributeValue("SortDescending", true);
        return ge;
    }

    private static XElement[] WriteCodeContent(CodeElement code)
    {
        var content = new List<XElement>
        {
            new("Language", code.Language.ToString()),
            new("Source", new XCData(code.Source)),
        };
        return [.. content];
    }

    private static XElement[] WriteMapContent(MapElement map)
    {
        var content = new List<XElement>();
        if (map.Basemap is not null)             content.Add(new XElement("Basemap", map.Basemap));
        if (map.DataSetName is not null)         content.Add(new XElement("DataSetName", map.DataSetName));
        if (map.LatitudeExpression is not null)  content.Add(new XElement("Latitude", map.LatitudeExpression));
        if (map.LongitudeExpression is not null) content.Add(new XElement("Longitude", map.LongitudeExpression));
        if (map.ShapeSet is not null)            content.Add(new XElement("ShapeSet", map.ShapeSet));
        if (map.ShapesGeoJson is not null)       content.Add(new XElement("ShapesGeoJson", new XCData(map.ShapesGeoJson)));
        content.Add(new XElement("ShowGraticule", map.ShowGraticule.ToString()));
        content.Add(new XElement("ShapeFill", map.ShapeFill));
        content.Add(new XElement("ShapeStroke", map.ShapeStroke));
        return [.. content];
    }

    private static XElement[] WriteGaugeContent(GaugeElement g)
    {
        var content = new List<XElement>
        {
            new("Kind", g.Kind.ToString()),
            new("Minimum", g.MinimumExpression),
            new("Maximum", g.MaximumExpression),
            new("Value", g.ValueExpression),
        };
        if (g.Ranges.Count > 0)
        {
            content.Add(new XElement("Ranges", g.Ranges.Select(r => new XElement("Range",
                new XAttribute("Start", r.StartExpression),
                new XAttribute("End", r.EndExpression),
                new XAttribute("Color", r.ColorHex)))));
        }
        return [.. content];
    }

    private static XElement[] WriteDataBarContent(DataBarElement b) =>
    [
        new("Value", b.ValueExpression),
        new("Minimum", b.MinimumExpression),
        new("Maximum", b.MaximumExpression),
        new("FillColor", b.FillColor),
    ];

    private static XElement[] WriteSparklineContent(SparklineElement s)
    {
        var content = new List<XElement>
        {
            new("Kind", s.Kind.ToString()),
            new("Value", s.ValueExpression),
        };
        if (s.DataSetName is not null)        content.Add(new XElement("DataSetName", s.DataSetName));
        if (s.CategoryExpression is not null) content.Add(new XElement("Category", s.CategoryExpression));
        return [.. content];
    }

    private static XElement[] WriteIndicatorContent(IndicatorElement i)
    {
        var content = new List<XElement>
        {
            new("Kind", i.Kind.ToString()),
            new("Value", i.ValueExpression),
        };
        if (i.States.Count > 0)
        {
            content.Add(new XElement("States", i.States.Select(st => new XElement("State",
                new XAttribute("Start", st.StartExpression),
                new XAttribute("End", st.EndExpression),
                new XAttribute("Icon", st.IconName)))));
        }
        return [.. content];
    }
}
