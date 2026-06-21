using System.Globalization;
using System.Text.Json.Nodes;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

internal static class RepJsonReader
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static ReportDefinition Read(JsonObject root, out SchemaVersion version)
    {
        version = SchemaVersion.Parse((string?)root["schemaVersion"] ?? "1.0");
        return ReadDefinition(root, version);
    }

    private static ReportDefinition ReadDefinition(JsonObject root, SchemaVersion version)
    {
        var name = (string?)root["name"] ?? "Untitled";
        var pageSetup = ReadPageSetup(root["pageSetup"]?.AsObject()) ?? PageSetup.A4Portrait;

        var parameters = ReadArray(root, "parameters", n => ReadParameter(n.AsObject()));
        var dataSources = ReadArray(root, "dataSources", n => ReadDataSource(n.AsObject()));
        var variables = ReadArray(root, "variables", n => ReadVariable(n.AsObject()));
        var groups = ReadArray(root, "groups", n => ReadGroup(n.AsObject()));

        var reportHeader = root["reportHeader"] is JsonObject rh ? ReadReportBand(rh, BandKind.ReportHeader) : null;
        var pageHeader = root["pageHeader"] is JsonObject ph ? ReadReportBand(ph, BandKind.PageHeader) : null;
        var pageFooter = root["pageFooter"] is JsonObject pf ? ReadReportBand(pf, BandKind.PageFooter) : null;
        var reportFooter = root["reportFooter"] is JsonObject rf ? ReadReportBand(rf, BandKind.ReportFooter) : null;

        var detail = root["detail"] is JsonObject d ? ReadDetailBand(d) : DetailBand.Empty;
        var metadata = new Dictionary<string, string>();
        if (root["metadata"] is JsonObject meta)
        {
            foreach (var kv in meta)
            {
                metadata[kv.Key] = (string?)kv.Value ?? string.Empty;
            }
        }

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

    private static T[] ReadArray<T>(JsonObject root, string name, Func<JsonNode, T> map)
    {
        if (root[name] is not JsonArray arr)
        {
            return Array.Empty<T>();
        }
        var list = new List<T>(arr.Count);
        foreach (var node in arr)
        {
            if (node is null)
            {
                continue;
            }
            list.Add(map(node));
        }
        return [.. list];
    }

    private static PageSetup? ReadPageSetup(JsonObject? o)
    {
        if (o is null)
        {
            return null;
        }
        var paperObj = o["paper"]?.AsObject()
            ?? throw new FormatException("PageSetup missing 'paper'.");
        var paper = new PaperSize(
            (string?)paperObj["name"] ?? "A4",
            Formats.ParseUnit((string?)paperObj["width"]),
            Formats.ParseUnit((string?)paperObj["height"]));
        var orientation = Enum.Parse<Orientation>((string?)o["orientation"] ?? nameof(Orientation.Portrait));
        var margins = Formats.ParseThickness((string?)o["margins"]);
        var columns = (int?)o["columns"] ?? 1;
        var columnSpacing = Formats.ParseUnit((string?)o["columnSpacing"]);
        return new PageSetup(paper, orientation, margins, columns, columnSpacing);
    }

    private static ReportParameter ReadParameter(JsonObject o)
    {
        var name = (string?)o["name"] ?? throw new FormatException("Parameter missing 'name'.");
        var type = Formats.ParseType((string?)o["type"]);
        var prompt = (string?)o["prompt"];
        var required = (bool?)o["required"] ?? true;
        var allowMultiple = (bool?)o["allowMultiple"] ?? false;
        object? defaultValue = (string?)o["defaultValue"] is { } dv
            ? Convert.ChangeType(dv, type, Inv)
            : null;
        ParameterAvailableValues? available = null;
        if (o["availableValues"] is JsonObject avo)
        {
            var values = avo["values"] is JsonArray va
                ? va.Where(n => n is JsonObject).Select(n => new ParameterValue(
                    (string?)n!["value"] ?? string.Empty, (string?)n["label"])).ToArray()
                : Array.Empty<ParameterValue>();
            available = new ParameterAvailableValues
            {
                Values = new EquatableArray<ParameterValue>(values),
                DataSet = (string?)avo["dataSet"],
                ValueField = (string?)avo["valueField"],
                LabelField = (string?)avo["labelField"],
            };
        }
        return new ReportParameter(name, type, prompt, defaultValue, allowMultiple, required, available,
            Nullable: (bool?)o["nullable"] ?? false,
            AllowBlank: (bool?)o["allowBlank"] ?? false,
            Hidden: (bool?)o["hidden"] ?? false);
    }

    private static ReportVariable ReadVariable(JsonObject o)
        => new((string?)o["name"] ?? "",
               (string?)o["expression"] ?? "",
               Enum.Parse<VariableScope>((string?)o["scope"] ?? nameof(VariableScope.Row)));

    // ── Shared sort / calculated-field helpers (mirror RepJsonWriter) ────────────

    private static EquatableArray<SortDescriptor> ReadSorts(JsonObject o)
        => new(ReadArray(o, "sortExpressions", n =>
        {
            var so = n.AsObject();
            return new SortDescriptor(
                (string?)so["expression"] ?? "",
                Enum.Parse<SortDirection>((string?)so["direction"] ?? nameof(SortDirection.Ascending)));
        }));

    private static CalculatedField ReadCalculatedField(JsonObject o)
        => new((string?)o["name"] ?? "",
               (string?)o["expression"] ?? "",
               (string?)o["type"] is { } t ? Formats.ParseType(t) : null);

    private static DataSourceDefinition ReadDataSource(JsonObject o)
    {
        var fields = ReadArray(o, "fields", n =>
        {
            var fo = n.AsObject();
            return new DataField(
                (string?)fo["name"] ?? "",
                (string?)fo["type"] is { } t ? Formats.ParseType(t) : null,
                (string?)fo["displayName"]);
        });
        var relations = ReadArray(o, "relations", n =>
        {
            var ro = n.AsObject();
            return new DataRelation(
                (string?)ro["name"] ?? "",
                (string?)ro["parentSource"] ?? "",
                (string?)ro["parentField"] ?? "",
                (string?)ro["childSource"] ?? "",
                (string?)ro["childField"] ?? "");
        });
        var parameters = new Dictionary<string, string>();
        if (o["parameters"] is JsonObject po)
        {
            foreach (var kv in po)
            {
                parameters[kv.Key] = (string?)kv.Value ?? string.Empty;
            }
        }
        return new DataSourceDefinition(
            (string?)o["name"] ?? "",
            (string?)o["dataMember"],
            new EquatableArray<DataField>(fields),
            new EquatableArray<DataRelation>(relations),
            new EquatableDictionary<string, string>(parameters),
            CalculatedFields: new EquatableArray<CalculatedField>(
                ReadArray(o, "calculatedFields", n => ReadCalculatedField(n.AsObject()))),
            FilterExpression: (string?)o["filterExpression"],
            SortExpressions: ReadSorts(o));
    }

    // ── Bands ───────────────────────────────────────────────────────────────────

    private static ReportBand ReadReportBand(JsonObject o, BandKind kind)
        => new(kind,
               Formats.ParseUnit((string?)o["height"]),
               ReadElements(o["elements"]),
               Visible: (bool?)o["visible"] ?? true,
               VisibleExpression: (string?)o["visibleExpression"],
               PrintOnFirstPage: (bool?)o["printOnFirstPage"] ?? true,
               PrintOnLastPage: (bool?)o["printOnLastPage"] ?? true,
               PageBreak: Enum.Parse<PageBreak>((string?)o["pageBreak"] ?? nameof(PageBreak.None)));

    private static DetailBand ReadDetailBand(JsonObject o)
        => new(Formats.ParseUnit((string?)o["height"]),
               ReadElements(o["elements"]),
               Visible: (bool?)o["visible"] ?? true,
               VisibleExpression: (string?)o["visibleExpression"],
               CanGrow: (bool?)o["canGrow"] ?? false,
               CanShrink: (bool?)o["canShrink"] ?? false,
               SubDetails: new EquatableArray<SubDetailBand>(
                   ReadArray(o, "subDetails", n => ReadSubDetail(n.AsObject()))),
               NoRowsMessage: (string?)o["noRowsMessage"],
               FilterExpression: (string?)o["filterExpression"],
               SortExpressions: ReadSorts(o),
               PageBreak: Enum.Parse<PageBreak>((string?)o["pageBreak"] ?? nameof(PageBreak.None)));

    private static SubDetailBand ReadSubDetail(JsonObject o)
        => new(Name: (string?)o["name"] ?? "",
               DataMember: (string?)o["dataMember"] ?? "",
               Height: Formats.ParseUnit((string?)o["height"]),
               Elements: ReadElements(o["elements"]),
               Header: o["header"] is JsonObject h ? ReadReportBand(h, BandKind.GroupHeader) : null,
               Footer: o["footer"] is JsonObject f ? ReadReportBand(f, BandKind.GroupFooter) : null,
               Visible: (bool?)o["visible"] ?? true,
               VisibleExpression: (string?)o["visibleExpression"],
               PrintIfEmpty: (bool?)o["printIfEmpty"] ?? false,
               NoRowsMessage: (string?)o["noRowsMessage"],
               FilterExpression: (string?)o["filterExpression"],
               SortExpressions: ReadSorts(o));

    private static GroupBand ReadGroup(JsonObject o)
        => new((string?)o["name"] ?? "",
               (string?)o["groupExpression"] ?? "",
               o["header"] is JsonObject h ? ReadReportBand(h, BandKind.GroupHeader) : null,
               o["footer"] is JsonObject f ? ReadReportBand(f, BandKind.GroupFooter) : null,
               KeepTogether: (bool?)o["keepTogether"] ?? false,
               NewPageBefore: (bool?)o["newPageBefore"] ?? false,
               NewPageAfter: (bool?)o["newPageAfter"] ?? false,
               RepeatHeaderOnNewPage: (bool?)o["repeatHeaderOnNewPage"] ?? false,
               Visible: (bool?)o["visible"] ?? true,
               VisibleExpression: (string?)o["visibleExpression"],
               PageBreak: Enum.Parse<PageBreak>((string?)o["pageBreak"] ?? nameof(PageBreak.None)),
               FilterExpression: (string?)o["filterExpression"],
               SortExpressions: ReadSorts(o),
               Variables: new EquatableArray<ReportVariable>(
                   ReadArray(o, "variables", n => ReadVariable(n.AsObject()))));

    private static EquatableArray<ReportElement> ReadElements(JsonNode? node)
    {
        if (node is not JsonArray arr)
        {
            return EquatableArray<ReportElement>.Empty;
        }
        var list = new List<ReportElement>(arr.Count);
        foreach (var item in arr)
        {
            if (item is JsonObject obj)
            {
                list.Add(ReadElement(obj));
            }
        }
        return new EquatableArray<ReportElement>(list);
    }

    // ── Elements ────────────────────────────────────────────────────────────────

    private static ReportElement ReadElement(JsonObject o)
    {
        var kind = (string?)o["kind"] ?? throw new FormatException("Element missing 'kind'.");
        var id = (string?)o["id"] ?? Guid.NewGuid().ToString("n");
        var name = (string?)o["name"];
        var bounds = Formats.ParseRectangle((string?)o["bounds"]);
        var visible = (bool?)o["visible"] ?? true;
        var visibleExpression = (string?)o["visibleExpression"];
        var style = ReadStyle(o["style"]?.AsObject());
        var conditionalFormats = ReadArray(o, "conditionalFormats", n =>
        {
            var co = n.AsObject();
            return new ConditionalFormat((string?)co["condition"] ?? "true", ReadStyle(co["style"]?.AsObject()));
        });
        var propertyExpressions = new Dictionary<string, string>();
        if (o["propertyExpressions"] is JsonObject peObj)
        {
            foreach (var kv in peObj)
            {
                propertyExpressions[kv.Key] = (string?)kv.Value ?? string.Empty;
            }
        }
        // RDL-style extensions on the abstract base — optional; round-trip losslessly when absent.
        var bookmark = (string?)o["bookmark"];
        var documentMapLabel = (string?)o["documentMapLabel"];
        var toggleItemId = (string?)o["toggleItemId"];
        var initiallyHidden = (bool?)o["initiallyHidden"] ?? false;
        var action = o["action"] is JsonObject actObj ? ReadAction(actObj) : null;

        ReportElement element = kind switch
        {
            "Label" => new LabelElement { Text = (string?)o["text"] ?? "", Bounds = bounds },
            "TextBox" => new TextBoxElement
            {
                Expression = (string?)o["expression"] ?? "",
                Bounds = bounds,
                CanGrow = (bool?)o["canGrow"] ?? false,
                CanShrink = (bool?)o["canShrink"] ?? false,
                TextRuns = ReadTextRuns(o),
            },
            "Line" => ReadLine(o, bounds),
            "Rectangle" => new RectangleElement
            {
                Bounds = bounds,
                FillColor = (string?)o["fillColor"] is { } fc ? Formats.ParseColor(fc) : null,
                CornerRadius = Formats.ParseUnit((string?)o["cornerRadius"]),
            },
            "Ellipse" => new EllipseElement
            {
                Bounds = bounds,
                FillColor = (string?)o["fillColor"] is { } fc2 ? Formats.ParseColor(fc2) : null,
            },
            "Image" => ReadImage(o, bounds),
            "Barcode" => new BarcodeElement
            {
                Bounds = bounds,
                Symbology = Enum.Parse<BarcodeSymbology>((string?)o["symbology"] ?? nameof(BarcodeSymbology.Code128)),
                Expression = (string?)o["expression"] ?? "",
                ShowText = (bool?)o["showText"] ?? true,
                QrEcc = Enum.Parse<QrEccLevel>((string?)o["qrEcc"] ?? nameof(QrEccLevel.Medium)),
            },
            "Chart" => ReadChart(o, bounds),
            "Subreport" => ReadSubreport(o, bounds),
            "Table" => ReadTable(o, bounds),
            // ── RDL F2 advanced elements ─────────────────────────────────────────
            "Tablix" => ReadTablix(o, bounds),
            "Code" => new CodeElement
            {
                Bounds = bounds,
                Source = (string?)o["source"] ?? "",
                Language = Enum.Parse<CodeLanguage>((string?)o["language"] ?? nameof(CodeLanguage.CSharp)),
            },
            "Map" => new MapElement
            {
                Bounds = bounds,
                Basemap = (string?)o["basemap"],
                DataSetName = (string?)o["dataSetName"],
                LatitudeExpression = (string?)o["latitude"],
                LongitudeExpression = (string?)o["longitude"],
                ShapeSet = (string?)o["shapeSet"],
                ShapesGeoJson = (string?)o["shapesGeoJson"],
                ShowGraticule = (bool?)o["showGraticule"] ?? false,
                ShapeFill = (string?)o["shapeFill"] ?? "#E8EDE4",
                ShapeStroke = (string?)o["shapeStroke"] ?? "#9CA3AF",
            },
            "Gauge" => ReadGauge(o, bounds),
            "DataBar" => new DataBarElement
            {
                Bounds = bounds,
                ValueExpression = (string?)o["value"] ?? "0",
                MinimumExpression = (string?)o["minimum"] ?? "0",
                MaximumExpression = (string?)o["maximum"] ?? "100",
                FillColor = (string?)o["fillColor"] ?? "#C2410C",
            },
            "Sparkline" => new SparklineElement
            {
                Bounds = bounds,
                Kind = Enum.Parse<SparklineKind>((string?)o["sparklineKind"] ?? nameof(SparklineKind.Line)),
                ValueExpression = (string?)o["value"] ?? "Fields.Value",
                DataSetName = (string?)o["dataSetName"],
                CategoryExpression = (string?)o["category"],
            },
            "Indicator" => ReadIndicator(o, bounds),
            // Convention-based fallback: a kind with no explicit arm resolves to a registered all-scalar type.
            _ => ElementSerializationRegistry.TryGetType(kind, out var genType)
                ? ElementSerializationRegistry.ReadJson(genType, bounds, o)
                : throw new FormatException($"Unknown element kind: '{kind}'."),
        };

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

    private static ElementAction? ReadAction(JsonObject o)
    {
        var parameters = ReadArray(o, "parameters", n =>
        {
            var po = n.AsObject();
            return new DrillthroughParameter(
                (string?)po["name"] ?? "",
                (string?)po["value"] ?? "",
                (bool?)po["omit"] ?? false);
        });
        return new ElementAction(
            Enum.Parse<ActionKind>((string?)o["kind"] ?? nameof(ActionKind.Hyperlink)),
            (string?)o["hyperlink"],
            (string?)o["bookmarkId"],
            (string?)o["drillthroughReportName"],
            new EquatableArray<DrillthroughParameter>(parameters));
    }

    private static EquatableArray<TextRun> ReadTextRuns(JsonObject o)
    {
        var runs = ReadArray(o, "textRuns", n =>
        {
            var ro = n.AsObject();
            var style = ro["style"] is JsonObject so ? ReadStyle(so) : (Style?)null;
            var act = ro["action"] is JsonObject ao ? ReadAction(ao) : null;
            return new TextRun((string?)ro["value"] ?? "", style, act);
        });
        return new EquatableArray<TextRun>(runs);
    }

    private static LineElement ReadLine(JsonObject o, Rectangle bounds)
    {
        var direction = Enum.Parse<LineDirection>((string?)o["direction"] ?? nameof(LineDirection.Horizontal));
        var penObj = o["pen"]?.AsObject();
        BorderSide pen = penObj is null
            ? new BorderSide(BorderLineStyle.Solid, Unit.FromPoint(0.5), Color.Black)
            : new BorderSide(
                Enum.Parse<BorderLineStyle>((string?)penObj["style"] ?? nameof(BorderLineStyle.Solid)),
                Formats.ParseUnit((string?)penObj["thickness"]),
                Formats.ParseColor((string?)penObj["color"]));
        return new LineElement { Bounds = bounds, Direction = direction, Pen = pen };
    }

    private static ImageElement ReadImage(JsonObject o, Rectangle bounds)
    {
        var inline = (string?)o["inlineData"];
        return new ImageElement
        {
            Bounds = bounds,
            Source = Enum.Parse<ImageSourceKind>((string?)o["source"] ?? nameof(ImageSourceKind.Path)),
            Sizing = Enum.Parse<ImageSizing>((string?)o["sizing"] ?? nameof(ImageSizing.Fit)),
            Path = (string?)o["path"],
            Expression = (string?)o["expression"],
            InlineData = string.IsNullOrEmpty(inline)
                ? EquatableArray<byte>.Empty
                : new EquatableArray<byte>(Convert.FromBase64String(inline)),
        };
    }

    private static ChartElement ReadChart(JsonObject o, Rectangle bounds)
    {
        var series = ReadArray(o, "series", n =>
        {
            var so = n.AsObject();
            return new ChartSeries(
                (string?)so["name"] ?? "",
                (string?)so["categoryExpression"] ?? "",
                (string?)so["valueExpression"] ?? "",
                (string?)so["color"] is { } c ? Formats.ParseColor(c) : null,
                (string?)so["sizeExpression"],
                (string?)so["highExpression"],
                (string?)so["lowExpression"]);
        });
        return new ChartElement
        {
            Bounds = bounds,
            Kind = Enum.Parse<ChartKind>((string?)o["chartKind"] ?? nameof(ChartKind.Bar)),
            Title = (string?)o["title"],
            ShowLegend = (bool?)o["showLegend"] ?? true,
            Series = new EquatableArray<ChartSeries>(series),
        };
    }

    private static SubreportElement ReadSubreport(JsonObject o, Rectangle bounds)
    {
        ReportDefinition? inline = null;
        if (o["inlineDefinition"] is JsonObject inner)
        {
            inline = ReadDefinition(inner, SchemaVersion.Parse((string?)inner["schemaVersion"] ?? "1.0"));
        }
        var bindings = new Dictionary<string, string>();
        if (o["parameterBindings"] is JsonObject pb)
        {
            foreach (var kv in pb)
            {
                bindings[kv.Key] = (string?)kv.Value ?? string.Empty;
            }
        }
        return new SubreportElement
        {
            Bounds = bounds,
            ReportId = (string?)o["reportId"],
            InlineDefinition = inline,
            ParameterBindings = new EquatableDictionary<string, string>(bindings),
            DataExpression = (string?)o["dataExpression"],
        };
    }

    private static TableElement ReadTable(JsonObject o, Rectangle bounds)
    {
        var columns = ReadArray(o, "columns", n =>
        {
            var co = n.AsObject();
            return new TableColumn(
                (string?)co["name"] ?? "",
                Formats.ParseUnit((string?)co["width"]),
                (string?)co["headerText"],
                (string?)co["detailExpression"],
                (string?)co["footerExpression"]);
        });
        return new TableElement
        {
            Bounds = bounds,
            HeaderHeight = Formats.ParseUnit((string?)o["headerHeight"]),
            DetailHeight = Formats.ParseUnit((string?)o["detailHeight"]),
            FooterHeight = Formats.ParseUnit((string?)o["footerHeight"]),
            DataExpression = (string?)o["dataExpression"],
            Columns = new EquatableArray<TableColumn>(columns),
        };
    }

    // ── RDL F2 advanced elements ──────────────────────────────────────────────────

    private static TablixElement ReadTablix(JsonObject o, Rectangle bounds)
    {
        var rowGroups = ReadArray(o, "rowGroups", n => ReadTablixGroup(n.AsObject()));
        var colGroups = ReadArray(o, "columnGroups", n => ReadTablixGroup(n.AsObject()));
        var cells = ReadArray(o, "cells", n =>
        {
            var co = n.AsObject();
            ReportElement? content = co["content"] is JsonObject ce ? ReadElement(ce) : null;
            return new TablixCell((int?)co["rowIndex"] ?? 0, (int?)co["columnIndex"] ?? 0, content);
        });
        var columnWidths = ReadArray(o, "columnWidths", n => (double?)n ?? 0d);
        return new TablixElement
        {
            Bounds = bounds,
            DataSetName = (string?)o["dataSetName"],
            RowGroups = new EquatableArray<TablixGroup>(rowGroups),
            ColumnGroups = new EquatableArray<TablixGroup>(colGroups),
            Cells = new EquatableArray<TablixCell>(cells),
            ColumnWidths = new EquatableArray<double>(columnWidths),
            RowSubtotals = (bool?)o["rowSubtotals"] ?? false,
            ColumnSubtotals = (bool?)o["columnSubtotals"] ?? false,
            SubtotalLabel = (string?)o["subtotalLabel"],
            GrandTotalLabel = (string?)o["grandTotalLabel"],
            NoRowsMessage = (string?)o["noRowsMessage"],
        };
    }

    private static TablixGroup ReadTablixGroup(JsonObject o)
        => new((string?)o["name"] ?? "",
               (string?)o["groupExpression"],
               (string?)o["sortExpression"],
               (bool?)o["sortDescending"] ?? false);

    private static GaugeElement ReadGauge(JsonObject o, Rectangle bounds)
    {
        var ranges = ReadArray(o, "ranges", n =>
        {
            var ro = n.AsObject();
            return new GaugeRange(
                (string?)ro["start"] ?? "0",
                (string?)ro["end"] ?? "100",
                (string?)ro["color"] ?? "#000000");
        });
        return new GaugeElement
        {
            Bounds = bounds,
            Kind = Enum.Parse<GaugeKind>((string?)o["gaugeKind"] ?? nameof(GaugeKind.Radial)),
            MinimumExpression = (string?)o["minimum"] ?? "0",
            MaximumExpression = (string?)o["maximum"] ?? "100",
            ValueExpression = (string?)o["value"] ?? "0",
            Ranges = new EquatableArray<GaugeRange>(ranges),
        };
    }

    private static IndicatorElement ReadIndicator(JsonObject o, Rectangle bounds)
    {
        var states = ReadArray(o, "states", n =>
        {
            var so = n.AsObject();
            return new IndicatorState(
                (string?)so["start"] ?? "0",
                (string?)so["end"] ?? "100",
                (string?)so["icon"] ?? "circle");
        });
        return new IndicatorElement
        {
            Bounds = bounds,
            Kind = Enum.Parse<IndicatorKind>((string?)o["indicatorKind"] ?? nameof(IndicatorKind.DirectionalArrow)),
            ValueExpression = (string?)o["value"] ?? "0",
            States = new EquatableArray<IndicatorState>(states),
        };
    }

    // ── Styling ─────────────────────────────────────────────────────────────────

    private static Style ReadStyle(JsonObject? o)
    {
        if (o is null)
        {
            return Style.Default;
        }
        var font = o["font"] is JsonObject fo
            ? new Font((string?)fo["family"] ?? "Arial",
                       (double?)fo["size"] ?? 10,
                       Enum.Parse<FontStyle>((string?)fo["style"] ?? nameof(FontStyle.Regular)))
            : null;
        var border = o["border"] is JsonObject bo ? ReadBorder(bo) : null;
        var padding = (string?)o["padding"] is { } p && !string.IsNullOrEmpty(p)
            ? Formats.ParseThickness(p)
            : (Thickness?)null;
        return new Style(
            font,
            (string?)o["foreColor"] is { } fc ? Formats.ParseColor(fc) : null,
            (string?)o["backColor"] is { } bc ? Formats.ParseColor(bc) : null,
            border,
            padding,
            Enum.Parse<HorizontalAlignment>((string?)o["horizontalAlignment"] ?? nameof(HorizontalAlignment.Left)),
            Enum.Parse<VerticalAlignment>((string?)o["verticalAlignment"] ?? nameof(VerticalAlignment.Top)),
            (bool?)o["wordWrap"] ?? true,
            (string?)o["format"]);
    }

    private static Border ReadBorder(JsonObject o)
    {
        BorderSide Side(string key) => o[key] is JsonObject so
            ? new BorderSide(
                Enum.Parse<BorderLineStyle>((string?)so["style"] ?? nameof(BorderLineStyle.None)),
                Formats.ParseUnit((string?)so["thickness"]),
                Formats.ParseColor((string?)so["color"]))
            : BorderSide.None;
        return new Border(Side("left"), Side("top"), Side("right"), Side("bottom"));
    }
}
