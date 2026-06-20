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

/// <summary>Reads a <see cref="ReportDefinition"/> back from a <see cref="XDocument"/>.</summary>
internal static class RepxReader
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static ReportDefinition Read(XDocument document, out SchemaVersion version)
    {
        var root = document.Root ?? throw new FormatException("Empty XML document.");
        if (root.Name.LocalName != "Report")
        {
            throw new FormatException($"Root element must be <Report>, found <{root.Name.LocalName}>.");
        }
        version = SchemaVersion.Parse(Attr(root, "SchemaVersion") ?? "1.0");
        return ReadDefinition(root, version);
    }

    private static ReportDefinition ReadDefinition(XElement root, SchemaVersion version)
    {
        var name = Attr(root, "Name") ?? "Untitled";
        var pageSetup = ReadPageSetup(root.Element("PageSetup")) ?? PageSetup.A4Portrait;

        var parameters = root.Element("Parameters")?.Elements("Parameter").Select(ReadParameter).ToArray()
                         ?? Array.Empty<ReportParameter>();

        var dataSources = root.Element("DataSources")?.Elements("DataSource").Select(ReadDataSource).ToArray()
                         ?? Array.Empty<DataSourceDefinition>();

        var variables = root.Element("Variables")?.Elements("Variable").Select(ReadVariable).ToArray()
                       ?? Array.Empty<ReportVariable>();

        var reportHeader = root.Element("ReportHeader") is { } rh ? ReadReportBand(rh, BandKind.ReportHeader) : null;
        var pageHeader = root.Element("PageHeader") is { } ph ? ReadReportBand(ph, BandKind.PageHeader) : null;
        var pageFooter = root.Element("PageFooter") is { } pf ? ReadReportBand(pf, BandKind.PageFooter) : null;
        var reportFooter = root.Element("ReportFooter") is { } rf ? ReadReportBand(rf, BandKind.ReportFooter) : null;

        var groups = root.Element("Groups")?.Elements("Group").Select(ReadGroup).ToArray()
                    ?? Array.Empty<GroupBand>();

        var detail = root.Element("Detail") is { } d ? ReadDetailBand(d) : DetailBand.Empty;

        var metadata = root.Element("Metadata")?.Elements("Entry")
            .ToDictionary(e => Attr(e, "Key") ?? string.Empty, e => Attr(e, "Value") ?? string.Empty)
            ?? new Dictionary<string, string>();

        return new ReportDefinition(name, pageSetup, detail)
        {
            SchemaVersion = version.ToString(),
            Parameters = new EquatableArray<ReportParameter>(parameters),
            DataSources = new EquatableArray<DataSourceDefinition>(dataSources),
            Variables = new EquatableArray<ReportVariable>(variables),
            ReportHeader = reportHeader,
            PageHeader = pageHeader,
            Groups = new EquatableArray<GroupBand>(groups),
            PageFooter = pageFooter,
            ReportFooter = reportFooter,
            Metadata = new EquatableDictionary<string, string>(metadata),
        };
    }

    // ── PageSetup ───────────────────────────────────────────────────────────────

    private static PageSetup? ReadPageSetup(XElement? element)
    {
        if (element is null)
        {
            return null;
        }
        var orientation = Enum.Parse<Orientation>(Attr(element, "Orientation") ?? nameof(Orientation.Portrait));
        var margins = Formats.ParseThickness(Attr(element, "Margins"));
        var columns = int.Parse(Attr(element, "Columns") ?? "1", Inv);
        var columnSpacing = Formats.ParseUnit(Attr(element, "ColumnSpacing"));
        var paperEl = element.Element("Paper")
            ?? throw new FormatException("PageSetup is missing <Paper>.");
        var paper = new PaperSize(
            Attr(paperEl, "Name") ?? "A4",
            Formats.ParseUnit(Attr(paperEl, "Width")),
            Formats.ParseUnit(Attr(paperEl, "Height")));
        return new PageSetup(paper, orientation, margins, columns, columnSpacing);
    }

    // ── Parameters / Variables / DataSources ────────────────────────────────────

    private static ReportParameter ReadParameter(XElement el)
    {
        var name = Attr(el, "Name") ?? throw new FormatException("Parameter missing Name.");
        var type = Formats.ParseType(Attr(el, "Type"));
        var prompt = Attr(el, "Prompt");
        var required = bool.Parse(Attr(el, "Required") ?? "true");
        var allowMultiple = bool.Parse(Attr(el, "AllowMultiple") ?? "false");
        object? defaultValue = null;
        if (el.Element("DefaultValue")?.Attribute("Value")?.Value is { } v)
        {
            defaultValue = Convert.ChangeType(v, type, Inv);
        }
        ParameterAvailableValues? available = null;
        if (el.Element("AvailableValues") is { } ave)
        {
            var values = ave.Elements("Value")
                .Select(ve => new ParameterValue(Attr(ve, "Value") ?? string.Empty, Attr(ve, "Label")))
                .ToArray();
            available = new ParameterAvailableValues
            {
                Values = new EquatableArray<ParameterValue>(values),
                DataSet = Attr(ave, "DataSet"),
                ValueField = Attr(ave, "ValueField"),
                LabelField = Attr(ave, "LabelField"),
            };
        }
        return new ReportParameter(name, type, prompt, defaultValue, allowMultiple, required, available);
    }

    private static ReportVariable ReadVariable(XElement el)
        => new(Attr(el, "Name") ?? "",
               Attr(el, "Expression") ?? "",
               Enum.Parse<VariableScope>(Attr(el, "Scope") ?? nameof(VariableScope.Row)));

    private static DataSourceDefinition ReadDataSource(XElement el)
    {
        var name = Attr(el, "Name") ?? throw new FormatException("DataSource missing Name.");
        var dataMember = Attr(el, "DataMember");
        var filterExpression = Attr(el, "FilterExpression");
        var fields = el.Element("Fields")?.Elements("Field").Select(f => new DataField(
            Attr(f, "Name") ?? "",
            Attr(f, "Type") is { } t ? Formats.ParseType(t) : null,
            Attr(f, "DisplayName"))).ToArray() ?? Array.Empty<DataField>();
        var calculatedFields = el.Element("CalculatedFields")?.Elements("CalculatedField").Select(c => new CalculatedField(
            Attr(c, "Name") ?? "",
            Attr(c, "Expression") ?? "",
            Attr(c, "Type") is { } t ? Formats.ParseType(t) : null)).ToArray() ?? Array.Empty<CalculatedField>();
        var relations = el.Element("Relations")?.Elements("Relation").Select(r => new DataRelation(
            Attr(r, "Name") ?? "",
            Attr(r, "ParentSource") ?? "",
            Attr(r, "ParentField") ?? "",
            Attr(r, "ChildSource") ?? "",
            Attr(r, "ChildField") ?? "")).ToArray() ?? Array.Empty<DataRelation>();
        var sorts = ReadSortExpressions(el.Element("SortExpressions"));
        var parameters = el.Element("Parameters")?.Elements("Parameter")
            .ToDictionary(p => Attr(p, "Key") ?? "", p => Attr(p, "Value") ?? "")
            ?? new Dictionary<string, string>();
        return new DataSourceDefinition(name, dataMember,
            new EquatableArray<DataField>(fields),
            new EquatableArray<DataRelation>(relations),
            new EquatableDictionary<string, string>(parameters),
            new EquatableArray<CalculatedField>(calculatedFields),
            filterExpression,
            sorts);
    }

    // ── Shared sort/filter readers ──────────────────────────────────────────────

    private static EquatableArray<SortDescriptor> ReadSortExpressions(XElement? parent)
    {
        if (parent is null) return EquatableArray<SortDescriptor>.Empty;
        var sorts = parent.Elements("Sort").Select(s => new SortDescriptor(
            Attr(s, "Expression") ?? "",
            Enum.Parse<SortDirection>(Attr(s, "Direction") ?? nameof(SortDirection.Ascending)))).ToArray();
        return new EquatableArray<SortDescriptor>(sorts);
    }

    // ── Bands ───────────────────────────────────────────────────────────────────

    private static ReportBand ReadReportBand(XElement el, BandKind kind)
    {
        var height = Formats.ParseUnit(Attr(el, "Height"));
        var elements = ReadElements(el.Element("Elements"));
        return new ReportBand(kind, height, elements,
            Visible: bool.Parse(Attr(el, "Visible") ?? "true"),
            VisibleExpression: Attr(el, "VisibleExpression"),
            PrintOnFirstPage: bool.Parse(Attr(el, "PrintOnFirstPage") ?? "true"),
            PrintOnLastPage: bool.Parse(Attr(el, "PrintOnLastPage") ?? "true"),
            PageBreak: ParsePageBreak(Attr(el, "PageBreak")));
    }

    private static DetailBand ReadDetailBand(XElement el)
    {
        var subs = el.Element("SubDetails")?.Elements("SubDetail").Select(ReadSubDetail).ToList()
                   ?? new List<SubDetailBand>();
        return new(Formats.ParseUnit(Attr(el, "Height")),
                   ReadElements(el.Element("Elements")),
                   Visible: bool.Parse(Attr(el, "Visible") ?? "true"),
                   VisibleExpression: Attr(el, "VisibleExpression"),
                   CanGrow: bool.Parse(Attr(el, "CanGrow") ?? "false"),
                   CanShrink: bool.Parse(Attr(el, "CanShrink") ?? "false"),
                   SubDetails: new EquatableArray<SubDetailBand>(subs),
                   NoRowsMessage: Attr(el, "NoRowsMessage"),
                   FilterExpression: Attr(el, "FilterExpression"),
                   SortExpressions: ReadSortExpressions(el.Element("SortExpressions")),
                   PageBreak: ParsePageBreak(Attr(el, "PageBreak")));
    }

    private static SubDetailBand ReadSubDetail(XElement el)
    {
        ReportBand? header = null;
        ReportBand? footer = null;
        if (el.Element("Header") is { } h)
        {
            header = new ReportBand(BandKind.Detail,
                Formats.ParseUnit(Attr(h, "Height")),
                ReadElements(h.Element("Elements")));
        }
        if (el.Element("Footer") is { } f)
        {
            footer = new ReportBand(BandKind.Detail,
                Formats.ParseUnit(Attr(f, "Height")),
                ReadElements(f.Element("Elements")));
        }
        return new SubDetailBand(
            Name: Attr(el, "Name") ?? "Sub",
            DataMember: Attr(el, "DataMember") ?? string.Empty,
            Height: Formats.ParseUnit(Attr(el, "Height")),
            Elements: ReadElements(el.Element("Elements")),
            Header: header,
            Footer: footer,
            Visible: bool.Parse(Attr(el, "Visible") ?? "true"),
            VisibleExpression: Attr(el, "VisibleExpression"),
            PrintIfEmpty: bool.Parse(Attr(el, "PrintIfEmpty") ?? "false"),
            NoRowsMessage: Attr(el, "NoRowsMessage"),
            FilterExpression: Attr(el, "FilterExpression"),
            SortExpressions: ReadSortExpressions(el.Element("SortExpressions")));
    }

    private static GroupBand ReadGroup(XElement el)
    {
        var header = el.Element("Header") is { } h ? ReadReportBand(h, BandKind.GroupHeader) : null;
        var footer = el.Element("Footer") is { } f ? ReadReportBand(f, BandKind.GroupFooter) : null;
        var groupVars = el.Element("Variables")?.Elements("Variable").Select(ReadVariable).ToArray()
                       ?? Array.Empty<ReportVariable>();
        return new GroupBand(
            Attr(el, "Name") ?? "",
            Attr(el, "GroupExpression") ?? "",
            header, footer,
            KeepTogether: bool.Parse(Attr(el, "KeepTogether") ?? "false"),
            NewPageBefore: bool.Parse(Attr(el, "NewPageBefore") ?? "false"),
            NewPageAfter: bool.Parse(Attr(el, "NewPageAfter") ?? "false"),
            RepeatHeaderOnNewPage: bool.Parse(Attr(el, "RepeatHeaderOnNewPage") ?? "false"),
            Visible: bool.Parse(Attr(el, "Visible") ?? "true"),
            VisibleExpression: Attr(el, "VisibleExpression"),
            PageBreak: ParsePageBreak(Attr(el, "PageBreak")),
            FilterExpression: Attr(el, "FilterExpression"),
            SortExpressions: ReadSortExpressions(el.Element("SortExpressions")),
            Variables: new EquatableArray<ReportVariable>(groupVars));
    }

    private static PageBreak ParsePageBreak(string? raw)
        => raw is null ? PageBreak.None : Enum.Parse<PageBreak>(raw);

    private static EquatableArray<ReportElement> ReadElements(XElement? parent)
    {
        if (parent is null)
        {
            return EquatableArray<ReportElement>.Empty;
        }
        var list = new List<ReportElement>();
        foreach (var el in parent.Elements())
        {
            list.Add(ReadElement(el));
        }
        return new EquatableArray<ReportElement>(list);
    }

    // ── Elements ────────────────────────────────────────────────────────────────

    private static ReportElement ReadElement(XElement el)
    {
        var id = Attr(el, "Id") ?? Guid.NewGuid().ToString("n");
        var name = Attr(el, "Name");
        var bounds = Formats.ParseRectangle(Attr(el, "Bounds"));
        var visible = bool.Parse(Attr(el, "Visible") ?? "true");
        var visibleExpression = Attr(el, "VisibleExpression");
        var style = ReadStyle(el.Element("Style"));
        var conditionalFormats = el.Element("ConditionalFormats")?.Elements("ConditionalFormat")
            .Select(cf => new ConditionalFormat(
                Attr(cf, "Condition") ?? "true",
                ReadStyle(cf.Element("Style"))))
            .ToArray() ?? Array.Empty<ConditionalFormat>();
        var propertyExpressions = el.Element("PropertyExpressions")?.Elements("PropertyExpression")
            .ToDictionary(x => Attr(x, "Path") ?? string.Empty, x => Attr(x, "Expression") ?? string.Empty)
            ?? new Dictionary<string, string>();
        // RDL-style extensions on the abstract base — optional attrs/child reflecting
        // the additions in ReportElement. Round-trips losslessly when not present.
        var bookmark = Attr(el, "Bookmark");
        var documentMapLabel = Attr(el, "DocumentMapLabel");
        var toggleItemId = Attr(el, "ToggleItemId");
        var initiallyHidden = bool.Parse(Attr(el, "InitiallyHidden") ?? "false");
        var action = ReadAction(el.Element("Action"));

        ReportElement element = el.Name.LocalName switch
        {
            "Label" => new LabelElement { Text = el.Element("Text")?.Value ?? string.Empty, Bounds = bounds },
            "TextBox" => ReadTextBoxElement(el, bounds),
            "Line" => ReadLineElement(el, bounds),
            "Rectangle" => ReadRectangleElement(el, bounds),
            "Ellipse" => new EllipseElement
            {
                Bounds = bounds,
                FillColor = ReadOptionalColor(el.Element("FillColor")),
            },
            "Image" => ReadImageElement(el, bounds),
            "Barcode" => new BarcodeElement
            {
                Bounds = bounds,
                Symbology = Enum.Parse<BarcodeSymbology>(el.Element("Symbology")?.Value ?? nameof(BarcodeSymbology.Code128)),
                Expression = el.Element("Expression")?.Value ?? string.Empty,
                ShowText = bool.Parse(el.Element("ShowText")?.Value ?? "true"),
                QrEcc = Enum.Parse<QrEccLevel>(el.Element("QrEcc")?.Value ?? nameof(QrEccLevel.Medium)),
            },
            "Chart" => ReadChartElement(el, bounds),
            "Subreport" => ReadSubreportElement(el, bounds),
            "Table" => ReadTableElement(el, bounds),
            // ── RDL F2 advanced elements (scaffold) ──────────────────────────────
            "Tablix"    => ReadTablixElement(el, bounds),
            "Code"      => ReadCodeElement(el, bounds),
            "Map"       => ReadMapElement(el, bounds),
            "Gauge"     => ReadGaugeElement(el, bounds),
            "DataBar"   => ReadDataBarElement(el, bounds),
            "Sparkline" => ReadSparklineElement(el, bounds),
            "Indicator" => ReadIndicatorElement(el, bounds),
            // Convention-based fallback: a tag with no explicit arm resolves to a registered all-scalar type.
            _ => ElementSerializationRegistry.TryGetType(el.Name.LocalName, out var genType)
                ? ElementSerializationRegistry.ReadXml(genType, bounds, el)
                : throw new FormatException($"Unknown element tag: <{el.Name.LocalName}>"),
        };

        // The init-only Id/Visible/Style/etc. on the abstract base must be applied via 'with'.
        return element with
        {
            Id = id,
            Name = name,
            Visible = visible,
            VisibleExpression = visibleExpression,
            Style = style,
            ConditionalFormats = new EquatableArray<ConditionalFormat>(conditionalFormats),
            PropertyExpressions = new EquatableDictionary<string, string>(propertyExpressions),
            Bookmark = bookmark,
            DocumentMapLabel = documentMapLabel,
            ToggleItemId = toggleItemId,
            InitiallyHidden = initiallyHidden,
            Action = action,
        };
    }

    private static ElementAction? ReadAction(XElement? el)
    {
        if (el is null) return null;
        var kind = Enum.Parse<ActionKind>(Attr(el, "Kind") ?? nameof(ActionKind.Hyperlink));
        var hyperlink = Attr(el, "Hyperlink");
        var bookmarkId = Attr(el, "BookmarkId");
        var drillthroughReport = Attr(el, "DrillthroughReportName");
        var parameters = el.Element("Parameters")?.Elements("Parameter").Select(p => new DrillthroughParameter(
            Attr(p, "Name") ?? "",
            Attr(p, "Value") ?? "",
            bool.Parse(Attr(p, "Omit") ?? "false"))).ToArray() ?? Array.Empty<DrillthroughParameter>();
        return new ElementAction(kind, hyperlink, bookmarkId, drillthroughReport,
            new EquatableArray<DrillthroughParameter>(parameters));
    }

    private static LineElement ReadLineElement(XElement el, Rectangle bounds)
    {
        var direction = Enum.Parse<LineDirection>(el.Element("Direction")?.Value ?? nameof(LineDirection.Horizontal));
        var penEl = el.Element("Pen");
        BorderSide pen = penEl is null
            ? new BorderSide(BorderLineStyle.Solid, Unit.FromPoint(0.5), Color.Black)
            : new BorderSide(
                Enum.Parse<BorderLineStyle>(Attr(penEl, "Style") ?? nameof(BorderLineStyle.Solid)),
                Formats.ParseUnit(Attr(penEl, "Thickness")),
                Formats.ParseColor(Attr(penEl, "Color")));
        return new LineElement { Bounds = bounds, Direction = direction, Pen = pen };
    }

    private static RectangleElement ReadRectangleElement(XElement el, Rectangle bounds)
        => new()
        {
            Bounds = bounds,
            FillColor = ReadOptionalColor(el.Element("FillColor")),
            CornerRadius = Formats.ParseUnit(el.Element("CornerRadius")?.Value),
        };

    private static ImageElement ReadImageElement(XElement el, Rectangle bounds)
    {
        var source = Enum.Parse<ImageSourceKind>(el.Element("Source")?.Value ?? nameof(ImageSourceKind.Path));
        var sizing = Enum.Parse<ImageSizing>(el.Element("Sizing")?.Value ?? nameof(ImageSizing.Fit));
        var path = el.Element("Path")?.Value;
        var expression = el.Element("Expression")?.Value;
        var inlineRaw = el.Element("InlineData")?.Value;
        var inline = string.IsNullOrEmpty(inlineRaw)
            ? EquatableArray<byte>.Empty
            : new EquatableArray<byte>(Convert.FromBase64String(inlineRaw));
        return new ImageElement
        {
            Bounds = bounds,
            Source = source,
            Sizing = sizing,
            Path = path,
            Expression = expression,
            InlineData = inline,
        };
    }

    private static ChartElement ReadChartElement(XElement el, Rectangle bounds)
    {
        var series = el.Element("Series")?.Elements("Series").Select(s =>
        {
            var color = Attr(s, "Color") is { } c ? Formats.ParseColor(c) : (Color?)null;
            return new ChartSeries(
                Attr(s, "Name") ?? "",
                Attr(s, "CategoryExpression") ?? "",
                Attr(s, "ValueExpression") ?? "",
                color,
                Attr(s, "SizeExpression"),
                Attr(s, "HighExpression"),
                Attr(s, "LowExpression"));
        }).ToArray() ?? Array.Empty<ChartSeries>();
        return new ChartElement
        {
            Bounds = bounds,
            Kind = Enum.Parse<ChartKind>(el.Element("Kind")?.Value ?? nameof(ChartKind.Bar)),
            Title = el.Element("Title")?.Value,
            ShowLegend = bool.Parse(el.Element("ShowLegend")?.Value ?? "true"),
            Series = new EquatableArray<ChartSeries>(series),
        };
    }

    private static SubreportElement ReadSubreportElement(XElement el, Rectangle bounds)
    {
        ReportDefinition? inline = null;
        if (el.Element("InlineDefinition")?.Element("Report") is { } innerReport)
        {
            inline = ReadDefinition(innerReport, SchemaVersion.Parse(Attr(innerReport, "SchemaVersion") ?? "1.0"));
        }
        var bindings = el.Element("ParameterBindings")?.Elements("Binding")
            .ToDictionary(b => Attr(b, "Key") ?? "", b => Attr(b, "Value") ?? "")
            ?? new Dictionary<string, string>();
        return new SubreportElement
        {
            Bounds = bounds,
            ReportId = el.Element("ReportId")?.Value,
            InlineDefinition = inline,
            ParameterBindings = new EquatableDictionary<string, string>(bindings),
            DataExpression = el.Element("DataExpression")?.Value,
        };
    }

    private static TableElement ReadTableElement(XElement el, Rectangle bounds)
    {
        var columns = el.Element("Columns")?.Elements("Column").Select(c => new TableColumn(
            Attr(c, "Name") ?? "",
            Formats.ParseUnit(Attr(c, "Width")),
            Attr(c, "HeaderText"),
            Attr(c, "DetailExpression"),
            Attr(c, "FooterExpression"))).ToArray() ?? Array.Empty<TableColumn>();
        return new TableElement
        {
            Bounds = bounds,
            HeaderHeight = Formats.ParseUnit(el.Element("HeaderHeight")?.Value),
            DetailHeight = Formats.ParseUnit(el.Element("DetailHeight")?.Value),
            FooterHeight = Formats.ParseUnit(el.Element("FooterHeight")?.Value),
            Columns = new EquatableArray<TableColumn>(columns),
            DataExpression = el.Element("DataExpression")?.Value,
        };
    }

    // ── RDL F1.8 TextRun: rich text inside a TextBox ─────────────────────────────

    private static TextBoxElement ReadTextBoxElement(XElement el, Rectangle bounds)
    {
        var runs = el.Element("TextRuns")?.Elements("TextRun").Select(ReadTextRun).ToArray()
                   ?? Array.Empty<TextRun>();
        return new TextBoxElement
        {
            Expression = el.Element("Expression")?.Value ?? string.Empty,
            Bounds = bounds,
            CanGrow = bool.Parse(Attr(el, "CanGrow") ?? "false"),
            CanShrink = bool.Parse(Attr(el, "CanShrink") ?? "false"),
            TextRuns = new EquatableArray<TextRun>(runs),
        };
    }

    private static TextRun ReadTextRun(XElement r)
    {
        var value = r.Element("Value")?.Value ?? string.Empty;
        var style = r.Element("Style") is { } sty ? ReadStyle(sty) : (Style?)null;
        var action = ReadAction(r.Element("Action"));
        return new TextRun(value, style, action);
    }

    // ── RDL F2 advanced elements — round-trip readers ──────────────────────────

    private static TablixElement ReadTablixElement(XElement el, Rectangle bounds)
    {
        var rowGroups = el.Element("RowGroups")?.Elements("Group").Select(ReadTablixGroup).ToArray()
                         ?? Array.Empty<TablixGroup>();
        var colGroups = el.Element("ColumnGroups")?.Elements("Group").Select(ReadTablixGroup).ToArray()
                         ?? Array.Empty<TablixGroup>();
        var cells = el.Element("Cells")?.Elements("Cell").Select(c =>
        {
            var content = c.Element("Content")?.Elements().FirstOrDefault();
            return new TablixCell(
                int.Parse(Attr(c, "RowIndex") ?? "0", Inv),
                int.Parse(Attr(c, "ColumnIndex") ?? "0", Inv),
                content is null ? null : ReadElement(content));
        }).ToArray() ?? Array.Empty<TablixCell>();
        var columnWidths = el.Element("ColumnWidths")?.Elements("W")
            .Select(wt => double.TryParse(wt.Value, System.Globalization.NumberStyles.Any, Inv, out var d) ? d : 0d)
            .ToArray() ?? Array.Empty<double>();
        return new TablixElement
        {
            Bounds = bounds,
            DataSetName = el.Element("DataSetName")?.Value,
            RowGroups = new EquatableArray<TablixGroup>(rowGroups),
            ColumnGroups = new EquatableArray<TablixGroup>(colGroups),
            Cells = new EquatableArray<TablixCell>(cells),
            ColumnWidths = new EquatableArray<double>(columnWidths),
            RowSubtotals = string.Equals(el.Element("RowSubtotals")?.Value, "true", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static TablixGroup ReadTablixGroup(XElement g) => new(
        Name: Attr(g, "Name") ?? "",
        GroupExpression: Attr(g, "GroupExpression"),
        SortExpression: Attr(g, "SortExpression"),
        SortDescending: bool.Parse(Attr(g, "SortDescending") ?? "false"));

    private static CodeElement ReadCodeElement(XElement el, Rectangle bounds) => new()
    {
        Bounds = bounds,
        Source = el.Element("Source")?.Value ?? string.Empty,
        Language = Enum.Parse<CodeLanguage>(el.Element("Language")?.Value ?? nameof(CodeLanguage.CSharp)),
    };

    private static MapElement ReadMapElement(XElement el, Rectangle bounds) => new()
    {
        Bounds = bounds,
        Basemap = el.Element("Basemap")?.Value,
        DataSetName = el.Element("DataSetName")?.Value,
        LatitudeExpression = el.Element("Latitude")?.Value,
        LongitudeExpression = el.Element("Longitude")?.Value,
        ShapeSet = el.Element("ShapeSet")?.Value,
        ShapesGeoJson = el.Element("ShapesGeoJson")?.Value,
        ShowGraticule = bool.Parse(el.Element("ShowGraticule")?.Value ?? "false"),
        ShapeFill = el.Element("ShapeFill")?.Value ?? "#E8EDE4",
        ShapeStroke = el.Element("ShapeStroke")?.Value ?? "#9CA3AF",
    };

    private static GaugeElement ReadGaugeElement(XElement el, Rectangle bounds)
    {
        var ranges = el.Element("Ranges")?.Elements("Range").Select(r => new GaugeRange(
            Attr(r, "Start") ?? "0",
            Attr(r, "End") ?? "100",
            Attr(r, "Color") ?? "#000000")).ToArray() ?? Array.Empty<GaugeRange>();
        return new GaugeElement
        {
            Bounds = bounds,
            Kind = Enum.Parse<GaugeKind>(el.Element("Kind")?.Value ?? nameof(GaugeKind.Radial)),
            MinimumExpression = el.Element("Minimum")?.Value ?? "0",
            MaximumExpression = el.Element("Maximum")?.Value ?? "100",
            ValueExpression = el.Element("Value")?.Value ?? "0",
            Ranges = new EquatableArray<GaugeRange>(ranges),
        };
    }

    private static DataBarElement ReadDataBarElement(XElement el, Rectangle bounds) => new()
    {
        Bounds = bounds,
        ValueExpression = el.Element("Value")?.Value ?? "0",
        MinimumExpression = el.Element("Minimum")?.Value ?? "0",
        MaximumExpression = el.Element("Maximum")?.Value ?? "100",
        FillColor = el.Element("FillColor")?.Value ?? "#C2410C",
    };

    private static SparklineElement ReadSparklineElement(XElement el, Rectangle bounds) => new()
    {
        Bounds = bounds,
        Kind = Enum.Parse<SparklineKind>(el.Element("Kind")?.Value ?? nameof(SparklineKind.Line)),
        DataSetName = el.Element("DataSetName")?.Value,
        ValueExpression = el.Element("Value")?.Value ?? "Fields.Value",
        CategoryExpression = el.Element("Category")?.Value,
    };

    private static IndicatorElement ReadIndicatorElement(XElement el, Rectangle bounds)
    {
        var states = el.Element("States")?.Elements("State").Select(s => new IndicatorState(
            Attr(s, "Start") ?? "0",
            Attr(s, "End") ?? "100",
            Attr(s, "Icon") ?? "circle")).ToArray() ?? Array.Empty<IndicatorState>();
        return new IndicatorElement
        {
            Bounds = bounds,
            Kind = Enum.Parse<IndicatorKind>(el.Element("Kind")?.Value ?? nameof(IndicatorKind.DirectionalArrow)),
            ValueExpression = el.Element("Value")?.Value ?? "0",
            States = new EquatableArray<IndicatorState>(states),
        };
    }

    // ── Styling ─────────────────────────────────────────────────────────────────

    private static Style ReadStyle(XElement? el)
    {
        if (el is null)
        {
            return Style.Default;
        }
        var horizontal = Enum.Parse<HorizontalAlignment>(Attr(el, "HorizontalAlignment") ?? nameof(HorizontalAlignment.Left));
        var vertical = Enum.Parse<VerticalAlignment>(Attr(el, "VerticalAlignment") ?? nameof(VerticalAlignment.Top));
        var wordWrap = bool.Parse(Attr(el, "WordWrap") ?? "true");
        var format = Attr(el, "Format");
        var font = el.Element("Font") is { } fontEl ? ReadFont(fontEl) : null;
        var foreColor = ReadOptionalColor(el.Element("ForeColor"));
        var backColor = ReadOptionalColor(el.Element("BackColor"));
        var padding = el.Element("Padding")?.Value is { } p && !string.IsNullOrEmpty(p)
            ? Formats.ParseThickness(p)
            : (Thickness?)null;
        var border = el.Element("Border") is { } borderEl ? ReadBorder(borderEl) : null;
        return new Style(font, foreColor, backColor, border, padding, horizontal, vertical, wordWrap, format);
    }

    private static Font ReadFont(XElement el)
        => new(Attr(el, "Family") ?? "Arial",
               double.Parse(Attr(el, "Size") ?? "10", Inv),
               Enum.Parse<FontStyle>(Attr(el, "Style") ?? nameof(FontStyle.Regular)));

    private static Border ReadBorder(XElement el)
    {
        BorderSide ReadOne(string position) =>
            el.Elements("Side")
                .FirstOrDefault(s => Attr(s, "Position") == position) is { } side
                ? new BorderSide(
                    Enum.Parse<BorderLineStyle>(Attr(side, "Style") ?? nameof(BorderLineStyle.None)),
                    Formats.ParseUnit(Attr(side, "Thickness")),
                    Formats.ParseColor(Attr(side, "Color")))
                : BorderSide.None;
        return new Border(ReadOne("Left"), ReadOne("Top"), ReadOne("Right"), ReadOne("Bottom"));
    }

    private static Color? ReadOptionalColor(XElement? el)
        => el is null || string.IsNullOrWhiteSpace(el.Value) ? null : Formats.ParseColor(el.Value);

    private static string? Attr(XElement el, string name) => el.Attribute(name)?.Value;
}
