using System.Globalization;
using System.Xml.Linq;
using Reporting.Bands;
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
        var valid = WriteValidValues(p.AvailableValues, p.Name, warnings);
        if (valid is not null)
        {
            el.Add(valid);
        }
        // Required is DERIVED on import (!Nullable && no <DefaultValue>); RDL has no <Required>, so a model
        // whose Required disagrees with that derivation can't round-trip the flag — warn, don't drop silently.
        var derivedRequired = !p.Nullable && p.DefaultValue is null;
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
        if (t == typeof(decimal)) { return "Decimal"; }
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
