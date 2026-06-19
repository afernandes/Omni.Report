using System.Globalization;
using System.Text.Json.Nodes;
using Reporting.Bands;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Parameters;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

internal static class RepJsonWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static JsonObject Write(ReportDefinition def, SchemaVersion version)
    {
        var obj = new JsonObject
        {
            ["schemaVersion"] = version.ToString(),
            ["name"] = def.Name,
            ["pageSetup"] = WritePageSetup(def),
        };

        if (def.Parameters.Count > 0)
        {
            obj["parameters"] = new JsonArray(def.Parameters.Select(WriteParameter).Cast<JsonNode?>().ToArray());
        }
        if (def.DataSources.Count > 0)
        {
            obj["dataSources"] = new JsonArray(def.DataSources.Select(WriteDataSource).Cast<JsonNode?>().ToArray());
        }
        if (def.Variables.Count > 0)
        {
            obj["variables"] = new JsonArray(def.Variables.Select(WriteVariable).Cast<JsonNode?>().ToArray());
        }
        if (def.ReportHeader is not null)
        {
            obj["reportHeader"] = WriteBand(def.ReportHeader);
        }
        if (def.PageHeader is not null)
        {
            obj["pageHeader"] = WriteBand(def.PageHeader);
        }
        if (def.Groups.Count > 0)
        {
            obj["groups"] = new JsonArray(def.Groups.Select(WriteGroup).Cast<JsonNode?>().ToArray());
        }
        obj["detail"] = WriteDetail(def.Detail);
        if (def.PageFooter is not null)
        {
            obj["pageFooter"] = WriteBand(def.PageFooter);
        }
        if (def.ReportFooter is not null)
        {
            obj["reportFooter"] = WriteBand(def.ReportFooter);
        }
        if (def.Metadata.Count > 0)
        {
            var meta = new JsonObject();
            foreach (var kv in def.Metadata)
            {
                meta[kv.Key] = kv.Value;
            }
            obj["metadata"] = meta;
        }
        return obj;
    }

    private static JsonObject WritePageSetup(ReportDefinition def)
    {
        var s = def.PageSetup;
        return new JsonObject
        {
            ["paper"] = new JsonObject
            {
                ["name"] = s.Paper.Name,
                ["width"] = Formats.FormatUnit(s.Paper.Width),
                ["height"] = Formats.FormatUnit(s.Paper.Height),
            },
            ["orientation"] = s.Orientation.ToString(),
            ["margins"] = Formats.FormatThickness(s.Margins),
            ["columns"] = s.Columns,
            ["columnSpacing"] = Formats.FormatUnit(s.ColumnSpacing),
        };
    }

    private static JsonObject WriteParameter(ReportParameter p)
    {
        var o = new JsonObject
        {
            ["name"] = p.Name,
            ["type"] = Formats.FormatType(p.ValueType),
            ["required"] = p.Required,
            ["allowMultiple"] = p.AllowMultiple,
        };
        if (p.Prompt is not null)
        {
            o["prompt"] = p.Prompt;
        }
        if (p.DefaultValue is not null)
        {
            o["defaultValue"] = Convert.ToString(p.DefaultValue, Inv);
        }
        return o;
    }

    private static JsonObject WriteVariable(ReportVariable v)
        => new()
        {
            ["name"] = v.Name,
            ["expression"] = v.Expression,
            ["scope"] = v.Scope.ToString(),
        };

    private static JsonObject WriteDataSource(DataSourceDefinition ds)
    {
        var o = new JsonObject { ["name"] = ds.Name };
        if (ds.DataMember is not null)
        {
            o["dataMember"] = ds.DataMember;
        }
        if (ds.Fields.Count > 0)
        {
            o["fields"] = new JsonArray(ds.Fields.Select(f =>
            {
                var fo = new JsonObject { ["name"] = f.Name };
                if (f.FieldType is not null)
                {
                    fo["type"] = Formats.FormatType(f.FieldType);
                }
                if (f.DisplayName is not null)
                {
                    fo["displayName"] = f.DisplayName;
                }
                return (JsonNode?)fo;
            }).ToArray());
        }
        if (ds.Relations.Count > 0)
        {
            o["relations"] = new JsonArray(ds.Relations.Select(r => (JsonNode?)new JsonObject
            {
                ["name"] = r.Name,
                ["parentSource"] = r.ParentSource,
                ["parentField"] = r.ParentField,
                ["childSource"] = r.ChildSource,
                ["childField"] = r.ChildField,
            }).ToArray());
        }
        if (ds.FilterExpression is not null)
        {
            o["filterExpression"] = ds.FilterExpression;
        }
        if (ds.CalculatedFields.Count > 0)
        {
            o["calculatedFields"] = new JsonArray(ds.CalculatedFields.Select(WriteCalculatedField).Cast<JsonNode?>().ToArray());
        }
        if (ds.SortExpressions.Count > 0)
        {
            o["sortExpressions"] = WriteSortExpressions(ds.SortExpressions);
        }
        if (ds.Parameters.Count > 0)
        {
            var p = new JsonObject();
            foreach (var kv in ds.Parameters)
            {
                p[kv.Key] = kv.Value;
            }
            o["parameters"] = p;
        }
        return o;
    }

    // ── Shared sort / calculated-field helpers (data sources, bands, groups) ─────

    private static JsonArray WriteSortExpressions(Reporting.Common.EquatableArray<SortDescriptor> sorts)
        => new(sorts.Select(s => (JsonNode?)new JsonObject
        {
            ["expression"] = s.Expression,
            ["direction"] = s.Direction.ToString(),
        }).ToArray());

    private static JsonObject WriteCalculatedField(CalculatedField cf)
    {
        var o = new JsonObject { ["name"] = cf.Name, ["expression"] = cf.Expression };
        if (cf.ResultType is not null)
        {
            o["type"] = Formats.FormatType(cf.ResultType);
        }
        return o;
    }

    // ── Bands ───────────────────────────────────────────────────────────────────

    private static JsonObject WriteBand(ReportBand band)
    {
        var o = new JsonObject
        {
            ["kind"] = band.Kind.ToString(),
            ["height"] = Formats.FormatUnit(band.Height),
            ["visible"] = band.Visible,
            ["printOnFirstPage"] = band.PrintOnFirstPage,
            ["printOnLastPage"] = band.PrintOnLastPage,
            ["pageBreak"] = band.PageBreak.ToString(),
            ["elements"] = new JsonArray(band.Elements.Select(e => (JsonNode?)WriteElement(e)).ToArray()),
        };
        if (band.VisibleExpression is not null)
        {
            o["visibleExpression"] = band.VisibleExpression;
        }
        return o;
    }

    private static JsonObject WriteDetail(DetailBand band)
    {
        var o = new JsonObject
        {
            ["height"] = Formats.FormatUnit(band.Height),
            ["visible"] = band.Visible,
            ["canGrow"] = band.CanGrow,
            ["canShrink"] = band.CanShrink,
            ["pageBreak"] = band.PageBreak.ToString(),
            ["elements"] = new JsonArray(band.Elements.Select(e => (JsonNode?)WriteElement(e)).ToArray()),
        };
        if (band.VisibleExpression is not null)
        {
            o["visibleExpression"] = band.VisibleExpression;
        }
        if (band.NoRowsMessage is not null)
        {
            o["noRowsMessage"] = band.NoRowsMessage;
        }
        if (band.FilterExpression is not null)
        {
            o["filterExpression"] = band.FilterExpression;
        }
        if (band.SortExpressions.Count > 0)
        {
            o["sortExpressions"] = WriteSortExpressions(band.SortExpressions);
        }
        if (band.SubDetails.Count > 0)
        {
            o["subDetails"] = new JsonArray(band.SubDetails.Select(WriteSubDetail).Cast<JsonNode?>().ToArray());
        }
        return o;
    }

    private static JsonObject WriteSubDetail(SubDetailBand sd)
    {
        var o = new JsonObject
        {
            ["name"] = sd.Name,
            ["dataMember"] = sd.DataMember,
            ["height"] = Formats.FormatUnit(sd.Height),
            ["visible"] = sd.Visible,
            ["printIfEmpty"] = sd.PrintIfEmpty,
            ["elements"] = new JsonArray(sd.Elements.Select(e => (JsonNode?)WriteElement(e)).ToArray()),
        };
        if (sd.VisibleExpression is not null) o["visibleExpression"] = sd.VisibleExpression;
        if (sd.NoRowsMessage is not null) o["noRowsMessage"] = sd.NoRowsMessage;
        if (sd.FilterExpression is not null) o["filterExpression"] = sd.FilterExpression;
        if (sd.SortExpressions.Count > 0) o["sortExpressions"] = WriteSortExpressions(sd.SortExpressions);
        if (sd.Header is not null) o["header"] = WriteBand(sd.Header);
        if (sd.Footer is not null) o["footer"] = WriteBand(sd.Footer);
        return o;
    }

    private static JsonObject WriteGroup(GroupBand g)
    {
        var o = new JsonObject
        {
            ["name"] = g.Name,
            ["groupExpression"] = g.GroupExpression,
            ["keepTogether"] = g.KeepTogether,
            ["newPageBefore"] = g.NewPageBefore,
            ["newPageAfter"] = g.NewPageAfter,
            ["repeatHeaderOnNewPage"] = g.RepeatHeaderOnNewPage,
            ["visible"] = g.Visible,
            ["pageBreak"] = g.PageBreak.ToString(),
        };
        if (g.VisibleExpression is not null)
        {
            o["visibleExpression"] = g.VisibleExpression;
        }
        if (g.FilterExpression is not null)
        {
            o["filterExpression"] = g.FilterExpression;
        }
        if (g.SortExpressions.Count > 0)
        {
            o["sortExpressions"] = WriteSortExpressions(g.SortExpressions);
        }
        if (g.Variables.Count > 0)
        {
            o["variables"] = new JsonArray(g.Variables.Select(WriteVariable).Cast<JsonNode?>().ToArray());
        }
        if (g.Header is not null)
        {
            o["header"] = WriteBand(g.Header);
        }
        if (g.Footer is not null)
        {
            o["footer"] = WriteBand(g.Footer);
        }
        return o;
    }

    // ── Elements ────────────────────────────────────────────────────────────────

    private static JsonObject WriteElement(ReportElement element)
    {
        var o = new JsonObject
        {
            ["kind"] = ElementKindFor(element),
            ["id"] = element.Id,
            ["bounds"] = Formats.FormatRectangle(element.Bounds),
            ["visible"] = element.Visible,
            ["style"] = WriteStyle(element.Style),
        };
        if (element.Name is not null)
        {
            o["name"] = element.Name;
        }
        if (element.VisibleExpression is not null)
        {
            o["visibleExpression"] = element.VisibleExpression;
        }
        if (element.ConditionalFormats.Count > 0)
        {
            o["conditionalFormats"] = new JsonArray(element.ConditionalFormats.Select(cf =>
                (JsonNode?)new JsonObject { ["condition"] = cf.Condition, ["style"] = WriteStyle(cf.Style) }).ToArray());
        }
        if (element.PropertyExpressions.Count > 0)
        {
            var pe = new JsonObject();
            foreach (var kv in element.PropertyExpressions)
            {
                pe[kv.Key] = kv.Value;
            }
            o["propertyExpressions"] = pe;
        }
        // RDL-style extensions on the abstract base — emitted only when set so the common
        // shape stays compact for elements that don't use them (parity with the .repx writer).
        if (element.Action is { } act)
        {
            o["action"] = WriteAction(act);
        }
        if (element.Bookmark is not null)
        {
            o["bookmark"] = element.Bookmark;
        }
        if (element.DocumentMapLabel is not null)
        {
            o["documentMapLabel"] = element.DocumentMapLabel;
        }
        if (element.ToggleItemId is not null)
        {
            o["toggleItemId"] = element.ToggleItemId;
        }
        if (element.InitiallyHidden)
        {
            o["initiallyHidden"] = true;
        }

        switch (element)
        {
            case LabelElement lbl:
                o["text"] = lbl.Text;
                break;
            case TextBoxElement tb:
                o["expression"] = tb.Expression;
                o["canGrow"] = tb.CanGrow;
                o["canShrink"] = tb.CanShrink;
                if (tb.TextRuns.Count > 0)
                {
                    o["textRuns"] = new JsonArray(tb.TextRuns.Select(r => (JsonNode?)WriteTextRun(r)).ToArray());
                }
                break;
            case LineElement line:
                o["direction"] = line.Direction.ToString();
                o["pen"] = new JsonObject
                {
                    ["style"] = line.Pen.Style.ToString(),
                    ["thickness"] = Formats.FormatUnit(line.Pen.Thickness),
                    ["color"] = Formats.FormatColor(line.Pen.Color),
                };
                break;
            case RectangleElement rect:
                if (rect.FillColor is not null)
                {
                    o["fillColor"] = Formats.FormatColor(rect.FillColor.Value);
                }
                o["cornerRadius"] = Formats.FormatUnit(rect.CornerRadius);
                break;
            case EllipseElement ellipse:
                if (ellipse.FillColor is not null)
                {
                    o["fillColor"] = Formats.FormatColor(ellipse.FillColor.Value);
                }
                break;
            case ImageElement img:
                o["source"] = img.Source.ToString();
                o["sizing"] = img.Sizing.ToString();
                if (img.Path is not null)
                {
                    o["path"] = img.Path;
                }
                if (img.Expression is not null)
                {
                    o["expression"] = img.Expression;
                }
                if (img.InlineData.Count > 0)
                {
                    var bytes = new byte[img.InlineData.Count];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = img.InlineData[i];
                    }
                    o["inlineData"] = Convert.ToBase64String(bytes);
                }
                break;
            case BarcodeElement bc:
                o["symbology"] = bc.Symbology.ToString();
                o["expression"] = bc.Expression;
                o["showText"] = bc.ShowText;
                break;
            case ChartElement chart:
                o["chartKind"] = chart.Kind.ToString();
                if (chart.Title is not null)
                {
                    o["title"] = chart.Title;
                }
                o["showLegend"] = chart.ShowLegend;
                if (chart.Series.Count > 0)
                {
                    o["series"] = new JsonArray(chart.Series.Select(s =>
                    {
                        var so = new JsonObject
                        {
                            ["name"] = s.Name,
                            ["categoryExpression"] = s.CategoryExpression,
                            ["valueExpression"] = s.ValueExpression,
                        };
                        if (s.Color is not null)
                        {
                            so["color"] = Formats.FormatColor(s.Color.Value);
                        }
                        if (s.SizeExpression is not null) so["sizeExpression"] = s.SizeExpression;
                        if (s.HighExpression is not null) so["highExpression"] = s.HighExpression;
                        if (s.LowExpression is not null) so["lowExpression"] = s.LowExpression;
                        return (JsonNode?)so;
                    }).ToArray());
                }
                break;
            case SubreportElement sub:
                if (sub.ReportId is not null)
                {
                    o["reportId"] = sub.ReportId;
                }
                if (sub.InlineDefinition is not null)
                {
                    o["inlineDefinition"] = Write(sub.InlineDefinition, SchemaVersion.Current);
                }
                if (sub.DataExpression is not null)
                {
                    o["dataExpression"] = sub.DataExpression;
                }
                if (sub.ParameterBindings.Count > 0)
                {
                    var pb = new JsonObject();
                    foreach (var kv in sub.ParameterBindings)
                    {
                        pb[kv.Key] = kv.Value;
                    }
                    o["parameterBindings"] = pb;
                }
                break;
            case TableElement table:
                o["headerHeight"] = Formats.FormatUnit(table.HeaderHeight);
                o["detailHeight"] = Formats.FormatUnit(table.DetailHeight);
                o["footerHeight"] = Formats.FormatUnit(table.FooterHeight);
                if (table.DataExpression is not null)
                {
                    o["dataExpression"] = table.DataExpression;
                }
                o["columns"] = new JsonArray(table.Columns.Select(c =>
                {
                    var co = new JsonObject { ["name"] = c.Name, ["width"] = Formats.FormatUnit(c.Width) };
                    if (c.HeaderText is not null)
                    {
                        co["headerText"] = c.HeaderText;
                    }
                    if (c.DetailExpression is not null)
                    {
                        co["detailExpression"] = c.DetailExpression;
                    }
                    if (c.FooterExpression is not null)
                    {
                        co["footerExpression"] = c.FooterExpression;
                    }
                    return (JsonNode?)co;
                }).ToArray());
                break;
            // ── RDL F2 advanced elements ─────────────────────────────────────────
            case TablixElement tablix:
                if (tablix.DataSetName is not null)
                {
                    o["dataSetName"] = tablix.DataSetName;
                }
                if (tablix.ColumnWidths.Count > 0)
                {
                    o["columnWidths"] = new JsonArray(tablix.ColumnWidths.Select(wt => (JsonNode?)wt).ToArray());
                }
                if (tablix.RowGroups.Count > 0)
                {
                    o["rowGroups"] = new JsonArray(tablix.RowGroups.Select(g => (JsonNode?)WriteTablixGroup(g)).ToArray());
                }
                if (tablix.ColumnGroups.Count > 0)
                {
                    o["columnGroups"] = new JsonArray(tablix.ColumnGroups.Select(g => (JsonNode?)WriteTablixGroup(g)).ToArray());
                }
                if (tablix.Cells.Count > 0)
                {
                    o["cells"] = new JsonArray(tablix.Cells.Select(c =>
                    {
                        var co = new JsonObject { ["rowIndex"] = c.RowIndex, ["columnIndex"] = c.ColumnIndex };
                        if (c.Content is not null)
                        {
                            co["content"] = WriteElement(c.Content);
                        }
                        return (JsonNode?)co;
                    }).ToArray());
                }
                break;
            case CodeElement code:
                o["language"] = code.Language.ToString();
                o["source"] = code.Source;
                break;
            case MapElement map:
                if (map.Basemap is not null)
                {
                    o["basemap"] = map.Basemap;
                }
                if (map.DataSetName is not null)
                {
                    o["dataSetName"] = map.DataSetName;
                }
                if (map.LatitudeExpression is not null)
                {
                    o["latitude"] = map.LatitudeExpression;
                }
                if (map.LongitudeExpression is not null)
                {
                    o["longitude"] = map.LongitudeExpression;
                }
                if (map.ShapeSet is not null)
                {
                    o["shapeSet"] = map.ShapeSet;
                }
                if (map.ShapesGeoJson is not null)
                {
                    o["shapesGeoJson"] = map.ShapesGeoJson;
                }
                o["showGraticule"] = map.ShowGraticule;
                o["shapeFill"] = map.ShapeFill;
                o["shapeStroke"] = map.ShapeStroke;
                break;
            case GaugeElement gauge:
                o["gaugeKind"] = gauge.Kind.ToString();
                o["minimum"] = gauge.MinimumExpression;
                o["maximum"] = gauge.MaximumExpression;
                o["value"] = gauge.ValueExpression;
                if (gauge.Ranges.Count > 0)
                {
                    o["ranges"] = new JsonArray(gauge.Ranges.Select(r => (JsonNode?)new JsonObject
                    {
                        ["start"] = r.StartExpression,
                        ["end"] = r.EndExpression,
                        ["color"] = r.ColorHex,
                    }).ToArray());
                }
                break;
            case DataBarElement bar:
                o["value"] = bar.ValueExpression;
                o["minimum"] = bar.MinimumExpression;
                o["maximum"] = bar.MaximumExpression;
                o["fillColor"] = bar.FillColor;
                break;
            case SparklineElement spark:
                o["sparklineKind"] = spark.Kind.ToString();
                o["value"] = spark.ValueExpression;
                if (spark.DataSetName is not null)
                {
                    o["dataSetName"] = spark.DataSetName;
                }
                if (spark.CategoryExpression is not null)
                {
                    o["category"] = spark.CategoryExpression;
                }
                break;
            case IndicatorElement ind:
                o["indicatorKind"] = ind.Kind.ToString();
                o["value"] = ind.ValueExpression;
                if (ind.States.Count > 0)
                {
                    o["states"] = new JsonArray(ind.States.Select(st => (JsonNode?)new JsonObject
                    {
                        ["start"] = st.StartExpression,
                        ["end"] = st.EndExpression,
                        ["icon"] = st.IconName,
                    }).ToArray());
                }
                break;
        }
        return o;
    }

    private static JsonObject WriteTablixGroup(TablixGroup g)
    {
        var o = new JsonObject { ["name"] = g.Name };
        if (g.GroupExpression is not null)
        {
            o["groupExpression"] = g.GroupExpression;
        }
        if (g.SortExpression is not null)
        {
            o["sortExpression"] = g.SortExpression;
        }
        if (g.SortDescending)
        {
            o["sortDescending"] = true;
        }
        return o;
    }

    internal static string ElementKindFor(ReportElement element)
        => element switch
        {
            LabelElement => "Label",
            TextBoxElement => "TextBox",
            LineElement => "Line",
            RectangleElement => "Rectangle",
            EllipseElement => "Ellipse",
            ImageElement => "Image",
            BarcodeElement => "Barcode",
            ChartElement => "Chart",
            SubreportElement => "Subreport",
            TableElement => "Table",
            // ── RDL F2 advanced elements ─────────────────────────────────────────
            TablixElement => "Tablix",
            CodeElement => "Code",
            MapElement => "Map",
            GaugeElement => "Gauge",
            DataBarElement => "DataBar",
            SparklineElement => "Sparkline",
            IndicatorElement => "Indicator",
            _ => throw new InvalidOperationException($"Unsupported element type: {element.GetType().Name}"),
        };

    private static JsonObject WriteAction(ElementAction action)
    {
        var o = new JsonObject { ["kind"] = action.Kind.ToString() };
        if (action.Hyperlink is not null)
        {
            o["hyperlink"] = action.Hyperlink;
        }
        if (action.BookmarkId is not null)
        {
            o["bookmarkId"] = action.BookmarkId;
        }
        if (action.DrillthroughReportName is not null)
        {
            o["drillthroughReportName"] = action.DrillthroughReportName;
        }
        if (action.DrillthroughParameters.Count > 0)
        {
            o["parameters"] = new JsonArray(action.DrillthroughParameters.Select(p => (JsonNode?)new JsonObject
            {
                ["name"] = p.Name,
                ["value"] = p.Value,
                ["omit"] = p.Omit,
            }).ToArray());
        }
        return o;
    }

    private static JsonObject WriteTextRun(TextRun run)
    {
        var o = new JsonObject { ["value"] = run.Value };
        if (run.Style is not null)
        {
            o["style"] = WriteStyle(run.Style);
        }
        if (run.Action is not null)
        {
            o["action"] = WriteAction(run.Action);
        }
        return o;
    }

    private static JsonObject WriteStyle(Style style)
    {
        var o = new JsonObject
        {
            ["horizontalAlignment"] = style.HorizontalAlignment.ToString(),
            ["verticalAlignment"] = style.VerticalAlignment.ToString(),
            ["wordWrap"] = style.WordWrap,
        };
        if (style.Format is not null)
        {
            o["format"] = style.Format;
        }
        if (style.Font is not null)
        {
            o["font"] = new JsonObject
            {
                ["family"] = style.Font.Family,
                ["size"] = style.Font.Size,
                ["style"] = style.Font.Style.ToString(),
            };
        }
        if (style.ForeColor is not null)
        {
            o["foreColor"] = Formats.FormatColor(style.ForeColor.Value);
        }
        if (style.BackColor is not null)
        {
            o["backColor"] = Formats.FormatColor(style.BackColor.Value);
        }
        if (style.Padding is not null)
        {
            o["padding"] = Formats.FormatThickness(style.Padding.Value);
        }
        if (style.Border is not null)
        {
            o["border"] = WriteBorder(style.Border);
        }
        return o;
    }

    private static JsonObject WriteBorder(Border b)
        => new()
        {
            ["left"] = WriteSide(b.Left),
            ["top"] = WriteSide(b.Top),
            ["right"] = WriteSide(b.Right),
            ["bottom"] = WriteSide(b.Bottom),
        };

    private static JsonObject WriteSide(BorderSide s)
        => new()
        {
            ["style"] = s.Style.ToString(),
            ["thickness"] = Formats.FormatUnit(s.Thickness),
            ["color"] = Formats.FormatColor(s.Color),
        };
}
