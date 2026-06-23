using System.Globalization;
using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
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
/// <para>Not yet imported (follow-ups): Tablix/Matrix and Chart data regions, dataset queries,
/// subreports, and Database images. These are skipped, not errored — the structural import always
/// succeeds.</para>
/// <para>Not thread-safe: an instance carries per-import state (the resolved embedded-image map). Use
/// one instance per import, or guard external concurrency.</para>
/// </remarks>
public sealed class RdlImporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Name → bytes of the report's <EmbeddedImages>, resolved per Import() and consumed by embedded
    // <Image> references. Reset at the start of every Import.
    private IReadOnlyDictionary<string, byte[]> _embeddedImages = new Dictionary<string, byte[]>();

    // Non-fatal import warnings (e.g. partially-mapped Tablix) collected per Import and surfaced in
    // Metadata["ImportWarnings"] — import never fails silently.
    private readonly List<string> _warnings = new();

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

        _embeddedImages = ReadEmbeddedImages(report);
        _warnings.Clear();

        var page = El(report, "Page");
        var marginHost = page ?? report;
        var pageWidth = ParseSize(Val(marginHost, "PageWidth")) ?? PaperSize.A4.Width;
        var pageHeight = ParseSize(Val(marginHost, "PageHeight")) ?? PaperSize.A4.Height;
        var margins = new Thickness(
            ParseSize(Val(marginHost, "LeftMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "TopMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "RightMargin")) ?? Unit.Zero,
            ParseSize(Val(marginHost, "BottomMargin")) ?? Unit.Zero);
        // Newspaper/snake columns: RDL <Columns>/<ColumnSpacing> live on <Page> (2016+) or <Report> (legacy);
        // marginHost (page ?? report) covers both. Absent → 1 column (no change).
        var columns = int.TryParse(Val(marginHost, "Columns"), out var n) && n > 1 ? n : 1;
        var columnSpacing = columns > 1 ? ParseSize(Val(marginHost, "ColumnSpacing")) ?? Unit.Zero : Unit.Zero;
        var pageSetup = new PageSetup(new PaperSize("Imported", pageWidth, pageHeight), Orientation.Portrait,
            margins, columns, columnSpacing);

        var parameters = (El(report, "ReportParameters")?.Elements()
                .Where(e => e.Name.LocalName == "ReportParameter")
                .Select(ReadParameter).ToArray())
            ?? Array.Empty<ReportParameter>();

        // Body's free report items render once → a ReportHeader band. Page header/footer map directly.
        var body = El(report, "Body");
        var pageHeaderEl = El(page, "PageHeader");
        var pageFooterEl = El(page, "PageFooter");
        var pageFooter = BandFrom(El(pageFooterEl, "ReportItems"), Val(pageFooterEl, "Height"), BandKind.PageFooter);

        // First-cut Tablix→bands: a Body that is exactly one flat Tablix becomes a repeating column-header
        // band + a paginating DetailBand (so a long table flows across pages and repeats its header) instead
        // of one monolithic TablixElement. Anything else keeps the TablixElement path.
        ReportBand? reportHeader;
        ReportBand? pageHeader;
        DetailBand detail;
        if (TryFlatTablixBands(body, pageHeaderEl is not null, out var flatHeader, out var flatDetail))
        {
            reportHeader = null;
            pageHeader = flatHeader;
            detail = flatDetail!;
        }
        else
        {
            reportHeader = BandFrom(El(body, "ReportItems"), Val(body, "Height"), BandKind.ReportHeader);
            pageHeader = BandFrom(El(pageHeaderEl, "ReportItems"), Val(pageHeaderEl, "Height"), BandKind.PageHeader);
            detail = DetailBand.Empty;
        }

        // Run every reader that can add warnings BEFORE snapshotting Metadata (which merges _warnings) —
        // an object-initializer evaluates top-to-bottom, so reading DataSets after Metadata would drop their
        // warnings.
        var variables = ReadVariables(report);
        var dataSources = ReadDataSets(report);
        return new ReportDefinition(reportName ?? "RdlReport", pageSetup, detail)
        {
            Parameters = new EquatableArray<ReportParameter>(parameters),
            ReportHeader = reportHeader,
            PageHeader = pageHeader,
            PageFooter = pageFooter,
            Variables = new EquatableArray<ReportVariable>(variables),
            DataSources = new EquatableArray<DataSourceDefinition>(dataSources),
            Metadata = new EquatableDictionary<string, string>(MetadataWithWarnings(report)),
        };
    }

    // RDL <DataSets><DataSet Name> → DataSourceDefinition (the binding metadata; query execution stays
    // delegated to the host's IReportDataSource). Maps Fields/CalculatedFields, structured <Filters> to a
    // boolean FilterExpression, <SortExpressions>, and preserves CommandText/QueryParameters in Parameters
    // (a dedicated Query record is a follow-up — PR-8).
    private DataSourceDefinition[] ReadDataSets(XElement report)
        => El(report, "DataSets")?.Elements().Where(e => e.Name.LocalName == "DataSet")
            .Where(d => d.Attribute("Name")?.Value is { Length: > 0 })
            .Select(ReadDataSet).ToArray()
            ?? Array.Empty<DataSourceDefinition>();

    private DataSourceDefinition ReadDataSet(XElement ds)
    {
        var name = ds.Attribute("Name")!.Value;
        var fields = new List<DataField>();
        var calculated = new List<CalculatedField>();
        foreach (var f in El(ds, "Fields")?.Elements().Where(e => e.Name.LocalName == "Field")
                 ?? Enumerable.Empty<XElement>())
        {
            var fieldName = f.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(fieldName))
            {
                continue;
            }
            // A <Field> with a <Value> expression is a calculated field; otherwise it's a data column.
            if (Val(f, "Value") is { Length: > 0 } valueExpr)
            {
                // rd:TypeName on a calculated field carries its ResultType (the exporter round-trips it).
                calculated.Add(new CalculatedField(fieldName, RdlExpression.Convert(valueExpr), MapClrType(Val(f, "TypeName"))));
            }
            else
            {
                fields.Add(new DataField(fieldName, MapClrType(Val(f, "TypeName"))));
            }
        }

        var query = El(ds, "Query");
        var ps = new Dictionary<string, string>(StringComparer.Ordinal);
        // Write the DESIGNER's live query convention (_sql / _storedProc / param:@x) — the same keys
        // DataSourceCatalog.ToDefinition/FromDefinition and the runtime DataSourceFactory consume. The old
        // CommandText/CommandType/QueryParameter: keys were dead (nothing read them), so an imported query
        // was silently lost; now it opens in the designer and executes.
        if (Val(query, "CommandText") is { Length: > 0 } sql)
        {
            ps["_sql"] = sql;
        }
        if (string.Equals(Val(query, "CommandType"), "StoredProcedure", StringComparison.OrdinalIgnoreCase))
        {
            ps["_storedProc"] = "true";
        }
        foreach (var qp in El(query, "QueryParameters")?.Elements().Where(e => e.Name.LocalName == "QueryParameter")
                 ?? Enumerable.Empty<XElement>())
        {
            if (qp.Attribute("Name")?.Value is { Length: > 0 } pn)
            {
                ps[$"param:{pn}"] = QueryParamEncoding(Val(qp, "Value"), name, pn);
            }
        }

        return new DataSourceDefinition(name)
        {
            Fields = new EquatableArray<DataField>(fields),
            CalculatedFields = new EquatableArray<CalculatedField>(calculated),
            FilterExpression = ReadFilters(El(ds, "Filters")),
            SortExpressions = new EquatableArray<SortDescriptor>(ReadDataSetSorts(El(ds, "SortExpressions"))),
            Parameters = new EquatableDictionary<string, string>(ps),
        };
    }

    // RDL <Filters> is structured (expression + operator + values); fold into one boolean expression
    // (multiple filters AND together) the engine can evaluate per row.
    private static string? ReadFilters(XElement? filters)
    {
        if (filters is null)
        {
            return null;
        }
        var clauses = new List<string>();
        foreach (var f in filters.Elements().Where(e => e.Name.LocalName == "Filter"))
        {
            var left = RdlExpression.Convert(Val(f, "FilterExpression"));
            if (string.IsNullOrEmpty(left))
            {
                continue;
            }
            var value = FilterValue(El(f, "FilterValues"));
            if (value is null)
            {
                continue; // no comparison value → skip rather than emit a malformed clause
            }
            var op = Val(f, "Operator") ?? "Equal";
            var clause = op switch
            {
                "Equal" => $"{left} == {value}",
                "NotEqual" => $"{left} <> {value}",
                "GreaterThan" => $"{left} > {value}",
                "GreaterThanOrEqual" => $"{left} >= {value}",
                "LessThan" => $"{left} < {value}",
                "LessThanOrEqual" => $"{left} <= {value}",
                "Like" => $"Like({left}, {value})",
                // Unsupported (In/Between/TopN/…) → skip, never fabricate a wrong equality predicate.
                _ => null,
            };
            if (clause is not null)
            {
                clauses.Add(clause);
            }
        }
        return clauses.Count == 0 ? null : string.Join(" && ", clauses);
    }

    private static string? FilterValue(XElement? filterValues)
    {
        var raw = filterValues?.Elements().FirstOrDefault(e => e.Name.LocalName == "FilterValue")?.Value;
        if (string.IsNullOrEmpty(raw))
        {
            return null; // no value supplied — caller skips the clause
        }
        if (RdlExpression.IsExpression(raw))
        {
            return RdlExpression.Convert(raw);
        }
        // Literal: only a genuine bare number stays unquoted (no thousands/currency, which would tokenize
        // wrong); anything else becomes a quoted string.
        return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowLeadingSign, Inv, out _)
            ? raw
            : $"\"{raw}\"";
    }

    // Encodes an RDL <QueryParameter><Value> into the designer's "reportParam|literal" form. A pure
    // =Parameters!P.Value binds the SQL parameter to report parameter P; anything else becomes a literal.
    private string QueryParamEncoding(string? rawValue, string dataSetName, string paramName)
    {
        if (rawValue is not { Length: > 0 })
        {
            return "|";
        }
        var converted = RdlExpression.Convert(rawValue);
        const string prefix = "Parameters.";
        if (RdlExpression.IsExpression(rawValue)
            && converted.StartsWith(prefix, StringComparison.Ordinal)
            && converted.Length > prefix.Length
            && converted.IndexOf('.', prefix.Length) < 0) // exactly "Parameters.P", no further member
        {
            return converted[prefix.Length..] + "|"; // report-parameter binding
        }
        if (RdlExpression.IsExpression(rawValue))
        {
            // A dynamic value (e.g. =Today()) can't be a designer parameter binding, so it's frozen as a
            // literal string and won't be re-evaluated — never silent, so warn.
            _warnings.Add($"DataSet '{dataSetName}': parâmetro de query '{paramName}' com valor de expressão '{rawValue}' foi importado como literal (não será reavaliado; só =Parameters!P.Value vira binding).");
        }
        return "|" + converted; // literal (best-effort for non-parameter values)
    }

    private static List<SortDescriptor> ReadDataSetSorts(XElement? sorts)
    {
        var list = new List<SortDescriptor>();
        foreach (var s in sorts?.Elements().Where(e => e.Name.LocalName == "SortExpression")
                 ?? Enumerable.Empty<XElement>())
        {
            var expr = RdlExpression.Convert(Val(s, "Value"));
            if (string.IsNullOrEmpty(expr))
            {
                continue;
            }
            var desc = string.Equals(Val(s, "Direction"), "Descending", StringComparison.OrdinalIgnoreCase);
            list.Add(new SortDescriptor(expr, desc ? SortDirection.Descending : SortDirection.Ascending));
        }
        return list;
    }

    private static Type? MapClrType(string? typeName) => typeName switch
    {
        "System.Int32" or "System.Int16" or "System.Int64" => typeof(int),
        "System.Decimal" => typeof(decimal),
        "System.Double" or "System.Single" => typeof(double),
        "System.Boolean" => typeof(bool),
        "System.DateTime" => typeof(DateTime),
        "System.String" => typeof(string),
        _ => null,
    };

    // RDL <Variables><Variable Name><Value>=expr → report-level ReportVariable (Report scope).
    private static ReportVariable[] ReadVariables(XElement report)
        => El(report, "Variables")?.Elements().Where(e => e.Name.LocalName == "Variable")
            .Where(v => v.Attribute("Name")?.Value is { Length: > 0 })
            .Select(v => new ReportVariable(
                v.Attribute("Name")!.Value,
                RdlExpression.Convert(Val(v, "Value")),
                VariableScope.Report))
            .ToArray()
            ?? Array.Empty<ReportVariable>();

    // Report metadata + any non-fatal import warnings accumulated while building the bands.
    private Dictionary<string, string> MetadataWithWarnings(XElement report)
    {
        var meta = ReadMetadata(report);
        if (_warnings.Count > 0)
        {
            meta["ImportWarnings"] = string.Join(" | ", _warnings);
        }
        return meta;
    }

    // RDL <EmbeddedImages><EmbeddedImage Name><ImageData>base64 → name→bytes map (bad base64 skipped).
    private static Dictionary<string, byte[]> ReadEmbeddedImages(XElement report)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var img in El(report, "EmbeddedImages")?.Elements().Where(e => e.Name.LocalName == "EmbeddedImage")
                 ?? Enumerable.Empty<XElement>())
        {
            var name = img.Attribute("Name")?.Value;
            var data = Val(img, "ImageData");
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(data))
            {
                continue;
            }
            try { map[name] = Convert.FromBase64String(data.Trim()); }
            catch (FormatException) { }
        }
        return map;
    }

    // RDL <CustomProperties> → Metadata; report-level <Code> is preserved under "RdlCode" (a VB function
    // module — executing it is a follow-up, but the source must not be lost on import).
    private static Dictionary<string, string> ReadMetadata(XElement report)
    {
        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cp in El(report, "CustomProperties")?.Elements().Where(e => e.Name.LocalName == "CustomProperty")
                 ?? Enumerable.Empty<XElement>())
        {
            var name = Val(cp, "Name");
            if (!string.IsNullOrEmpty(name))
            {
                meta[name] = Val(cp, "Value") ?? string.Empty;
            }
        }
        if (Val(report, "Code") is { Length: > 0 } code)
        {
            meta["RdlCode"] = code;
        }
        // RDL <Report><Language> (e.g. "en-US") → the report's authoring culture. Drives Format/FormatDateTime
        // at render via the expression context. Carried in Metadata (round-trips generically); opt-in — when
        // absent the engine keeps its default culture.
        if (Val(report, "Language") is { Length: > 0 } lang)
        {
            meta["Language"] = lang;
        }
        // Report-level descriptive metadata — carried in Metadata (round-trips generically); opt-in.
        // AutoRefresh (seconds) is preserved as-is; static output has no auto-refresh pipeline.
        if (Val(report, "Description") is { Length: > 0 } desc)
        {
            meta["Description"] = desc;
        }
        if (Val(report, "Author") is { Length: > 0 } author)
        {
            meta["Author"] = author;
        }
        if (Val(report, "AutoRefresh") is { Length: > 0 } autoRefresh)
        {
            meta["AutoRefresh"] = autoRefresh;
        }
        return meta;
    }

    private ReportBand? BandFrom(XElement? reportItems, string? heightRaw, BandKind kind)
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

    // Maps one RDL report item to OmniReport element(s), offsetting its bounds by (dx, dy). A Rectangle's
    // nested items recurse with a ZERO offset into the rect's Children (relative bounds), preserving the
    // container hierarchy. Unknown item kinds are skipped (structural import never fails on them).
    private void AddItem(XElement item, Unit dx, Unit dy, List<ReportElement> into)
    {
        var bounds = Bounds(item, dx, dy);
        switch (item.Name.LocalName)
        {
            case "Textbox":
                into.Add(ApplyCommon(TextItem(item, bounds), item));
                break;
            case "Line":
                into.Add(ApplyCommon(LineItem(bounds), item));
                break;
            case "Image":
                into.Add(ApplyCommon(ImageItem(item, bounds), item));
                break;
            case "Rectangle":
                // RDL nests items inside <Rectangle><ReportItems>, positioned RELATIVE to the rectangle.
                // Preserve that hierarchy as RectangleElement.Children (relative bounds) — recurse with a zero
                // offset into a LOCAL list — instead of flattening children to absolute coords into the band.
                var rectChildren = new List<ReportElement>();
                foreach (var child in El(item, "ReportItems")?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    AddItem(child, Unit.Zero, Unit.Zero, rectChildren);
                }
                into.Add(ApplyCommon(
                    new RectangleElement { Bounds = bounds, Children = new EquatableArray<ReportElement>(rectChildren) },
                    item));
                break;
            case "Tablix":
                into.Add(ApplyCommon(TablixItem(item, TablixBounds(item, dx, dy)), item));
                break;
            case "Chart":
                into.Add(ApplyCommon(ChartItem(item, bounds), item));
                break;
            case "GaugePanel":
                into.Add(ApplyCommon(GaugeItem(item, bounds), item));
                break;
            case "Subreport":
                into.Add(ApplyCommon(SubreportItem(item, bounds), item));
                break;
            case "Map":
                _warnings.Add($"Map '{item.Attribute("Name")?.Value}': não importado (mapas RDL são follow-up).");
                break;
            case "CustomReportItem":
                var criType = Val(item, "Type");
                var cri = CustomReportItemItem(item, bounds, criType);
                if (cri is not null)
                {
                    into.Add(ApplyCommon(cri, item));
                }
                else
                {
                    _warnings.Add($"CustomReportItem '{criType}': tipo não suportado (mapeados: DataBar/Sparkline/Indicator/Gauge).");
                }
                break;
            // Other report items — skipped, not errored.
        }
    }

    // RDL <CustomReportItem> (the 2008-style wrapper SSRS uses for DataBar/Sparkline/Indicator/Gauge). The
    // rich vendor-specific config (states, ranges, palette) lives in a custom namespace and is a follow-up;
    // we map the <Type> to the matching first-class element + its primary value binding so the item lands as
    // the right EDITABLE element (completable in the Designer) instead of being silently dropped.
    private ReportElement? CustomReportItemItem(XElement item, Rectangle bounds, string? type)
    {
        var value = FirstBoundValue(item);
        return type switch
        {
            "DataBar" => new DataBarElement { Bounds = bounds, ValueExpression = value ?? "0" },
            "Sparkline" => new SparklineElement { Bounds = bounds, ValueExpression = value ?? "Fields.Value" },
            "Indicator" => new IndicatorElement { Bounds = bounds, ValueExpression = value ?? "0" },
            "Gauge" or "RadialGauge" or "LinearGauge" => new GaugeElement { Bounds = bounds, ValueExpression = value ?? "0" },
            _ => null,
        };
    }

    // Best-effort scan for a CustomReportItem's primary data binding: the first leaf <Value> whose text is an
    // expression (starts with '='), converted to OmniReport syntax. Covers the common
    // <…><DataValue><Value>=Fields!X.Value shape without committing to one vendor's CRI schema.
    private static string? FirstBoundValue(XElement cri)
    {
        var binding = cri.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "Value" && !e.HasElements && e.Value.TrimStart().StartsWith('='));
        return binding is null ? null : RdlExpression.Convert(binding.Value);
    }

    // RDL <Chart>: chart type from the first series' <Type>, category from the category hierarchy's first
    // group expression, and one ChartSeries per <ChartSeries> (value from its first DataValue).
    private ReportElement ChartItem(XElement item, Rectangle bounds)
    {
        var seriesEls = El(El(item, "ChartData"), "ChartSeriesCollection")?.Elements()
            .Where(e => e.Name.LocalName == "ChartSeries").ToList() ?? new List<XElement>();
        var category = RdlExpression.Convert(FirstGroupExpression(El(item, "ChartCategoryHierarchy")));
        var kind = MapChartKind(Val(seriesEls.FirstOrDefault(), "Type"));

        var series = new List<ChartSeries>();
        if (FirstGroupExpression(El(item, "ChartSeriesHierarchy")) is { Length: > 0 })
        {
            _warnings.Add($"Chart '{item.Attribute("Name")?.Value}': agrupamento dinâmico de série (ChartSeriesHierarchy) não importado (séries são achatadas).");
        }
        foreach (var s in seriesEls)
        {
            var valueRaw = TextOfFirst(El(s, "DataPoints"), "Value"); // the series' data value (scoped)
            if (string.IsNullOrEmpty(valueRaw))
            {
                continue;
            }
            series.Add(new ChartSeries(
                Name: Val(s, "SeriesName") ?? s.Attribute("Name")?.Value ?? $"Série{series.Count + 1}",
                CategoryExpression: category,
                ValueExpression: RdlExpression.Convert(valueRaw)));
        }
        if (series.Count == 0)
        {
            _warnings.Add($"Chart '{item.Attribute("Name")?.Value}': importado sem séries (estrutura ChartData não reconhecida).");
        }
        return new ChartElement
        {
            Bounds = bounds,
            Kind = kind,
            Series = new EquatableArray<ChartSeries>(series),
        };
    }

    private ReportElement GaugeItem(XElement item, Rectangle bounds)
    {
        var items = El(item, "GaugePanelItems");
        var kind = El(item, "LinearGauges") is not null || (items is not null && El(items, "LinearGauge") is not null)
            ? GaugeKind.Linear
            : GaugeKind.Radial;
        // The pointer value is the gauge's bound value. Scope to <GaugePointers> — the scale's
        // <Maximum>/<Minimum><Value> precede the pointers in RDL, so a whole-panel scan grabs the wrong one.
        var pointers = item.Descendants().FirstOrDefault(e => e.Name.LocalName == "GaugePointers");
        var value = TextOfFirst(pointers, "Value");

        // Scale Min/Max (<Maximum>/<Minimum>, optionally wrapping <Value>) — TextOfFirst concatenates the
        // descendant text, so both <Maximum>100</Maximum> and <Maximum><Value>100</Value></Maximum> work.
        // Banded ranges from <ScaleRanges><ScaleRange><StartValue>/<EndValue>/<BackgroundColor>.
        var ranges = item.Descendants().Where(e => e.Name.LocalName == "ScaleRange")
            .Select(r => new GaugeRange(
                GaugeScalar(TextOfFirst(r, "StartValue")) ?? "0",
                GaugeScalar(TextOfFirst(r, "EndValue")) ?? "0",
                (ParseColor(TextOfFirst(r, "BackgroundColor")) ?? Color.FromRgb(128, 128, 128)).ToHex()))
            .ToList();

        return new GaugeElement
        {
            Bounds = bounds,
            Kind = kind,
            ValueExpression = string.IsNullOrEmpty(value) ? "0" : RdlExpression.Convert(value),
            MinimumExpression = GaugeScalar(TextOfFirst(item, "Minimum")) ?? "0",
            MaximumExpression = GaugeScalar(TextOfFirst(item, "Maximum")) ?? "100",
            Ranges = new EquatableArray<GaugeRange>(ranges),
        };
    }

    // An RDL gauge scale value (Min/Max/range bound): an =expression is converted; a plain number is kept.
    private static string? GaugeScalar(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null
            : RdlExpression.IsExpression(raw) ? RdlExpression.Convert(raw) : raw.Trim();

    private static ReportElement SubreportItem(XElement item, Rectangle bounds)
    {
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in El(item, "Parameters")?.Elements().Where(e => e.Name.LocalName == "Parameter")
                 ?? Enumerable.Empty<XElement>())
        {
            if (p.Attribute("Name")?.Value is { Length: > 0 } pn)
            {
                bindings[pn] = RdlExpression.Convert(Val(p, "Value"));
            }
        }
        return new SubreportElement
        {
            Bounds = bounds,
            ReportId = Val(item, "ReportName"),
            ParameterBindings = new EquatableDictionary<string, string>(bindings),
        };
    }

    private static ChartKind MapChartKind(string? rdlType) => rdlType switch
    {
        "Line" => ChartKind.Line,
        "Area" => ChartKind.Area,
        "Shape" => ChartKind.Pie, // RDL pie/doughnut series are Type=Shape
        "Scatter" => ChartKind.Scatter,
        "Bubble" => ChartKind.Bubble,
        "Polar" or "Radar" => ChartKind.Radar,
        "Stock" or "Range" => ChartKind.Stock,
        _ => ChartKind.Bar, // Column/Bar and unknowns → Bar
    };

    // The text of the first descendant <Value> (namespace-agnostic) under the given scope.
    private static string? TextOfFirst(XElement? scope, string localName)
        => scope?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static string? FirstGroupExpression(XElement? hierarchy)
        => TextOfFirst(hierarchy, "GroupExpression");

    // Maps an RDL <Tablix> to OmniReport's TablixElement. First cut: the matrix/crosstab path — dynamic
    // row + column group hierarchies + the body value cell + corner. Static-column tables and per-cell
    // spans are follow-ups; a warning is recorded (never silent) when the Tablix isn't a clean matrix.
    private ReportElement TablixItem(XElement item, Rectangle bounds)
    {
        var name = item.Attribute("Name")?.Value ?? "Tablix";
        var rowGroups = ReadTablixGroups(El(El(item, "TablixRowHierarchy"), "TablixMembers"), "Rows");
        var colGroups = ReadTablixGroups(El(El(item, "TablixColumnHierarchy"), "TablixMembers"), "Cols");

        // Pure flat table (RDL Table/List): no DYNAMIC group on either axis (static columns + a Details row).
        // The model/renderer already support this shape (Cells row 0 = header, row 1 = detail); map the grid.
        if (rowGroups.Count == 0 && colGroups.Count == 0)
        {
            return FlatTableTablix(item, bounds, name);
        }

        var cells = new List<TablixCell>();
        var cornerRaw = TextboxValue(FirstTextbox(El(item, "TablixCorner")));
        if (!string.IsNullOrEmpty(cornerRaw))
        {
            cells.Add(new TablixCell(0, 0, new LabelElement { Text = cornerRaw, Bounds = Rectangle.Empty }));
        }
        var bodyTextbox = FirstTextbox(El(item, "TablixBody"));
        var bodyRaw = TextboxValue(bodyTextbox);
        if (!string.IsNullOrEmpty(bodyRaw))
        {
            // Carry the body textbox's <Style> so the matrix value cell keeps its RDL Format (currency,
            // percent, …) instead of falling back to the renderer's N2 default.
            cells.Add(new TablixCell(1, 1, new TextBoxElement
            {
                Expression = RdlExpression.Convert(bodyRaw),
                Bounds = Rectangle.Empty,
                Style = bodyTextbox is null ? Style.Default : ReadStyle(bodyTextbox) ?? Style.Default,
            }));
        }

        if (rowGroups.Count == 0 || colGroups.Count == 0)
        {
            // Pure flat tables are handled above; reaching here means exactly one axis is dynamic and the
            // other static — a table+matrix hybrid, which is a follow-up.
            _warnings.Add($"Tablix '{name}': importado parcialmente — híbrido tabela+matrix (um eixo com grupo dinâmico e outro estático) é follow-up.");
        }

        return new TablixElement
        {
            Bounds = bounds,
            DataSetName = Val(item, "DataSetName"),
            NoRowsMessage = NoRowsOf(item),
            RowGroups = new EquatableArray<TablixGroup>(rowGroups),
            ColumnGroups = new EquatableArray<TablixGroup>(colGroups),
            Cells = new EquatableArray<TablixCell>(cells),
            RowSubtotals = HasSubtotalMember(El(El(item, "TablixRowHierarchy"), "TablixMembers")),
            ColumnSubtotals = HasSubtotalMember(El(El(item, "TablixColumnHierarchy"), "TablixMembers")),
        };
    }

    // SSRS emits a group total as an EMPTY <Group/> member — no GroupExpression AND no Name — sitting as a
    // SIBLING of the dynamic group member at the same nesting level. A level that holds BOTH a dynamic group
    // and such a total member signals a subtotal on that axis. Detection is deliberately CONSERVATIVE: a named
    // static group (<Group Name="Details"/> = detail rows) and a label member (no <Group> at all) are NOT
    // totals, so they never produce a false positive (worst case is a missed total, which imports cleanly).
    private static bool HasSubtotalMember(XElement? members)
    {
        if (members is null)
        {
            return false;
        }
        bool anyDynamic = false, anyTotal = false;
        foreach (var m in members.Elements().Where(e => e.Name.LocalName == "TablixMember"))
        {
            var group = El(m, "Group");
            bool isDynamic = !string.IsNullOrEmpty(Val(El(group, "GroupExpressions"), "GroupExpression"));
            if (isDynamic)
            {
                anyDynamic = true;
            }
            else if (group is not null && string.IsNullOrEmpty(group.Attribute("Name")?.Value))
            {
                anyTotal = true; // empty <Group/> with no Name = the static total member
            }
            if (HasSubtotalMember(El(m, "TablixMembers"))) // nested levels (inner group totals)
            {
                return true;
            }
        }
        return anyDynamic && anyTotal;
    }

    // RDL <NoRowsMessage> on a data region (literal or =expression) → the message shown for an empty dataset.
    private static string? NoRowsOf(XElement item)
        => Val(item, "NoRowsMessage") is { Length: > 0 } m ? RdlExpression.Convert(m) : null;

    // Imports an RDL flat Table/List (static columns + a Details row) into the TablixElement table shape the
    // renderer already understands: Cells (0,c) = header Label, (1,c) = detail TextBox, RowGroups/ColumnGroups
    // empty, ColumnWidths = the RDL column widths (relative weights). Header vs detail rows are classified by
    // the row hierarchy (the member with a <Group> is the Details/detail row), falling back to position.
    // Scope limits (acceptable for the common 1-header-1-detail table): a Details member nested under a static
    // parent isn't classified (positional fallback covers the 2-row case); a trailing column empty in BOTH
    // rows is dropped (it carries no value); multiple detail/header rows collapse to one each.
    private ReportElement FlatTableTablix(XElement item, Rectangle bounds, string name)
    {
        var body = El(item, "TablixBody");
        var bodyRows = (El(body, "TablixRows")?.Elements().Where(e => e.Name.LocalName == "TablixRow")
            ?? Enumerable.Empty<XElement>()).ToList();
        var widths = (El(body, "TablixColumns")?.Elements().Where(e => e.Name.LocalName == "TablixColumn")
            ?? Enumerable.Empty<XElement>())
            .Select(c => ParseSize(Val(c, "Width"))?.ToMm() ?? 0.0).ToList();

        // Classify: the row-hierarchy member with a <Group> (Details, even without GroupExpression) is the
        // detail row; the static member before it is the header. No hierarchy → positional (last = detail).
        var rowMembers = (El(El(item, "TablixRowHierarchy"), "TablixMembers")?.Elements()
            .Where(e => e.Name.LocalName == "TablixMember") ?? Enumerable.Empty<XElement>()).ToList();
        int detailIdx = rowMembers.FindIndex(m => El(m, "Group") is not null);
        XElement? headerRow, detailRow;
        if (detailIdx >= 0 && detailIdx < bodyRows.Count)
        {
            detailRow = bodyRows[detailIdx];
            headerRow = detailIdx > 0 ? bodyRows[detailIdx - 1] : null;
        }
        else
        {
            detailRow = bodyRows.Count >= 1 ? bodyRows[^1] : null;
            headerRow = bodyRows.Count >= 2 ? bodyRows[0] : null;
        }

        var cells = new List<TablixCell>();
        if (headerRow is not null)
        {
            int col = 0; // RDL <ColSpan> pushes the next cell spanColumns over, not 1.
            foreach (var hcell in RowCells(headerRow))
            {
                int span = ColSpanOf(hcell);
                if (TextboxValue(FirstTextbox(hcell)) is { Length: > 0 } v)
                {
                    cells.Add(new TablixCell(0, col, new LabelElement { Text = v, Bounds = Rectangle.Empty }, ColumnSpan: span));
                }
                col += span;
            }
        }
        if (detailRow is not null)
        {
            int col = 0;
            foreach (var dcell in RowCells(detailRow))
            {
                int span = ColSpanOf(dcell);
                var tb = FirstTextbox(dcell);
                if (TextboxValue(tb) is { Length: > 0 } v)
                {
                    cells.Add(new TablixCell(1, col, new TextBoxElement
                    {
                        Expression = RdlExpression.Convert(v),
                        Bounds = Rectangle.Empty,
                        Style = tb is null ? Style.Default : ReadStyle(tb) ?? Style.Default,
                    }, ColumnSpan: span));
                }
                col += span;
            }
        }
        if (cells.Count == 0)
        {
            _warnings.Add($"Tablix '{name}': tabela sem células de texto reconhecíveis — importada vazia.");
        }

        return new TablixElement
        {
            Bounds = bounds,
            DataSetName = Val(item, "DataSetName"),
            NoRowsMessage = NoRowsOf(item),
            Cells = new EquatableArray<TablixCell>(cells),
            // RDL widths are absolute; the renderer treats ColumnWidths as relative weights, preserving ratios.
            ColumnWidths = widths.Count >= 2 && widths.Any(w => w > 0)
                ? new EquatableArray<double>(widths)
                : EquatableArray<double>.Empty,
        };
    }

    // First-cut Tablix→bands: when the Body is EXACTLY one flat Tablix (no dynamic row/col groups) and the
    // page has no <PageHeader>, decompose it into a repeating column-header band (PageHeader) + a DetailBand
    // with one positioned TextBox per column, instead of one monolithic TablixElement inside the ReportHeader.
    // The detail band paginates row-by-row and the header repeats per page — like a native banded report.
    // Bounds are absolute per column (RDL column widths are absolute, anchored at the Tablix's Left). Anything
    // that doesn't match (extra Body items, dynamic groups, no columns, no detail row, an existing PageHeader)
    // returns false so the caller keeps the existing TablixElement path (with its own warning).
    private bool TryFlatTablixBands(XElement? body, bool pageHasHeader, out ReportBand? headerBand, out DetailBand? detail)
    {
        headerBand = null;
        detail = null;
        var items = El(body, "ReportItems")?.Elements().ToList() ?? new List<XElement>();
        if (pageHasHeader || items.Count != 1 || items[0].Name.LocalName != "Tablix")
        {
            return false;
        }
        var tablix = items[0];
        if (ReadTablixGroups(El(El(tablix, "TablixRowHierarchy"), "TablixMembers"), "Rows").Count != 0
            || ReadTablixGroups(El(El(tablix, "TablixColumnHierarchy"), "TablixMembers"), "Cols").Count != 0)
        {
            return false;
        }

        var tbBounds = TablixBounds(tablix, Unit.Zero, Unit.Zero);
        var tablixBody = El(tablix, "TablixBody");
        var bodyRows = (El(tablixBody, "TablixRows")?.Elements().Where(e => e.Name.LocalName == "TablixRow")
            ?? Enumerable.Empty<XElement>()).ToList();
        var colWidths = (El(tablixBody, "TablixColumns")?.Elements().Where(e => e.Name.LocalName == "TablixColumn")
            ?? Enumerable.Empty<XElement>())
            .Select(c => ParseSize(Val(c, "Width")) ?? Unit.Zero).ToList();
        if (colWidths.Count == 0)
        {
            return false; // no column geometry → can't place cells; keep the TablixElement path
        }

        // Classify header/detail rows the same way FlatTableTablix does (the member with a <Group> = detail).
        var rowMembers = (El(El(tablix, "TablixRowHierarchy"), "TablixMembers")?.Elements()
            .Where(e => e.Name.LocalName == "TablixMember") ?? Enumerable.Empty<XElement>()).ToList();
        int detailIdx = rowMembers.FindIndex(m => El(m, "Group") is not null);
        XElement? headerRow, detailRow;
        if (detailIdx >= 0 && detailIdx < bodyRows.Count)
        {
            detailRow = bodyRows[detailIdx];
            headerRow = detailIdx > 0 ? bodyRows[detailIdx - 1] : null;
        }
        else
        {
            detailRow = bodyRows.Count >= 1 ? bodyRows[^1] : null;
            headerRow = bodyRows.Count >= 2 ? bodyRows[0] : null;
        }
        if (detailRow is null)
        {
            return false;
        }

        // Column X edges, anchored at the Tablix's Left. The columns are scaled to fill the Tablix's declared
        // width exactly — the RDL widths act as relative weights, matching how the old TablixElement render
        // (ComputeColumnEdges) fit them into the element's bounds. This keeps the table inside its rectangle
        // (no overflow past the page) regardless of the absolute width sum.
        double widthSumMm = colWidths.Sum(w => w.ToMm());
        double scale = widthSumMm > 0 ? tbBounds.Width.ToMm() / widthSumMm : 1.0;
        var edges = new Unit[colWidths.Count + 1];
        edges[0] = tbBounds.X;
        double accMm = 0;
        for (int c = 0; c < colWidths.Count; c++)
        {
            accMm += colWidths[c].ToMm() * scale;
            edges[c + 1] = tbBounds.X + Unit.FromMm(accMm);
        }
        // Width spanning columns [col, col+span) from the precomputed edges (honours RDL ColSpan).
        Unit SpanW(int col, int span) => edges[col + span] - edges[col];

        if (headerRow is not null)
        {
            var hHeight = ParseSize(Val(headerRow, "Height")) ?? Unit.FromMm(6);
            var hels = new List<ReportElement>();
            int col = 0;
            foreach (var hcell in RowCells(headerRow))
            {
                if (col >= colWidths.Count)
                {
                    break;
                }
                int span = Math.Clamp(ColSpanOf(hcell), 1, colWidths.Count - col);
                var tb = FirstTextbox(hcell);
                if (TextboxValue(tb) is { Length: > 0 } v)
                {
                    hels.Add(new LabelElement
                    {
                        Text = v,
                        Bounds = new Rectangle(edges[col], Unit.Zero, SpanW(col, span), hHeight),
                        Style = tb is null ? Style.Default : ReadStyle(tb) ?? Style.Default,
                    });
                }
                col += span;
            }
            if (hels.Count > 0)
            {
                headerBand = new ReportBand(BandKind.PageHeader, hHeight, new EquatableArray<ReportElement>(hels));
            }
        }

        var dHeight = ParseSize(Val(detailRow, "Height")) ?? Unit.FromMm(6);
        var dels = new List<ReportElement>();
        int dcol = 0;
        foreach (var dcell in RowCells(detailRow))
        {
            if (dcol >= colWidths.Count)
            {
                break;
            }
            int span = Math.Clamp(ColSpanOf(dcell), 1, colWidths.Count - dcol);
            var tb = FirstTextbox(dcell);
            if (TextboxValue(tb) is { Length: > 0 } v)
            {
                dels.Add(new TextBoxElement
                {
                    Expression = RdlExpression.Convert(v),
                    Bounds = new Rectangle(edges[dcol], Unit.Zero, SpanW(dcol, span), dHeight),
                    Style = tb is null ? Style.Default : ReadStyle(tb) ?? Style.Default,
                });
            }
            dcol += span;
        }
        if (dels.Count == 0)
        {
            return false; // nothing renderable in the detail → keep the TablixElement path (which warns)
        }
        detail = new DetailBand(dHeight, new EquatableArray<ReportElement>(dels))
        {
            DataSetName = Val(tablix, "DataSetName"),
            NoRowsMessage = NoRowsOf(tablix),
            PageBreak = ReadPageBreak(tablix),
        };
        return true;
    }

    // RDL <TablixCell><ColSpan> (optional, default 1) — how many columns the cell covers. RowSpan is implicit
    // in RDL (covered cells are omitted from later rows) and not inferred here.
    private static int ColSpanOf(XElement cell)
        => int.TryParse(Val(cell, "ColSpan"), out var n) && n > 1 ? n : 1;

    private static List<XElement> RowCells(XElement tablixRow)
        => (El(tablixRow, "TablixCells")?.Elements().Where(e => e.Name.LocalName == "TablixCell")
            ?? Enumerable.Empty<XElement>()).ToList();

    // Walks a TablixMembers tree (outer→inner), collecting each member that carries a <Group> with a
    // <GroupExpression> into a TablixGroup (with the member's optional first SortExpression). Static
    // members (no Group) are skipped. Nested <TablixMembers> become deeper group levels.
    private static List<TablixGroup> ReadTablixGroups(XElement? members, string prefix)
    {
        var list = new List<TablixGroup>();
        Walk(members);
        return list;

        void Walk(XElement? ms)
        {
            foreach (var m in ms?.Elements().Where(e => e.Name.LocalName == "TablixMember") ?? Enumerable.Empty<XElement>())
            {
                var group = El(m, "Group");
                var expr = Val(El(group, "GroupExpressions"), "GroupExpression");
                if (group is not null && !string.IsNullOrEmpty(expr))
                {
                    var sortEl = El(El(m, "SortExpressions"), "SortExpression");
                    var sortRaw = Val(sortEl, "Value");
                    var sort = string.IsNullOrEmpty(sortRaw) ? null : RdlExpression.Convert(sortRaw);
                    var desc = string.Equals(Val(sortEl, "Direction"), "Descending", StringComparison.OrdinalIgnoreCase);
                    list.Add(new TablixGroup($"{prefix}{list.Count}", RdlExpression.Convert(expr), sort, desc));
                }
                Walk(El(m, "TablixMembers")); // nested member → deeper group level
            }
        }
    }

    private static XElement? FirstTextbox(XElement? scope)
        => scope?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Textbox");

    // Applies the report-item attributes common to every element — the RDL <Style>, Visibility/Hidden,
    // Bookmark, DocumentMapLabel and Action — that the model already supports but the importer ignored.
    private static ReportElement ApplyCommon(ReportElement el, XElement item)
    {
        var style = ReadStyle(item);
        var (visible, visExpr) = ReadVisibility(item);
        var bookmark = ConvertOpt(Val(item, "Bookmark"));
        var docMap = ConvertOpt(Val(item, "DocumentMapLabel"));
        var action = ReadAction(El(item, "Action"));
        // The RDL item Name identifies the element for ReportItems!Name.Value references — capture it
        // (was previously dropped). Applied after the per-type arm so every element kind keeps it.
        var name = item.Attribute("Name")?.Value;
        var withCommon = el switch
        {
            TextBoxElement t => t with { Style = style ?? t.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            LabelElement l => l with { Style = style ?? l.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            RectangleElement r => r with { Style = style ?? r.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            ImageElement im => im with { Style = style ?? im.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            // A line's color/width come from its style border; map the first visible side to the Pen.
            LineElement ln => ln with { Pen = StyleBorderToPen(style) ?? ln.Pen, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            TablixElement tx => tx with { Style = style ?? tx.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            ChartElement ch => ch with { Style = style ?? ch.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            GaugeElement g => g with { Style = style ?? g.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            SubreportElement sr => sr with { Style = style ?? sr.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            // INVARIANT: every element type AddItem can produce must have an arm above — otherwise its
            // Style/Visibility/Bookmark/Action would be silently dropped. Keep this in sync with AddItem.
            _ => el,
        };
        // Style properties whose RDL value is an EXPRESSION (conditional formatting: negative-in-red, zebra,
        // threshold colouring) map to per-property bindings — previously these were silently dropped because
        // ReadStyle only parses literals. PropertyExpressions already round-trips and renders in every mode.
        var styleBindings = ReadStyleExpressions(item);
        if (styleBindings.Count > 0)
        {
            withCommon = withCommon with { PropertyExpressions = new EquatableDictionary<string, string>(styleBindings) };
        }
        // Model fields with no native RDL slot are restored from the element's <CustomProperties> (the inverse of
        // RdlWriter.WriteCustomProperties) — keeping the RDL round-trip lossless without leaving the XSD.
        var custom = ReadElementCustomProperties(item);
        if (custom.Count > 0)
        {
            withCommon = ApplyCustomProperties(withCommon, custom);
        }
        return string.IsNullOrEmpty(name) ? withCommon : withCommon with { Name = name };
    }

    // The element's own (direct-child) <CustomProperties> as a Name→Value map. Direct-child only (El), so it never
    // captures a nested item's properties nor the report-level ones (those go to Metadata via ReadMetadata).
    private static Dictionary<string, string> ReadElementCustomProperties(XElement item)
    {
        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cp in El(item, "CustomProperties")?.Elements().Where(e => e.Name.LocalName == "CustomProperty")
                 ?? Enumerable.Empty<XElement>())
        {
            var name = Val(cp, "Name");
            if (!string.IsNullOrEmpty(name))
            {
                props[name] = Val(cp, "Value") ?? string.Empty;
            }
        }
        return props;
    }

    private static ReportElement ApplyCustomProperties(ReportElement el, Dictionary<string, string> props)
    {
        switch (el)
        {
            case SubreportElement sr:
                if (props.TryGetValue("omni:DataExpression", out var de) && !string.IsNullOrEmpty(de))
                {
                    sr = sr with { DataExpression = de };
                }
                if (props.TryGetValue("omni:InlineDefinition", out var inl) && !string.IsNullOrEmpty(inl))
                {
                    // ReportId here is the synthetic placeholder ReportName the exporter emitted for XSD validity;
                    // clear it, since InlineDefinition and ReportId are mutually exclusive.
                    sr = sr with { InlineDefinition = DeserializeInline(inl), ReportId = null };
                }
                return sr;
            case TablixElement tx:
                if (props.TryGetValue("omni:SubtotalLabel", out var sl))
                {
                    tx = tx with { SubtotalLabel = sl };
                }
                if (props.TryGetValue("omni:GrandTotalLabel", out var gl))
                {
                    tx = tx with { GrandTotalLabel = gl };
                }
                return tx;
            default:
                return el;
        }
    }

    private static ReportDefinition DeserializeInline(string json)
    {
        using var ms = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return new RepJsonSerializer().Load(ms);
    }

    // RDL <Style> sub-properties whose value is an =expression → OmniReport PropertyExpressions (dotted path →
    // converted expr), so conditional formatting renders/round-trips. Only paths the renderer coerces reliably
    // are mapped; colour expressions render when they yield #hex (named-colour coercion is a render follow-up).
    private static Dictionary<string, string> ReadStyleExpressions(XElement item)
    {
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        var s = El(item, "Style");
        if (s is null)
        {
            return bindings;
        }
        foreach (var (rdlProp, path) in StyleExpressionPaths)
        {
            var raw = Val(s, rdlProp);
            if (!string.IsNullOrEmpty(raw) && RdlExpression.IsExpression(raw))
            {
                bindings[path] = RdlExpression.Convert(raw);
            }
        }
        return bindings;
    }

    private static readonly (string Rdl, string Path)[] StyleExpressionPaths =
    {
        ("Color", "Style.ForeColor"),
        ("BackgroundColor", "Style.BackColor"),
        ("Format", "Style.Format"),
        ("TextAlign", "Style.HorizontalAlignment"),
        ("VerticalAlign", "Style.VerticalAlignment"),
        ("FontFamily", "Style.Font.Family"),
    };

    // RDL page break on a data region/band → OmniReport PageBreak. RDL 2008+ uses
    // <PageBreak><BreakLocation>Start|End|StartAndEnd|Between</BreakLocation></PageBreak>; the 2005 legacy
    // uses the booleans <PageBreakAtStart>/<PageBreakAtEnd>. Absent/unknown → None.
    private static PageBreak ReadPageBreak(XElement? region)
    {
        if (region is null)
        {
            return PageBreak.None;
        }
        if (Val(El(region, "PageBreak"), "BreakLocation") is { Length: > 0 } loc)
        {
            return loc switch
            {
                "Start" => PageBreak.Start,
                "End" => PageBreak.End,
                "StartAndEnd" => PageBreak.StartAndEnd,
                "Between" => PageBreak.Between,
                _ => PageBreak.None,
            };
        }
        return (ParseBool(Val(region, "PageBreakAtStart")), ParseBool(Val(region, "PageBreakAtEnd"))) switch
        {
            (true, true) => PageBreak.StartAndEnd,
            (true, false) => PageBreak.Start,
            (false, true) => PageBreak.End,
            _ => PageBreak.None,
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

    private ReportElement TextItem(XElement item, Rectangle bounds)
    {
        var (runs, fallback) = ReadTextRuns(item);
        // A single run (or RDL-2008 direct <Value>) keeps the legacy single-expression / label path,
        // byte-identical to before. Multiple runs (mixed formatting in one box) populate TextRuns + a
        // concatenation-template fallback Expression for non-run-aware consumers.
        if (runs.Count <= 1)
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
        return new TextBoxElement
        {
            Expression = fallback,
            Bounds = bounds,
            CanGrow = ParseBool(Val(item, "CanGrow")),
            CanShrink = ParseBool(Val(item, "CanShrink")),
            TextRuns = new EquatableArray<TextRun>(runs),
        };
    }

    private static bool ParseBool(string? raw) => bool.TryParse(raw, out var b) && b;

    // RDL <Line> has no Direction element — it's implied by the bounding box: a near-zero height is a
    // horizontal ruler, a near-zero width a vertical one, otherwise the diagonal (RDL's top-left→bottom-right
    // for a positive box). Without this every imported line defaults to diagonal. The Pen comes from
    // ApplyCommon (StyleBorderToPen). The other diagonal (BottomLeftToTopRight) isn't encoded in RDL.
    private static ReportElement LineItem(Rectangle bounds)
    {
        const double flatMm = 0.5; // below this a dimension counts as "zero" for a ruler line
        double w = bounds.Width.ToMm(), h = bounds.Height.ToMm();
        var dir = h < flatMm && h <= w ? LineDirection.Horizontal
            : w < flatMm && w < h ? LineDirection.Vertical
            : LineDirection.TopLeftToBottomRight;
        return new LineElement { Bounds = bounds, Direction = dir };
    }

    // Doubles braces so literal RDL text renders verbatim through the template renderer ({ → {{, } → }}).
    private static string EscapeBraces(string? literal) => (literal ?? string.Empty).Replace("{", "{{").Replace("}", "}}");

    // Reads every <Paragraph>/<TextRun> of an RDL Textbox into model TextRuns (Value + per-run Style +
    // per-run inline Action), plus a fallback Expression that concatenates the runs as a template (literal
    // runs verbatim, expression runs as "{expr}") so a non-run-aware consumer still renders the whole text.
    // Paragraphs are separated by a newline run. <MarkupType>HTML</MarkupType> is flattened with a warning.
    private (List<TextRun> Runs, string Fallback) ReadTextRuns(XElement textbox)
    {
        var runs = new List<TextRun>();
        var fb = new System.Text.StringBuilder();
        var paragraphs = (El(textbox, "Paragraphs")?.Elements().Where(e => e.Name.LocalName == "Paragraph")
            ?? Enumerable.Empty<XElement>()).ToList();
        for (int p = 0; p < paragraphs.Count; p++)
        {
            if (p > 0)
            {
                runs.Add(new TextRun("\n"));
                fb.Append('\n');
            }
            foreach (var run in El(paragraphs[p], "TextRuns")?.Elements().Where(e => e.Name.LocalName == "TextRun")
                ?? Enumerable.Empty<XElement>())
            {
                var rawVal = Val(run, "Value");
                var isExpr = RdlExpression.IsExpression(rawVal);
                // A run's Value carries template semantics (same as Expression), so an RDL *literal* with a
                // brace must be escaped ({{ }}) or it would be mis-read as a placeholder — both in the per-run
                // render path and in the fallback template. Expression runs keep their converted form.
                var value = isExpr ? RdlExpression.Convert(rawVal) : EscapeBraces(rawVal);
                var action = ReadAction(El(El(El(run, "ActionInfo"), "Actions"), "Action"));
                runs.Add(new TextRun(value, ReadStyle(run), action));
                fb.Append(isExpr ? "{" + value + "}" : value);
                if (string.Equals(Val(run, "MarkupType"), "HTML", StringComparison.OrdinalIgnoreCase))
                {
                    _warnings.Add($"Textbox '{textbox.Attribute("Name")?.Value}': TextRun MarkupType=HTML importado como texto plano (rich text HTML é follow-up).");
                }
            }
        }
        return (runs, fb.ToString());
    }

    // RDL 2016 nests the value under Paragraphs/Paragraph/TextRuns/TextRun/Value; RDL 2008 uses a direct
    // <Value>. Returns the first value found.
    private static string? TextboxValue(XElement? textbox)
    {
        if (textbox is null)
        {
            return null;
        }
        var run = El(El(El(El(textbox, "Paragraphs"), "Paragraph"), "TextRuns"), "TextRun");
        return Val(run, "Value") ?? Val(textbox, "Value");
    }

    private ReportElement ImageItem(XElement item, Rectangle bounds)
    {
        var source = Val(item, "Source");
        var value = Val(item, "Value");
        var sizing = ParseSizing(Val(item, "Sizing"));
        if (string.Equals(source, "External", StringComparison.OrdinalIgnoreCase))
        {
            return RdlExpression.IsExpression(value)
                ? new ImageElement { Source = ImageSourceKind.Expression, Expression = RdlExpression.Convert(value), Bounds = bounds, Sizing = sizing }
                : new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds, Sizing = sizing };
        }
        if (string.Equals(source, "Embedded", StringComparison.OrdinalIgnoreCase)
            && value is { Length: > 0 } && _embeddedImages.TryGetValue(value, out var bytes))
        {
            return new ImageElement { Source = ImageSourceKind.Inline, InlineData = new EquatableArray<byte>(bytes), Bounds = bounds, Sizing = sizing };
        }
        // Database: <Value> is an expression yielding the image bytes (a binary field). Map to an Expression
        // source — the renderer's ResolveExpression already turns a byte[] result into the drawn image.
        if (string.Equals(source, "Database", StringComparison.OrdinalIgnoreCase) && value is { Length: > 0 })
        {
            return new ImageElement { Source = ImageSourceKind.Expression, Expression = RdlExpression.Convert(value), Bounds = bounds, Sizing = sizing };
        }
        // Unresolved embedded name (no matching <EmbeddedImage>) → keep the name as the path.
        return new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds, Sizing = sizing };
    }

    // RDL <Sizing> → OmniReport ImageSizing. RDL "Fit" stretches to the box (distorts) → Stretch;
    // "FitProportional" preserves aspect (letterbox) → Fit; "Clip" is native size clipped → Native.
    // "AutoSize" (the RDL default, which grows the report item to the image) has no fixed-bounds equivalent,
    // so it — like an absent/unknown value — falls back to the model default Fit (whole image, no distortion).
    private static ImageSizing ParseSizing(string? raw) => raw switch
    {
        "Fit" => ImageSizing.Stretch,
        "FitProportional" => ImageSizing.Fit,
        "Clip" => ImageSizing.Native,
        _ => ImageSizing.Fit,
    };

    // Instance (not static) so a dropped DefaultValue can be reported via _warnings — the importer never loses
    // a value silently. (Called as a method group: .Select(ReadParameter).)
    private ReportParameter ReadParameter(XElement el)
    {
        var name = el.Attribute("Name")?.Value ?? throw new FormatException("ReportParameter missing Name.");
        var type = MapType(Val(el, "DataType"));
        var prompt = Val(el, "Prompt");
        var nullable = string.Equals(Val(el, "Nullable"), "true", StringComparison.OrdinalIgnoreCase);
        var allowBlank = string.Equals(Val(el, "AllowBlank"), "true", StringComparison.OrdinalIgnoreCase);
        var hidden = string.Equals(Val(el, "Hidden"), "true", StringComparison.OrdinalIgnoreCase);
        var multiValue = string.Equals(Val(el, "MultiValue"), "true", StringComparison.OrdinalIgnoreCase);

        var defaultEl = El(el, "DefaultValue");
        object? defaultValue = null;
        string? defaultExpression = null;
        var defaultRaw = El(El(defaultEl, "Values"), "Value")?.Value;
        if (!string.IsNullOrEmpty(defaultRaw))
        {
            if (RdlExpression.IsExpression(defaultRaw))
            {
                // An =expression default (=Today(), =Parameters!X.Value) is preserved as DefaultValueExpression
                // (OmniReport syntax) and evaluated at run start to seed the value.
                defaultExpression = RdlExpression.Convert(defaultRaw);
            }
            else if (TryParseScalar(defaultRaw, type, out defaultValue))
            {
                // parsed
            }
            else
            {
                // Strict invariant parse failed (wrong type, or a locale-formatted number like "3,14" that the
                // old Convert.ChangeType would MISread as 314) — drop with a warning, never a silent/wrong value.
                _warnings.Add($"ReportParameter '{name}': DefaultValue '{defaultRaw}' não pôde ser convertido para {type.Name} (cultura invariante) — importado sem default.");
            }
        }

        ParameterAvailableValues? available = ReadAvailableValues(El(el, "ValidValues"));

        // RDL: a parameter is required only when it's neither nullable nor has a default supplied.
        var required = !nullable && defaultEl is null;
        return new ReportParameter(name, type, prompt, defaultValue, multiValue, required, available,
            Nullable: nullable, AllowBlank: allowBlank, Hidden: hidden)
        {
            DefaultValueExpression = defaultExpression,
        };
    }

    // Strict invariant parse of a literal default into the parameter's CLR type. Unlike Convert.ChangeType,
    // numbers reject a thousands separator (so a comma-decimal "3,14" fails rather than silently becoming 314),
    // and DateTime parses the ISO-8601 form the exporter emits. Returns false (value=null) on any mismatch.
    private static bool TryParseScalar(string raw, Type type, out object? value)
    {
        if (type == typeof(int) && int.TryParse(raw, NumberStyles.Integer, Inv, out var i)) { value = i; return true; }
        if (type == typeof(double) && double.TryParse(raw, NumberStyles.Float, Inv, out var d)) { value = d; return true; }
        if (type == typeof(decimal) && decimal.TryParse(raw, NumberStyles.Float, Inv, out var m)) { value = m; return true; }
        if (type == typeof(bool) && bool.TryParse(raw, out var b)) { value = b; return true; }
        if (type == typeof(DateTime) && DateTime.TryParse(raw, Inv, DateTimeStyles.RoundtripKind, out var dt)) { value = dt; return true; }
        if (type == typeof(string)) { value = raw; return true; }
        value = null;
        return false;
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
            WordWrap: ParseWrapMode(Val(s, "WrapMode")),
            Format: Val(s, "Format") is { Length: > 0 } fmt ? fmt : null,
            BackgroundImage: ReadBackgroundImage(s));
        return style == Style.Default ? null : style;
    }

    // RDL <Style><BackgroundImage><Source>External</Source><Value>path-or-=expr</Value></...>. Phase B maps
    // the External source (a literal path/URL, or an =expression) to the stretched background. Embedded/Database
    // backgrounds and <BackgroundRepeat>/tiling are a follow-up (phase C).
    private static BackgroundImage? ReadBackgroundImage(XElement style)
    {
        var bg = El(style, "BackgroundImage");
        var value = Val(bg, "Value");
        if (bg is null || string.IsNullOrEmpty(value)
            || !string.Equals(Val(bg, "Source"), "External", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return RdlExpression.IsExpression(value)
            ? new BackgroundImage(Expression: RdlExpression.Convert(value))
            : new BackgroundImage(Path: value);
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

    // RDL <WrapMode>: only "NoWrap" disables wrapping; "WordWrap"/absent → wrap (the model default).
    private static bool ParseWrapMode(string? a)
        => !string.Equals(a, "NoWrap", StringComparison.OrdinalIgnoreCase);

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
        // Named colours share one map with the renderer's expression-binding coercion (Color.FromName).
        return Color.FromName(c);
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

    // RDL <Tablix> carries NO <Width>/<Height> of its own — its extent is the sum of its column widths and
    // row heights. Plain Bounds() would fall back to a tiny 25×6 mm box, collapsing every column onto the same
    // X (text overlapping). Derive the real extent from the Tablix geometry; fall back to the declared value
    // when one IS present (some tools emit it).
    private static Rectangle TablixBounds(XElement item, Unit dx, Unit dy)
    {
        var b = Bounds(item, dx, dy);
        var body = El(item, "TablixBody");
        var colSum = SumChildSizes(El(body, "TablixColumns"), "TablixColumn", "Width");
        var rowSum = SumChildSizes(El(body, "TablixRows"), "TablixRow", "Height");
        var width = Val(item, "Width") is null && colSum > Unit.Zero ? colSum : b.Width;
        var height = Val(item, "Height") is null && rowSum > Unit.Zero ? rowSum : b.Height;
        return new Rectangle(b.X, b.Y, width, height);
    }

    private static Unit SumChildSizes(XElement? parent, string childName, string sizeName)
    {
        if (parent is null)
        {
            return Unit.Zero;
        }
        Unit sum = Unit.Zero;
        foreach (var c in parent.Elements().Where(e => e.Name.LocalName == childName))
        {
            sum += ParseSize(Val(c, sizeName)) ?? Unit.Zero;
        }
        return sum;
    }

    private static Unit BoundsHeight(IEnumerable<ReportElement> elements)
    {
        Unit max = Unit.Zero;
        foreach (var e in elements)
        {
            if (e.Bounds.Bottom > max) { max = e.Bounds.Bottom; }
            // A container rectangle's children are positioned relative to it; a child overflowing the rect
            // still extends the band (parity with the legacy flattened siblings) for the no-<Height> fallback.
            if (e is RectangleElement { Children.Count: > 0 } rect)
            {
                var childExtent = e.Bounds.Y + BoundsHeight(rect.Children);
                if (childExtent > max) { max = childExtent; }
            }
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
