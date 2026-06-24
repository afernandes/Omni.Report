using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Parameters;
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

    // The MS report-designer extension namespace, carrying rd:TypeName on DataSet fields (SSRS emits it).
    internal static readonly XNamespace RdlDesigner =
        "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";

    // Prefix for a report-item @Name synthesized when the model element has none (RDL requires @Name). The
    // importer strips it so synthetic names never leak into the model. Reserved — authors should not use it.
    internal const string SyntheticNamePrefix = "omni_auto_";

    public static XDocument Write(ReportDefinition def, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(warnings);
        var page = def.PageSetup;
        var report = new XElement(Rdl + "Report",
            new XAttribute(XNamespace.Xmlns + "rd", RdlDesigner));

        // The Body is RDL's flat design canvas. The importer turns free Body items into a ReportHeader band,
        // so the inverse writes ReportHeader (and any static ReportFooter) items back into Body.ReportItems.
        var bodyItems = new XElement(Rdl + "ReportItems");

        // A data-bound Detail with a repeating column-header PageHeader and no Groups is exactly what the
        // importer's TryFlatTablixBands produces by DECOMPOSING a single flat <Tablix> (column-header band +
        // detail band). The inverse reconstructs that one flat <Tablix> as the sole Body item, suppressing the
        // <PageHeader> section (the importer only re-decomposes when the page has no <PageHeader>).
        //
        // The shape must match UNAMBIGUOUSLY — only re-fold what the importer would have produced. A genuine
        // page header (free-positioned, non-Label, or unaligned to the data columns), a ColSpan/gappy layout,
        // or a filtered/sorted Detail are NOT flat tables; folding them would corrupt a legitimate report, so
        // they fall through to a warning instead.
        var flatTable = def.Detail is { DataSetName: not null, Elements.Count: > 0 }
            && def.Groups.Count == 0
            && (def.ReportHeader is null || def.ReportHeader.Elements.Count == 0)
            && (def.ReportFooter is null || def.ReportFooter.Elements.Count == 0)
            && def.Detail.FilterExpression is null
            && def.Detail.SortExpressions.Count == 0
            && IsReconstructableFlatTable(def.PageHeader, def.Detail);

        if (flatTable)
        {
            bodyItems.Add(WriteFlatTablix(def.PageHeader, def.Detail, warnings));
        }
        else
        {
            if (def.ReportHeader is { } rh)
            {
                WriteReportItems(bodyItems, rh.Elements, warnings);
            }
            if (def.ReportFooter is { Elements.Count: > 0 } rf)
            {
                WriteReportItems(bodyItems, rf.Elements, warnings);
            }
            if (def.Groups.Count > 0)
            {
                warnings.Add("Grupos (Groups) → <Tablix> hierarchy: exportação é uma fase posterior.");
            }
            if (def.Detail.Elements.Count > 0)
            {
                // A purely static detail → Body items. A data-bound detail that ISN'T a clean flat table (Groups,
                // a graphical page header, overlapping/zero-width cells, filters/sort) still has its elements
                // emitted as Body items so the DATA is never dropped — only the data-region binding (DataSetName)
                // and band type can't round-trip without the flat shape, which is warned. A genuine page header
                // is meanwhile preserved (the <PageHeader> section is not suppressed when flatTable is false).
                WriteReportItems(bodyItems, def.Detail.Elements, warnings);
                if (def.Detail.DataSetName is not null || def.Groups.Count > 0)
                {
                    warnings.Add("DetailBand vinculada a dados não corresponde ao padrão flat-table simples " +
                        "(Groups/PageHeader gráfico/células sobrepostas-ou-largura-zero/filtros) → elementos exportados como " +
                        "itens estáticos do Body; o vínculo DataSetName não round-trippa (round-trip parcial, sem perda de elementos).");
                }
            }
        }

        // The importer maps <Body><Height> onto the ReportHeader band's height, so the inverse writes that
        // band's height back (preserving the round-trip). Falls back to the printable area / a default.
        var bodyHeight = def.ReportHeader is { } rhBand && rhBand.Height.Mils > 0 ? rhBand.Height
            : page.ContentHeight.ToMm() > 0 ? page.ContentHeight : Unit.FromMm(100);
        // <ReportItems> requires ≥1 item — omit it entirely when the body is empty (the <Body> stays valid via
        // its <Height>); an empty <ReportItems> is XSD-invalid.
        var bodyEl = new XElement(Rdl + "Body", new XElement(Rdl + "Height", Size(bodyHeight)));
        if (bodyItems.HasElements)
        {
            bodyEl.Add(bodyItems);
        }

        var pageEl = new XElement(Rdl + "Page",
            // When the flat-table Tablix was reconstructed, its column headers ARE the PageHeader — don't also
            // emit a <PageHeader> section, or the importer won't re-decompose (it needs pageHasHeader==false).
            flatTable ? null : WriteBandSection(def.PageHeader, "PageHeader", warnings),
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

        // RDL 2016: <Report> no longer takes a direct <Body>; Body/Width/Page live inside
        // <ReportSections><ReportSection>. OmniReport's single-canvas model maps to exactly one section.
        report.Add(new XElement(Rdl + "ReportSections",
            new XElement(Rdl + "ReportSection",
                bodyEl,
                new XElement(Rdl + "Width", Size(page.ContentWidth)),
                pageEl)));

        WriteParameters(report, def, warnings);
        WriteDataSets(report, def, warnings);
        WriteVariables(report, def, warnings);

        AddIfPresent(report, def, "Language", "Language");
        AddIfPresent(report, def, "Description", "Description");
        AddIfPresent(report, def, "Author", "Author");

        return new XDocument(new XDeclaration("1.0", "utf-8", null), report);
    }

    // ── ReportParameters ──────────────────────────────────────────────────────────

    private static void WriteParameters(XElement report, ReportDefinition def, List<string> warnings)
    {
        if (def.Parameters.Count == 0)
        {
            return;
        }
        var ps = new XElement(Rdl + "ReportParameters");
        foreach (var p in def.Parameters)
        {
            ps.Add(WriteParameter(p, warnings));
        }
        report.Add(ps);
    }

    private static XElement WriteParameter(ReportParameter p, List<string> warnings)
    {
        var el = new XElement(Rdl + "ReportParameter",
            new XAttribute("Name", p.Name),
            new XElement(Rdl + "DataType", ParameterDataType(p.ValueType, p.Name, warnings)));
        if (p.Prompt is not null)
        {
            el.Add(new XElement(Rdl + "Prompt", p.Prompt));
        }
        // RDL booleans default to false; emit only when set (cleaner XML, same re-import).
        if (p.Nullable) { el.Add(new XElement(Rdl + "Nullable", "true")); }
        if (p.AllowBlank) { el.Add(new XElement(Rdl + "AllowBlank", "true")); }
        if (p.Hidden) { el.Add(new XElement(Rdl + "Hidden", "true")); }
        if (p.AllowMultiple) { el.Add(new XElement(Rdl + "MultiValue", "true")); } // RDL MultiValue ↔ AllowMultiple
        if (p.DefaultValue is not null)
        {
            // DateTime must be ISO-8601 ("o") so it round-trips exactly (Convert.ToString drops sub-seconds and
            // emits a non-ISO form SSRS rejects); other scalars use the invariant general form.
            var raw = p.DefaultValue is DateTime dt
                ? dt.ToString("o", CultureInfo.InvariantCulture)
                : Convert.ToString(p.DefaultValue, CultureInfo.InvariantCulture) ?? string.Empty;
            if (raw.StartsWith('='))
            {
                warnings.Add($"ReportParameter '{p.Name}': DefaultValue '{raw}' começa com '=' — o RDL o trata como expressão e o reimport o descarta (perda no round-trip).");
            }
            el.Add(new XElement(Rdl + "DefaultValue",
                new XElement(Rdl + "Values", new XElement(Rdl + "Value", raw))));
        }
        else if (!string.IsNullOrEmpty(p.DefaultValueExpression))
        {
            // Expression default → RDL <Value> with the leading '=' form (the importer's Convert reverses it).
            el.Add(new XElement(Rdl + "DefaultValue",
                new XElement(Rdl + "Values", new XElement(Rdl + "Value", ValueOf(p.DefaultValueExpression)))));
        }
        var valid = WriteValidValues(p.AvailableValues, p.Name, warnings);
        if (valid is not null)
        {
            el.Add(valid);
        }
        // Required is DERIVED on import (!Nullable && no <DefaultValue>); RDL has no <Required>, so a model
        // whose Required disagrees with that derivation can't round-trip the flag — warn, don't drop silently.
        var derivedRequired = !p.Nullable && p.DefaultValue is null && p.DefaultValueExpression is null;
        if (p.Required != derivedRequired)
        {
            warnings.Add($"ReportParameter '{p.Name}': Required={p.Required} não é representável em RDL " +
                $"(é derivado de Nullable/DefaultValue) — reimporta como {derivedRequired}.");
        }
        return el;
    }

    private static XElement? WriteValidValues(ParameterAvailableValues? av, string name, List<string> warnings)
    {
        if (av is null)
        {
            return null;
        }
        if (av.IsQuery)
        {
            // RDL <ValidValues> is query XOR static; a model carrying both loses the static list here.
            if (av.Values.Count > 0)
            {
                warnings.Add($"ReportParameter '{name}': AvailableValues combina query e lista estática; o RDL <ValidValues> é exclusivo — a lista estática não é exportada.");
            }
            var dsRef = new XElement(Rdl + "DataSetReference",
                new XElement(Rdl + "DataSetName", av.DataSet),
                new XElement(Rdl + "ValueField", av.ValueField ?? string.Empty));
            if (av.LabelField is not null)
            {
                dsRef.Add(new XElement(Rdl + "LabelField", av.LabelField));
            }
            return new XElement(Rdl + "ValidValues", dsRef);
        }
        if (av.Values.Count > 0)
        {
            var values = new XElement(Rdl + "ParameterValues");
            foreach (var v in av.Values)
            {
                var item = new XElement(Rdl + "ParameterValue", new XElement(Rdl + "Value", v.Value));
                if (v.Label is not null)
                {
                    item.Add(new XElement(Rdl + "Label", v.Label));
                }
                values.Add(item);
            }
            return new XElement(Rdl + "ValidValues", values);
        }
        return null;
    }

    private static string ParameterDataType(Type t, string name, List<string> warnings)
    {
        if (t == typeof(int)) { return "Integer"; }
        if (t == typeof(double)) { return "Float"; }
        // RDL's <DataType> enum has no "Decimal" — Float is its closest representable type (the value itself
        // still round-trips exactly via the literal <Value>; only the declared type widens to double on reimport).
        if (t == typeof(decimal)) { return "Float"; }
        if (t == typeof(bool)) { return "Boolean"; }
        if (t == typeof(DateTime)) { return "DateTime"; }
        if (t != typeof(string))
        {
            warnings.Add($"ReportParameter '{name}': tipo {t.Name} não tem equivalente RDL — exportado como String (reimporta como string).");
        }
        return "String";
    }

    // ── DataSets (a DataSourceDefinition maps to one <DataSet>) ─────────────────────

    private static void WriteDataSets(XElement report, ReportDefinition def, List<string> warnings)
    {
        if (def.DataSources.Count == 0)
        {
            return;
        }
        var sets = new XElement(Rdl + "DataSets");
        foreach (var ds in def.DataSources)
        {
            sets.Add(WriteDataSet(ds, warnings));
        }
        report.Add(sets);
    }

    private static XElement WriteDataSet(DataSourceDefinition ds, List<string> warnings)
    {
        var dataSet = new XElement(Rdl + "DataSet", new XAttribute("Name", ds.Name));

        // <Query> rebuilt from the reserved Parameters keys (_sql / _storedProc / param:*) — inverse of
        // ReadDataSet's encoding. Keys beginning with '_' (other than the two above) are designer connection
        // metadata, NOT query parameters — never emit them as <QueryParameter> (would corrupt the re-import).
        var query = new XElement(Rdl + "Query");
        if (ds.Parameters.TryGetValue("_sql", out var sql) && !string.IsNullOrEmpty(sql))
        {
            query.Add(new XElement(Rdl + "DataSourceName", ds.Name + "DataSource")); // synthetic; importer ignores
            if (ds.Parameters.TryGetValue("_storedProc", out var sp) &&
                string.Equals(sp, "true", StringComparison.OrdinalIgnoreCase))
            {
                query.Add(new XElement(Rdl + "CommandType", "StoredProcedure"));
            }
            query.Add(new XElement(Rdl + "CommandText", sql)); // raw SQL — NOT through RdlExpressionReverse
        }
        var queryParams = new XElement(Rdl + "QueryParameters");
        foreach (var kv in ds.Parameters)
        {
            if (!kv.Key.StartsWith("param:", StringComparison.Ordinal))
            {
                continue;
            }
            var sqlName = kv.Key["param:".Length..];
            var parts = kv.Value.Split('|', 2);             // encoding: "reportParameter|literal"
            var repParam = parts.Length > 0 ? parts[0] : string.Empty;
            var literal = parts.Length > 1 ? parts[1] : string.Empty;
            var value = !string.IsNullOrEmpty(repParam)
                ? RdlExpressionReverse.ToRdl("Parameters." + repParam) // → =Parameters!P.Value
                : literal;
            // A literal that itself starts with '=' would be re-read as an RDL expression (and silently turned
            // into a parameter binding or mangled) — warn rather than corrupt it on the round-trip.
            if (string.IsNullOrEmpty(repParam) && literal.StartsWith('='))
            {
                warnings.Add($"DataSet '{ds.Name}', parâmetro '{sqlName}': valor literal '{literal}' começa com '=' e o RDL o trataria como expressão — não round-trippa como literal.");
            }
            queryParams.Add(new XElement(Rdl + "QueryParameter", new XAttribute("Name", sqlName),
                new XElement(Rdl + "Value", value)));
        }
        // Designer connection metadata (provider/connection string/timeout) has no <DataSet> home and the
        // importer doesn't read <DataSources>; flag the loss rather than dropping it silently.
        if (ds.Parameters.ContainsKey("_kind") || ds.Parameters.ContainsKey("_connection") || ds.Parameters.ContainsKey("_timeout"))
        {
            warnings.Add($"DataSet '{ds.Name}': metadados de conexão do designer (provider/connection string/timeout) não são exportados para RDL — perda no round-trip.");
        }
        if (queryParams.HasElements)
        {
            query.Add(queryParams);
        }
        if (query.HasElements)
        {
            dataSet.Add(query);
        }

        // <Fields>: a DataField (no <Value>) carries <DataField> + optional rd:TypeName; a CalculatedField
        // carries <Value> (the importer distinguishes by the presence of <Value>).
        var fields = new XElement(Rdl + "Fields");
        foreach (var f in ds.Fields)
        {
            var field = new XElement(Rdl + "Field", new XAttribute("Name", f.Name),
                new XElement(Rdl + "DataField", f.Name)); // model has no physical column name → reuse Name
            var typeName = ClrTypeName(f.FieldType, $"DataSet '{ds.Name}', campo '{f.Name}'", warnings);
            if (typeName is not null)
            {
                field.Add(new XElement(RdlDesigner + "TypeName", typeName));
            }
            if (f.DisplayName is not null)
            {
                warnings.Add($"DataSet '{ds.Name}', campo '{f.Name}': DisplayName '{f.DisplayName}' não tem elemento RDL — perde no round-trip.");
            }
            fields.Add(field);
        }
        foreach (var c in ds.CalculatedFields)
        {
            // A calculated field carries <Value>; rd:TypeName round-trips its ResultType (read back below).
            var calc = new XElement(Rdl + "Field", new XAttribute("Name", c.Name),
                new XElement(Rdl + "Value", RdlExpressionReverse.ToRdl(c.Expression)));
            var resultType = ClrTypeName(c.ResultType, $"DataSet '{ds.Name}', campo calculado '{c.Name}'", warnings);
            if (resultType is not null)
            {
                calc.Add(new XElement(RdlDesigner + "TypeName", resultType));
            }
            fields.Add(calc);
        }
        if (fields.HasElements)
        {
            dataSet.Add(fields);
        }

        if (ds.SortExpressions.Count > 0)
        {
            var sorts = new XElement(Rdl + "SortExpressions");
            foreach (var s in ds.SortExpressions)
            {
                var sort = new XElement(Rdl + "SortExpression",
                    new XElement(Rdl + "Value", RdlExpressionReverse.ToRdl(s.Expression)));
                if (s.Direction == SortDirection.Descending)
                {
                    sort.Add(new XElement(Rdl + "Direction", "Descending")); // Ascending is the default
                }
                sorts.Add(sort);
            }
            dataSet.Add(sorts);
        }

        // The importer folds N <Filter>s into a flat boolean FilterExpression; rebuilding the structured
        // <Filters> that re-reads to the SAME expression isn't 1:1, so warn instead of emitting a lossy
        // approximation (a structural-preservation read-fidelity item could revisit this).
        if (!string.IsNullOrEmpty(ds.FilterExpression))
        {
            warnings.Add($"DataSet '{ds.Name}': FilterExpression → <Filters> estruturado não é reconstruído (perda conhecida).");
        }
        // Master-detail (DataMember/Relations) maps to nested data regions in RDL, not onto a flat <DataSet>.
        if (ds.DataMember is not null || ds.Relations.Count > 0)
        {
            warnings.Add($"DataSet '{ds.Name}': DataMember/Relations (master-detail) não têm representação em <DataSet> RDL — perdem no round-trip.");
        }
        return dataSet;
    }

    // Inverse of RdlImporter.MapClrType — null (→ omit <TypeName>) for any type outside the mapped set,
    // warning so an unrepresentable field type isn't dropped silently.
    private static string? ClrTypeName(Type? t, string ctx, List<string> warnings)
    {
        if (t is null) { return null; }
        if (t == typeof(int)) { return "System.Int32"; }
        if (t == typeof(decimal)) { return "System.Decimal"; }
        if (t == typeof(double)) { return "System.Double"; }
        if (t == typeof(bool)) { return "System.Boolean"; }
        if (t == typeof(DateTime)) { return "System.DateTime"; }
        if (t == typeof(string)) { return "System.String"; }
        warnings.Add($"{ctx}: tipo {t.Name} não tem equivalente RDL — <TypeName> omitido (reimporta sem tipo).");
        return null;
    }

    // ── Variables (report-scope only — the importer re-reads no others) ─────────────

    private static void WriteVariables(XElement report, ReportDefinition def, List<string> warnings)
    {
        if (def.Variables.Count == 0)
        {
            return;
        }
        var vars = new XElement(Rdl + "Variables");
        foreach (var v in def.Variables)
        {
            if (v.Scope != VariableScope.Report)
            {
                warnings.Add($"Variable '{v.Name}' (escopo {v.Scope}): RDL só relê variáveis de escopo Report — perde no round-trip.");
                continue;
            }
            if (v.InitialValue is not null)
            {
                warnings.Add($"Variable '{v.Name}': InitialValue não tem campo nativo em <Variable> RDL — perde no round-trip.");
            }
            vars.Add(new XElement(Rdl + "Variable", new XAttribute("Name", v.Name),
                new XElement(Rdl + "Value", RdlExpressionReverse.ToRdl(v.Expression))));
        }
        if (vars.HasElements)
        {
            report.Add(vars);
        }
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
            new XElement(Rdl + "PrintOnLastPage", Bool(band.PrintOnLastPage)));
        // Omit <ReportItems> if every element was unsupported/dropped — an empty <ReportItems> is XSD-invalid
        // (the section still round-trips its Height).
        if (items.HasElements)
        {
            section.Add(items);
        }
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
            TablixElement tablix => WriteTablix(tablix, warnings),
            ChartElement chart => WriteChart(chart, warnings),
            GaugeElement gauge => WriteGauge(gauge),
            SubreportElement sub => WriteSubreport(sub, warnings),
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

    // ── Data-viz (Chart/Gauge/Subreport) — inverses of ChartItem/GaugeItem/SubreportItem ──
    // The importer reads a deliberately FLATTENED subset of each; these writers re-emit exactly that subset so
    // an imported .rdl round-trips by value. Model fields with no RDL counterpart (Chart Title/Legend/series
    // colour & Size/High/Low, Subreport InlineDefinition/DataExpression) are warned, never dropped silently.
    //
    // Value/expression fields use ValueRoundTrip (NOT plain ValueOf): the importer stores a literal RDL value
    // verbatim, but ValueOf would mark it as an expression ('='-prefix) and re-import would fold '&'/'Like' into
    // Concat/Like. ValueRoundTrip only uses ValueOf when it survives the importer's Convert; else emits literal.

    private static XElement WriteChart(ChartElement chart, List<string> warnings)
    {
        var seriesCollection = new XElement(Rdl + "ChartSeriesCollection");
        foreach (var s in chart.Series)
        {
            seriesCollection.Add(new XElement(Rdl + "ChartSeries", new XAttribute("Name", s.Name),
                new XElement(Rdl + "Type", ChartTypeName(chart.Kind)),
                // RDL 2016: <ChartDataPoints><ChartDataPoint><ChartDataPointValues><Y> (the 2010 <DataPoints>
                // /<DataPoint>/<DataValues>/<DataValue>/<Value> shape is invalid under the 2016 schema).
                new XElement(Rdl + "ChartDataPoints",
                    new XElement(Rdl + "ChartDataPoint",
                        new XElement(Rdl + "ChartDataPointValues",
                            new XElement(Rdl + "Y", ValueRoundTrip(s.ValueExpression)))))));
            if (string.IsNullOrEmpty(s.ValueExpression))
            {
                warnings.Add($"ChartSeries '{s.Name}': sem ValueExpression — o importer descarta séries sem valor (a série não round-trippa).");
            }
            if (s.Color is not null || s.SizeExpression is not null || s.HighExpression is not null || s.LowExpression is not null)
            {
                warnings.Add($"ChartSeries '{s.Name}': Color/Size/High/Low não têm representação RDL lida — perdem no round-trip.");
            }
        }
        if (chart.Series.Count == 0)
        {
            // No series → emit a value-less placeholder carrying only the Type so the chart Kind survives the
            // round-trip (the importer reads Kind from the first series' <Type> before dropping value-less series).
            seriesCollection.Add(new XElement(Rdl + "ChartSeries", new XAttribute("Name", "Series1"),
                new XElement(Rdl + "Type", ChartTypeName(chart.Kind))));
        }
        var chartEl = new XElement(Rdl + "Chart", new XElement(Rdl + "ChartData", seriesCollection));

        // The importer applies a single category (the first <GroupExpression>) to every series; emit the first
        // series' category and warn if the model carries divergent ones.
        var category = chart.Series.Count > 0 ? chart.Series[0].CategoryExpression : null;
        if (!string.IsNullOrEmpty(category))
        {
            chartEl.Add(new XElement(Rdl + "ChartCategoryHierarchy",
                new XElement(Rdl + "ChartMembers",
                    new XElement(Rdl + "ChartMember",
                        new XElement(Rdl + "Group", new XAttribute("Name", SyntheticNamePrefix + chart.Id),
                            new XElement(Rdl + "GroupExpressions",
                                new XElement(Rdl + "GroupExpression", ValueRoundTrip(category))))))));
        }
        if (chart.Series.Any(s => s.CategoryExpression != category))
        {
            warnings.Add($"Chart '{chart.Name ?? chart.Id}': séries com categorias divergentes — só a primeira é exportada (RDL tem uma só hierarquia de categoria).");
        }
        if (chart.Title is not null || !chart.ShowLegend)
        {
            warnings.Add($"Chart '{chart.Name ?? chart.Id}': Title/ShowLegend não são lidos pelo importer — perdem no round-trip.");
        }
        return chartEl;
    }

    // Inverse of RdlImporter.MapChartKind (N→1: re-imports to the same ChartKind).
    private static string ChartTypeName(ChartKind kind) => kind switch
    {
        ChartKind.Line => "Line",
        ChartKind.Area => "Area",
        ChartKind.Pie => "Shape",
        ChartKind.Scatter => "Scatter",
        ChartKind.Bubble => "Bubble",
        ChartKind.Radar => "Polar",
        ChartKind.Stock => "Stock",
        _ => "Column", // Bar
    };

    private static XElement WriteGauge(GaugeElement gauge)
    {
        var pointerName = gauge.Kind == GaugeKind.Linear ? "LinearPointer" : "RadialPointer";
        var scale = new XElement(Rdl + (gauge.Kind == GaugeKind.Linear ? "LinearScale" : "RadialScale"),
            new XElement(Rdl + "Maximum", new XElement(Rdl + "Value", ValueRoundTrip(gauge.MaximumExpression))),
            new XElement(Rdl + "Minimum", new XElement(Rdl + "Value", ValueRoundTrip(gauge.MinimumExpression))),
            new XElement(Rdl + "GaugePointers",
                new XElement(Rdl + pointerName,
                    new XElement(Rdl + "Value", ValueRoundTrip(gauge.ValueExpression)))));
        if (gauge.Ranges.Count > 0)
        {
            var ranges = new XElement(Rdl + "ScaleRanges");
            foreach (var r in gauge.Ranges)
            {
                ranges.Add(new XElement(Rdl + "ScaleRange",
                    new XElement(Rdl + "StartValue", new XElement(Rdl + "Value", ValueRoundTrip(r.StartExpression))),
                    new XElement(Rdl + "EndValue", new XElement(Rdl + "Value", ValueRoundTrip(r.EndExpression))),
                    new XElement(Rdl + "BackgroundColor", r.ColorHex)));
            }
            scale.Add(ranges);
        }
        var gaugeBody = new XElement(Rdl + (gauge.Kind == GaugeKind.Linear ? "LinearGauge" : "RadialGauge"),
            new XElement(Rdl + "GaugeScales", scale));
        return new XElement(Rdl + "GaugePanel", new XElement(Rdl + "GaugePanelItems", gaugeBody));
    }

    private static XElement WriteSubreport(SubreportElement sub, List<string> warnings)
    {
        var subEl = new XElement(Rdl + "Subreport");
        if (!string.IsNullOrEmpty(sub.ReportId))
        {
            subEl.Add(new XElement(Rdl + "ReportName", sub.ReportId)); // literal — the importer reads it raw
        }
        else if (sub.InlineDefinition is not null)
        {
            // RDL requires <ReportName> (minOccurs=1). For an inline subreport (no external ReportId) emit the
            // inline def's name as a placeholder so the file stays XSD-valid; the real definition rides in
            // omni:InlineDefinition, and ApplyCustomProperties clears this placeholder ReportId on import.
            subEl.Add(new XElement(Rdl + "ReportName", sub.InlineDefinition.Name));
        }
        if (sub.ParameterBindings.Count > 0)
        {
            var parameters = new XElement(Rdl + "Parameters");
            foreach (var kv in sub.ParameterBindings)
            {
                parameters.Add(new XElement(Rdl + "Parameter", new XAttribute("Name", kv.Key),
                    new XElement(Rdl + "Value", ValueRoundTrip(kv.Value))));
            }
            subEl.Add(parameters);
        }
        // InlineDefinition/DataExpression are preserved losslessly via WriteCustomProperties (called by WriteCommon).
        return subEl;
    }

    // An RDL value body for an OmniReport expression that ALWAYS round-trips: ValueOf (=…) is used only when it
    // survives the importer's Convert; otherwise the value is a literal that ValueOf would mangle (text with
    // '&'/'Like', a dotted token, …), so it's emitted raw (the importer's Convert leaves a non-'=' value verbatim).
    private static string ValueRoundTrip(string? expr)
    {
        if (string.IsNullOrEmpty(expr))
        {
            return string.Empty;
        }
        var asExpression = ValueOf(expr);
        return RdlExpression.Convert(asExpression) == expr ? asExpression : expr;
    }

    // ── Tablix (matrix/crosstab) — inverse of RdlImporter.TablixItem ───────────────
    // A matrix TablixElement carries RowGroups/ColumnGroups + a corner cell (0,0) and a body value cell (1,1).
    // The importer reads exactly those (the corner via <TablixCorner>, the body via the first <Textbox> of
    // <TablixBody>, the groups via the two hierarchies, subtotals via an empty <Group/> sibling). WriteCommon
    // adds the Tablix's Name/Style/Top/Left/Width/Height afterwards (the importer's TablixBounds prefers the
    // literal <Width>/<Height> when present, so bounds round-trip).
    private static XElement WriteTablix(TablixElement tx, List<string> warnings)
    {
        var tablix = new XElement(Rdl + "Tablix");

        var corner = FindCell(tx, 0, 0);
        if (corner?.Content is not null)
        {
            tablix.Add(new XElement(Rdl + "TablixCorner",
                new XElement(Rdl + "TablixCornerRows",
                    new XElement(Rdl + "TablixCornerRow",
                        new XElement(Rdl + "TablixCornerCell", CellContents(corner.Content))))));
        }

        // Body: a single column/row carrying the (1,1) value cell. The importer reads only the first <Textbox>
        // of <TablixBody>; the column/row sizes are structural (bounds come from the literal <Width>/<Height>).
        var body = FindCell(tx, 1, 1);
        var colW = Size(tx.Bounds.Width.Mils > 0 ? tx.Bounds.Width : Unit.FromMm(25));
        var rowH = Size(tx.Bounds.Height.Mils > 0 ? tx.Bounds.Height : Unit.FromMm(6));
        tablix.Add(new XElement(Rdl + "TablixBody",
            new XElement(Rdl + "TablixColumns",
                new XElement(Rdl + "TablixColumn", new XElement(Rdl + "Width", colW))),
            new XElement(Rdl + "TablixRows",
                new XElement(Rdl + "TablixRow",
                    new XElement(Rdl + "Height", rowH),
                    new XElement(Rdl + "TablixCells",
                        new XElement(Rdl + "TablixCell", CellContents(body?.Content)))))));

        tablix.Add(WriteTablixHierarchy("TablixColumnHierarchy", tx.ColumnGroups, tx.ColumnSubtotals, warnings));
        tablix.Add(WriteTablixHierarchy("TablixRowHierarchy", tx.RowGroups, tx.RowSubtotals, warnings));

        if (tx.DataSetName is not null)
        {
            tablix.Add(new XElement(Rdl + "DataSetName", tx.DataSetName));
        }
        if (!string.IsNullOrEmpty(tx.NoRowsMessage))
        {
            // NoRowsMessage is a plain caption in the model (the importer stores it via Convert, never marked
            // as an expression); emit it literally — running it through ValueOf would prepend '=' and re-import
            // would mangle any '&'/'Like' as Concat/Like.
            tablix.Add(new XElement(Rdl + "NoRowsMessage", tx.NoRowsMessage));
        }
        // SubtotalLabel/GrandTotalLabel are preserved losslessly via WriteCustomProperties (called by WriteCommon).
        return tablix;
    }

    // A <CellContents><Textbox> for a Tablix cell: a Label → literal value; a TextBox → "=expr"; plus the
    // content's own <Style> (the importer reads the body cell's style via ReadStyle).
    private static XElement CellContents(ReportElement? content)
    {
        var textbox = content switch
        {
            LabelElement l => Textbox(TextRunValue(l.Text ?? string.Empty)),
            TextBoxElement t => Textbox(TextRunValue(ValueOf(t.Expression))),
            _ => Textbox(TextRunValue(string.Empty)),
        };
        // A cell's <Textbox> also requires @Name (XSD). Use the content's name, else synthesize (importer strips it).
        textbox.SetAttributeValue("Name", content is { Name: { Length: > 0 } cellName }
            ? cellName
            : SyntheticNamePrefix + (content?.Id ?? Guid.NewGuid().ToString("n")));
        var style = content is null ? null : StyleElement(content.Style);
        if (style is not null)
        {
            textbox.Add(style);
        }
        return new XElement(Rdl + "CellContents", textbox);
    }

    // <TablixRowHierarchy>/<TablixColumnHierarchy> from a group list (outer→inner, nested <TablixMembers>).
    // No groups → a single static anchor member. Subtotals → an empty <Group/> sibling at the outer level
    // (exactly what RdlImporter.HasSubtotalMember detects).
    private static XElement WriteTablixHierarchy(string element, EquatableArray<TablixGroup> groups, bool subtotals,
        List<string> warnings)
    {
        var members = new XElement(Rdl + "TablixMembers");
        if (groups.Count == 0)
        {
            members.Add(new XElement(Rdl + "TablixMember")); // static anchor (no dynamic group)
            return new XElement(Rdl + element, members);
        }
        XElement level = members;
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var g = groups[gi];
            XElement member;
            if (string.IsNullOrEmpty(g.GroupExpression))
            {
                // A keyless group is not a dynamic RDL group — an empty <GroupExpression> would be dropped by
                // the importer (and could flip the whole element to the flat-table shape). Emit a static member
                // and warn (the group identity/sort can't round-trip through RDL).
                warnings.Add($"TablixGroup '{g.Name}': sem GroupExpression — exportado como membro estático (não round-trippa como grupo dinâmico).");
                member = new XElement(Rdl + "TablixMember");
            }
            else
            {
                var group = new XElement(Rdl + "Group", new XAttribute("Name", g.Name),
                    new XElement(Rdl + "GroupExpressions",
                        new XElement(Rdl + "GroupExpression", ValueOf(g.GroupExpression))));
                member = new XElement(Rdl + "TablixMember", group);
                if (!string.IsNullOrEmpty(g.SortExpression))
                {
                    var sort = new XElement(Rdl + "SortExpression", new XElement(Rdl + "Value", ValueOf(g.SortExpression)));
                    if (g.SortDescending)
                    {
                        sort.Add(new XElement(Rdl + "Direction", "Descending"));
                    }
                    member.Add(new XElement(Rdl + "SortExpressions", sort));
                }
            }
            level.Add(member);
            // Only nest a child <TablixMembers> when there's an inner group; an empty one is XSD-invalid
            // (TablixMembers requires ≥1 TablixMember).
            if (gi < groups.Count - 1)
            {
                var inner = new XElement(Rdl + "TablixMembers"); // nest the next (inner) group beneath this member
                member.Add(inner);
                level = inner;
            }
        }
        if (subtotals)
        {
            members.Add(new XElement(Rdl + "TablixMember", new XElement(Rdl + "Group"))); // empty = total
        }
        return new XElement(Rdl + element, members);
    }

    private static TablixCell? FindCell(TablixElement tx, int row, int col)
    {
        foreach (var c in tx.Cells)
        {
            if (c.RowIndex == row && c.ColumnIndex == col)
            {
                return c;
            }
        }
        return null;
    }

    // True when the bands can be re-folded into a flat <Tablix> losslessly via the column-boundary grid. The
    // detail/header cells must not OVERLAP and must have positive width (those have no column-grid meaning);
    // a page header, if present, must be a column-header row — all Labels (graphics like a Line/Image mark
    // genuine page chrome, which is preserved as a <PageHeader> instead). The union-boundary grid handles any
    // alignment (ColSpan, leading/trailing/interior gaps, differing extents), so no extent match is required.
    private static bool IsReconstructableFlatTable(ReportBand? pageHeader, DetailBand detail)
    {
        var detailEls = detail.Elements.OrderBy(e => e.Bounds.X.Mils).ToList();
        if (HasOverlapOrZeroWidth(detailEls))
        {
            return false;
        }
        if (pageHeader is null || pageHeader.Elements.Count == 0)
        {
            return true; // detail-only flat table
        }
        var headerEls = pageHeader.Elements.OrderBy(e => e.Bounds.X.Mils).ToList();
        return headerEls.All(e => e is LabelElement) && !HasOverlapOrZeroWidth(headerEls);
    }

    // True if any element (in X-sorted order) has non-positive width or starts before the previous one ends —
    // either makes the elements something other than a clean left-to-right column grid.
    private static bool HasOverlapOrZeroWidth(List<ReportElement> els)
    {
        for (var i = 0; i < els.Count; i++)
        {
            if (els[i].Bounds.Width.Mils <= 0)
            {
                return true;
            }
            if (i > 0 && els[i].Bounds.X.Mils < els[i - 1].Bounds.X.Mils + els[i - 1].Bounds.Width.Mils)
            {
                return true;
            }
        }
        return false;
    }

    // ── Flat-table Tablix — inverse of RdlImporter.TryFlatTablixBands ───────────────
    // The importer decomposes a single flat <Tablix> (no dynamic groups) into a repeating PageHeader band
    // (column-header Labels) + a data-bound DetailBand (one TextBox per column). This rebuilds that one flat
    // <Tablix>. The column grid is the sorted distinct X boundaries (start AND end) of every header+detail
    // element, so a cell spanning several columns becomes a <ColSpan> and an uncovered column an empty cell —
    // ColSpan-merged headers and gap layouts round-trip exactly. The caller suppresses the <PageHeader> section.
    private static XElement WriteFlatTablix(ReportBand? pageHeader, DetailBand detail, List<string> warnings)
    {
        var detailEls = detail.Elements.OrderBy(e => e.Bounds.X.Mils).ToList();
        var headerEls = (pageHeader?.Elements ?? EquatableArray<ReportElement>.Empty)
            .OrderBy(e => e.Bounds.X.Mils).ToList();
        var hasHeader = headerEls.Count > 0;

        // Cluster the X boundaries: independent mm→mil rounding can place the "same" physical boundary 1 mil
        // apart (e.g. 20mm+20mm = 1574 mils vs 40mm = 1575), which would emit a spurious sliver column. Collapse
        // boundaries within EdgeTolerance to one. (Imported tables share exact edges, so nothing merges there.)
        var raw = new List<int>();
        foreach (var e in detailEls.Concat(headerEls))
        {
            raw.Add(e.Bounds.X.Mils);
            raw.Add(e.Bounds.X.Mils + e.Bounds.Width.Mils);
        }
        raw.Sort();
        var edgeList = new List<int>();
        foreach (var v in raw)
        {
            if (edgeList.Count == 0 || v - edgeList[^1] > EdgeTolerance)
            {
                edgeList.Add(v);
            }
        }
        var columnCount = Math.Max(edgeList.Count - 1, 0);

        var columns = new XElement(Rdl + "TablixColumns");
        for (var i = 0; i < columnCount; i++)
        {
            columns.Add(new XElement(Rdl + "TablixColumn",
                new XElement(Rdl + "Width", Size(new Unit(edgeList[i + 1] - edgeList[i])))));
        }
        var rows = new XElement(Rdl + "TablixRows");
        if (hasHeader)
        {
            rows.Add(FlatRow(pageHeader!.Height, headerEls, edgeList));
        }
        rows.Add(FlatRow(detail.Height, detailEls, edgeList));

        var tablix = new XElement(Rdl + "Tablix", new XElement(Rdl + "TablixBody", columns, rows));

        var colMembers = new XElement(Rdl + "TablixMembers");
        for (var i = 0; i < columnCount; i++)
        {
            colMembers.Add(new XElement(Rdl + "TablixMember")); // a static member per column
        }
        tablix.Add(new XElement(Rdl + "TablixColumnHierarchy", colMembers));

        var rowMembers = new XElement(Rdl + "TablixMembers");
        if (hasHeader)
        {
            rowMembers.Add(new XElement(Rdl + "TablixMember")); // static header row
        }
        // A <Group> WITHOUT a <GroupExpression> marks the detail row (El(m,"Group") is not null), and stays
        // out of the dynamic-group count so TryFlatTablixBands still fires.
        rowMembers.Add(new XElement(Rdl + "TablixMember",
            new XElement(Rdl + "Group", new XAttribute("Name", "Details"))));
        tablix.Add(new XElement(Rdl + "TablixRowHierarchy", rowMembers));

        tablix.Add(new XElement(Rdl + "DataSetName", detail.DataSetName));
        if (!string.IsNullOrEmpty(detail.NoRowsMessage))
        {
            tablix.Add(new XElement(Rdl + "NoRowsMessage", detail.NoRowsMessage)); // literal caption
        }
        if (detail.CanGrow || detail.CanShrink || detail.VisibleExpression is not null)
        {
            warnings.Add("Flat-table: CanGrow/CanShrink/VisibleExpression do Detail não são re-emitidos no <Tablix> (hint de render perdido).");
        }
        var pageBreak = WritePageBreak(detail.PageBreak);
        if (pageBreak is not null)
        {
            tablix.Add(pageBreak);
        }

        // Bounds span the whole grid so the import's scale = edges-width / Σcolumn-widths = 1 and the
        // reconstructed column edges land exactly on the original element X positions.
        var left = edgeList.Count > 0 ? new Unit(edgeList[0]) : Unit.Zero;
        var width = edgeList.Count > 0 ? new Unit(edgeList[^1] - edgeList[0]) : Unit.Zero;
        var height = (hasHeader ? pageHeader!.Height : Unit.Zero) + detail.Height;
        tablix.Add(new XElement(Rdl + "Top", Size(Unit.Zero)),
            new XElement(Rdl + "Left", Size(left)),
            new XElement(Rdl + "Width", Size(width)),
            new XElement(Rdl + "Height", Size(height)));
        return tablix;
    }

    // One <TablixRow> over the column grid: each element occupies the columns from its left edge to its right
    // edge (emitted with <ColSpan> when it covers more than one); a column no element starts on becomes an
    // empty placeholder cell. This is the inverse of the importer's edge/ColSpan walk (which drops empty cells
    // but still advances the column index).
    private static XElement FlatRow(Unit height, List<ReportElement> els, List<int> edges)
    {
        var tablixCells = new XElement(Rdl + "TablixCells");
        var columnCount = Math.Max(edges.Count - 1, 0);
        var col = 0;
        var elIdx = 0;
        while (col < columnCount)
        {
            if (elIdx < els.Count && ColumnOf(edges, els[elIdx].Bounds.X.Mils) == col)
            {
                var el = els[elIdx++];
                var endCol = ColumnOf(edges, el.Bounds.X.Mils + el.Bounds.Width.Mils);
                var span = Math.Max(endCol - col, 1);
                var cell = new XElement(Rdl + "TablixCell", CellContents(el));
                if (span > 1)
                {
                    cell.Add(new XElement(Rdl + "ColSpan", span.ToString(CultureInfo.InvariantCulture)));
                }
                tablixCells.Add(cell);
                col += span;
            }
            else
            {
                tablixCells.Add(new XElement(Rdl + "TablixCell", CellContents(null))); // gap → empty cell
                col++;
            }
        }
        return new XElement(Rdl + "TablixRow", new XElement(Rdl + "Height", Size(height)), tablixCells);
    }

    // Index of the clustered grid edge nearest to a mils value (within EdgeTolerance), so an element whose edge
    // was snapped during clustering still maps to its column. -1 if none (degenerate input).
    private static int ColumnOf(List<int> edges, int mils)
    {
        for (var i = 0; i < edges.Count; i++)
        {
            if (Math.Abs(edges[i] - mils) <= EdgeTolerance)
            {
                return i;
            }
        }
        return -1;
    }

    private const int EdgeTolerance = 2; // mils (≈0.05mm) — collapses mm→mil rounding noise between bands

    // Inverse of RdlImporter.ReadPageBreak (the 2008+ <PageBreak><BreakLocation> form).
    private static XElement? WritePageBreak(PageBreak pageBreak) => pageBreak == PageBreak.None
        ? null
        : new XElement(Rdl + "PageBreak", new XElement(Rdl + "BreakLocation", pageBreak.ToString()));

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
        // RDL requires @Name on every report item (XSD minOccurs + ReportItems!Name references). When the model
        // has no name, synthesize a stable unique one from the element Id; RdlImporter strips this synthetic
        // prefix back to null on import, so the round-trip stays clean.
        item.SetAttributeValue("Name", string.IsNullOrEmpty(el.Name) ? SyntheticNamePrefix + el.Id : el.Name);
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
        WriteCustomProperties(item, el);
    }

    // Model fields with no native slot in <Subreport>/<Tablix> are preserved LOSSLESSLY in RDL's own
    // <CustomProperties> (part of the XSD; ignored by Report Builder, so the file still opens in SSRS). Names are
    // 'omni:'-prefixed to avoid clashing with an author's own custom properties. RdlImporter.ReadElementCustomProperties
    // + ApplyCustomProperties are the exact inverse.
    private static void WriteCustomProperties(XElement item, ReportElement el)
    {
        var props = new List<(string Name, string Value)>();
        switch (el)
        {
            case SubreportElement sub:
                if (!string.IsNullOrEmpty(sub.DataExpression))
                {
                    props.Add(("omni:DataExpression", sub.DataExpression));
                }
                if (sub.InlineDefinition is not null)
                {
                    props.Add(("omni:InlineDefinition", SerializeInline(sub.InlineDefinition)));
                }
                break;
            case TablixElement tx:
                if (tx.SubtotalLabel is not null)
                {
                    props.Add(("omni:SubtotalLabel", tx.SubtotalLabel));
                }
                if (tx.GrandTotalLabel is not null)
                {
                    props.Add(("omni:GrandTotalLabel", tx.GrandTotalLabel));
                }
                break;
        }
        if (props.Count == 0)
        {
            return;
        }
        item.Add(new XElement(Rdl + "CustomProperties",
            props.Select(p => new XElement(Rdl + "CustomProperty",
                new XElement(Rdl + "Name", p.Name),
                new XElement(Rdl + "Value", p.Value)))));
    }

    // An inline subreport definition is itself a full ReportDefinition; persist it as a compact repjson blob in the
    // CustomProperty value, round-tripped by the same model serializer (RepJsonSerializer) the importer reads with.
    private static string SerializeInline(ReportDefinition def)
    {
        using var ms = new System.IO.MemoryStream();
        new RepJsonSerializer().Save(def, ms);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
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
        // RDL gradient: BackgroundColor (above) is the start; emit type + end colour only as a complete pair, so a
        // None gradient never carries a dangling end colour on RDL round-trip (the canonical .repx/.repjson keep both
        // independently for full losslessness).
        if (style.BackgroundGradient != BackgroundGradientType.None && style.BackColorEnd is { } backEnd)
        {
            s.Add(new XElement(Rdl + "BackgroundGradientType", style.BackgroundGradient.ToString()));
            s.Add(new XElement(Rdl + "BackgroundGradientEndColor", backEnd.ToHex()));
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
