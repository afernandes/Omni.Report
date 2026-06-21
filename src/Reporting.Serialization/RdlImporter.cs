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
            Metadata = new EquatableDictionary<string, string>(MetadataWithWarnings(report)),
            Variables = new EquatableArray<ReportVariable>(ReadVariables(report)),
            DataSources = new EquatableArray<DataSourceDefinition>(ReadDataSets(report)),
        };
    }

    // RDL <DataSets><DataSet Name> → DataSourceDefinition (the binding metadata; query execution stays
    // delegated to the host's IReportDataSource). Maps Fields/CalculatedFields, structured <Filters> to a
    // boolean FilterExpression, <SortExpressions>, and preserves CommandText/QueryParameters in Parameters
    // (a dedicated Query record is a follow-up — PR-8).
    private static DataSourceDefinition[] ReadDataSets(XElement report)
        => El(report, "DataSets")?.Elements().Where(e => e.Name.LocalName == "DataSet")
            .Where(d => d.Attribute("Name")?.Value is { Length: > 0 })
            .Select(ReadDataSet).ToArray()
            ?? Array.Empty<DataSourceDefinition>();

    private static DataSourceDefinition ReadDataSet(XElement ds)
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
                calculated.Add(new CalculatedField(fieldName, RdlExpression.Convert(valueExpr)));
            }
            else
            {
                fields.Add(new DataField(fieldName, MapClrType(Val(f, "TypeName"))));
            }
        }

        var query = El(ds, "Query");
        var ps = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Val(query, "CommandText") is { Length: > 0 } sql)
        {
            ps["CommandText"] = sql;
        }
        if (Val(query, "CommandType") is { Length: > 0 } ct)
        {
            ps["CommandType"] = ct;
        }
        foreach (var qp in El(query, "QueryParameters")?.Elements().Where(e => e.Name.LocalName == "QueryParameter")
                 ?? Enumerable.Empty<XElement>())
        {
            if (qp.Attribute("Name")?.Value is { Length: > 0 } pn)
            {
                ps[$"QueryParameter:{pn}"] = RdlExpression.Convert(Val(qp, "Value"));
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

    // Maps one RDL report item to OmniReport element(s), offsetting by (dx, dy) so nested items inside a
    // Rectangle become absolute. Unknown item kinds are skipped (structural import never fails on them).
    private void AddItem(XElement item, Unit dx, Unit dy, List<ReportElement> into)
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
            case "Tablix":
                into.Add(ApplyCommon(TablixItem(item, bounds), item));
                break;
            // Chart, Gauge, Map, Subreport, … — follow-ups; skipped, not errored.
        }
    }

    // Maps an RDL <Tablix> to OmniReport's TablixElement. First cut: the matrix/crosstab path — dynamic
    // row + column group hierarchies + the body value cell + corner. Static-column tables and per-cell
    // spans are follow-ups; a warning is recorded (never silent) when the Tablix isn't a clean matrix.
    private ReportElement TablixItem(XElement item, Rectangle bounds)
    {
        var name = item.Attribute("Name")?.Value ?? "Tablix";
        var rowGroups = ReadTablixGroups(El(El(item, "TablixRowHierarchy"), "TablixMembers"), "Rows");
        var colGroups = ReadTablixGroups(El(El(item, "TablixColumnHierarchy"), "TablixMembers"), "Cols");

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
            _warnings.Add($"Tablix '{name}': importado parcialmente — sem hierarquia dinâmica de linha E coluna (tabelas planas/colunas estáticas são follow-up).");
        }

        return new TablixElement
        {
            Bounds = bounds,
            DataSetName = Val(item, "DataSetName"),
            RowGroups = new EquatableArray<TablixGroup>(rowGroups),
            ColumnGroups = new EquatableArray<TablixGroup>(colGroups),
            Cells = new EquatableArray<TablixCell>(cells),
        };
    }

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
        return el switch
        {
            TextBoxElement t => t with { Style = style ?? t.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            LabelElement l => l with { Style = style ?? l.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            RectangleElement r => r with { Style = style ?? r.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            ImageElement im => im with { Style = style ?? im.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            // A line's color/width come from its style border; map the first visible side to the Pen.
            LineElement ln => ln with { Pen = StyleBorderToPen(style) ?? ln.Pen, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
            TablixElement tx => tx with { Style = style ?? tx.Style, Visible = visible, VisibleExpression = visExpr, Bookmark = bookmark, DocumentMapLabel = docMap, Action = action },
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
        if (string.Equals(source, "External", StringComparison.OrdinalIgnoreCase))
        {
            return RdlExpression.IsExpression(value)
                ? new ImageElement { Source = ImageSourceKind.Expression, Expression = RdlExpression.Convert(value), Bounds = bounds }
                : new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds };
        }
        if (string.Equals(source, "Embedded", StringComparison.OrdinalIgnoreCase)
            && value is { Length: > 0 } && _embeddedImages.TryGetValue(value, out var bytes))
        {
            return new ImageElement { Source = ImageSourceKind.Inline, InlineData = new EquatableArray<byte>(bytes), Bounds = bounds };
        }
        // Database images (or an unresolved embedded name) are a follow-up — keep the name as the path.
        return new ImageElement { Source = ImageSourceKind.Path, Path = value, Bounds = bounds };
    }

    private static ReportParameter ReadParameter(XElement el)
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
        return new ReportParameter(name, type, prompt, defaultValue, multiValue, required, available,
            Nullable: nullable, AllowBlank: allowBlank, Hidden: hidden);
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
