using System.Text.RegularExpressions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Internal;
using Reporting.Layout.Primitives;
using Reporting.Rendering;

namespace Reporting.Layout;

/// <summary>Default <see cref="IReportPaginator"/> — produces a fully positioned
/// <see cref="RenderedReport"/> from a definition + data.</summary>
/// <remarks>
/// <para>Algorithm: two-pass when the report references <c>Page.Total</c>, single-pass otherwise.</para>
/// <para>Within each pass: iterate primary data source rows, detect group transitions,
/// emit Report header → Page headers → Group headers (open) → Detail (per row) →
/// Group footers (close) → Page footers → Report footer. Page break occurs when the
/// next band wouldn't fit between the page header bottom and the page footer top.</para>
/// </remarks>
public sealed partial class ReportPaginator : IReportPaginator
{
    private readonly ExpressionCompiler _compiler;
    private readonly ExpressionEvaluator _evaluator;
    private readonly TemplateRenderer _templates;

    public ReportPaginator(ExpressionCompiler? compiler = null)
    {
        _compiler = compiler ?? new ExpressionCompiler();
        _evaluator = new ExpressionEvaluator(_compiler);
        _templates = new TemplateRenderer(_evaluator);
    }

    public async Task<RenderedReport> PaginateAsync(PaginationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Opt-in: wire the report's Code.X(...) resolver (null unless the host enabled it).
        _evaluator.CodeFunctionResolver = request.CodeFunctionResolver;
        var measurer = request.Measurer ?? new AverageWidthTextMeasurer();
        var (iterationRows, allSources) = await MaterializeAsync(request, ct).ConfigureAwait(false);

        var firstPass = ExecutePass(request, iterationRows, allSources, measurer, totalPagesHint: 0);
        // A second pass is needed when an expression references Page.Total/TotalPages (so the count must be
        // known), OR when a page header/footer must be suppressed on the last page (PrintOnLastPage=false) —
        // the last page can't be identified during the forward-only first pass, only its total count can.
        if (!UsesTotalPages(request.Definition) && !UsesLastPageGating(request.Definition))
        {
            return new RenderedReport(request.Definition.Name, new EquatableArray<RenderedPage>(firstPass.ToArray()));
        }
        var secondPass = ExecutePass(request, iterationRows, allSources, measurer, totalPagesHint: firstPass.Count);
        return new RenderedReport(request.Definition.Name, new EquatableArray<RenderedPage>(secondPass.ToArray()));
    }

    /// <summary>Pre-materializes every registered data source's rows (so sub-detail bands can
    /// iterate them in-memory) and builds the iteration list for the main Detail band.</summary>
    private static async Task<(List<IterationRow> iter,
        Dictionary<string, List<IReadOnlyList<KeyValuePair<string, object?>>>> allSources)>
        MaterializeAsync(PaginationRequest request, CancellationToken ct)
    {
        var allSources = new Dictionary<string, List<IReadOnlyList<KeyValuePair<string, object?>>>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var name in request.DataSources.Names)
        {
            if (!request.DataSources.TryGet(name, out var ds)) continue;
            var rows = new List<IReadOnlyList<KeyValuePair<string, object?>>>();
            await foreach (var record in ds.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add(record.ToKeyValuePairs().ToList());
            }
            allSources[name] = rows;
        }
        var iter = await MaterializeRowsAsync(request, ct).ConfigureAwait(false);
        return (iter, allSources);
    }

    /// <summary>One row of the Detail iteration. <see cref="Fields"/> is the "live" row (sets
    /// the unqualified <c>Fields.X</c> resolution); <see cref="SourceRows"/> carries the
    /// per-source current rows used for qualified <c>Fields.SourceName.X</c> references —
    /// in master-detail iteration this includes both the parent and the child rows.</summary>
    private sealed record IterationRow(
        IReadOnlyList<KeyValuePair<string, object?>> Fields,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? SourceRows = null);

    private static async Task<List<IterationRow>> MaterializeRowsAsync(
        PaginationRequest request, CancellationToken ct)
    {
        // Resolve the primary source name once. It drives the unqualified Fields.X scope.
        var primaryName = ResolvePrimaryName(request);
        var iteration = new List<IterationRow>();
        if (primaryName is null) return iteration;
        if (!request.DataSources.TryGet(primaryName, out var primaryDs)) return iteration;

        // Materialize the primary source's rows.
        var primaryRows = new List<IReadOnlyList<KeyValuePair<string, object?>>>();
        await foreach (var record in primaryDs.ReadAsync(ct).ConfigureAwait(false))
        {
            primaryRows.Add(record.ToKeyValuePairs().ToList());
        }

        // Pick up master-detail relations declared on the primary source. When at least one
        // is present, the iteration becomes nested (per parent row → per child row of the
        // first relation), and Detail bands receive both source contexts so qualified
        // references like {Fields.Clientes.nome} work inside a Pedido row.
        var primaryDef = request.Definition.DataSources
            .FirstOrDefault(d => string.Equals(d.Name, primaryName, StringComparison.Ordinal));
        var relations = primaryDef?.Relations ?? Reporting.Common.EquatableArray<Reporting.Data.DataRelation>.Empty;

        // When the Detail band has sub-details declared, the parent Detail must fire ONCE
        // per parent row (NOT per child). The sub-detail loops handle child iteration
        // themselves at render time. This matches DevExpress XtraReports' DetailReportBand
        // and FastReport's sub-band semantics — flattening the iteration would render the
        // parent multiple times, which is rarely desired.
        bool hasSubDetails = request.Definition.Detail.SubDetails.Count > 0;

        if (relations.Count == 0 || hasSubDetails)
        {
            // No relations — single-source iteration. Wrap each row in an IterationRow with
            // the primary source's snapshot so qualified Fields.<primary>.X still resolves.
            foreach (var row in primaryRows)
            {
                var sources = new Dictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>(
                    StringComparer.OrdinalIgnoreCase) { [primaryName] = row };
                iteration.Add(new IterationRow(row, sources));
            }
            return iteration;
        }

        // Master-detail iteration. For simplicity (and matching Crystal/SSRS's "one detail
        // band per child row" model), we pick the FIRST relation as the iteration driver.
        // Reports with multiple parallel child relations should use sub-bands or split into
        // separate reports — true cartesian explosion is rarely useful.
        var rel = relations[0];
        if (!request.DataSources.TryGet(rel.ChildSource, out var childDs))
        {
            // Child source not registered — fall back to parent-only iteration.
            foreach (var row in primaryRows)
            {
                var sources = new Dictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>(
                    StringComparer.OrdinalIgnoreCase) { [primaryName] = row };
                iteration.Add(new IterationRow(row, sources));
            }
            return iteration;
        }

        // Materialize child rows once, then for each parent emit one IterationRow per matching child.
        var childRows = new List<IReadOnlyList<KeyValuePair<string, object?>>>();
        await foreach (var record in childDs.ReadAsync(ct).ConfigureAwait(false))
        {
            childRows.Add(record.ToKeyValuePairs().ToList());
        }

        foreach (var parentRow in primaryRows)
        {
            var parentKey = ValueOf(parentRow, rel.ParentField);
            bool anyMatch = false;
            foreach (var childRow in childRows)
            {
                var childKey = ValueOf(childRow, rel.ChildField);
                if (!KeysMatch(parentKey, childKey)) continue;
                anyMatch = true;
                var sources = new Dictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [primaryName] = parentRow,
                    [rel.ChildSource] = childRow,
                };
                // Live "Fields" stays bound to the child row — that's the Detail band's
                // natural iteration; parent fields are still reachable via Fields.<Parent>.X.
                iteration.Add(new IterationRow(childRow, sources));
            }
            // Optional left-join behavior: parents with no children still emit a row so group
            // headers/footers fire. Detail-only reports are typically inner-joined; uncomment
            // below to opt into outer behavior.
            // if (!anyMatch) {
            //     var sources = new Dictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>(
            //         StringComparer.OrdinalIgnoreCase) { [primaryName] = parentRow };
            //     iteration.Add(new IterationRow(parentRow, sources));
            // }
            _ = anyMatch;
        }
        return iteration;
    }

    // Resolves the dataset that drives the detail loop. Shared by MaterializeRowsAsync and ExecutePass so the
    // two never drift (a drift would make materialization iterate one source while the render publishes/filters
    // another). The DetailBand's explicit DataSetName wins (it's more specific than the host's request); when
    // null the chain is the historical one — PrimaryDataSource, then first declared source, then any registered.
    private static string? ResolvePrimaryName(PaginationRequest request)
        => request.Definition.Detail.DataSetName
           ?? request.PrimaryDataSource
           ?? request.Definition.DataSources.FirstOrDefault()?.Name
           ?? request.DataSources.Names.FirstOrDefault();

    private static object? ValueOf(IReadOnlyList<KeyValuePair<string, object?>> row, string fieldName)
    {
        foreach (var kv in row)
        {
            if (string.Equals(kv.Key, fieldName, StringComparison.Ordinal)) return kv.Value;
        }
        return null;
    }

    // Resolves an RDL language tag (e.g. "en-US") to a culture; an unknown/blank tag yields null so the
    // expression context keeps its default culture instead of throwing. predefinedOnly:true rejects phantom
    // cultures deterministically (ICU and NLS alike) — a creatable-but-uninitialized tag like "qaa" would
    // otherwise pass here and crash a later ToString(format, culture). So only real cultures get through.
    private static System.Globalization.CultureInfo? TryGetCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        try { return System.Globalization.CultureInfo.GetCultureInfo(language, predefinedOnly: true); }
        catch (System.Globalization.CultureNotFoundException) { return null; }
    }

    private static bool KeysMatch(object? a, object? b)
    {
        if (a is null) return b is null;
        if (b is null) return false;
        if (a.Equals(b)) return true;
        // Coerce numeric types so int parent.id matches long/decimal child.cliente_id.
        if (IsNumeric(a) && IsNumeric(b))
        {
            try { return Convert.ToDecimal(a) == Convert.ToDecimal(b); }
            catch { /* fall through */ }
        }
        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static bool IsNumeric(object o) =>
        o is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    /// <summary>Emits one or more sub-detail bands after the parent Detail. Each sub-detail
    /// resolves its <c>DataMember</c> first as a relation declared on the parent's source
    /// (master-detail by name), then falls back to a registered data source. Header runs once
    /// before the first child row, the sub-detail's elements once per child, footer once after.</summary>
    private void EmitSubDetails(
        Reporting.Common.EquatableArray<Reporting.Bands.SubDetailBand> subDetails,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, object?>>>? sourceRows,
        Reporting.Data.DataSourceDefinition? primaryDef,
        IReadOnlyDictionary<string, List<IReadOnlyList<KeyValuePair<string, object?>>>> allSources,
        PageAccumulator page,
        BandRenderer bandRenderer,
        ReportExpressionContext ctx,
        ReportDefinition def,
        string? primarySourceName,
        IReadOnlyList<KeyValuePair<string, object?>> parentRow)
    {
        foreach (var sub in subDetails)
        {
            if (!sub.Visible) continue;
            if (!string.IsNullOrEmpty(sub.VisibleExpression))
            {
                var v = _evaluator.Evaluate(sub.VisibleExpression, ctx);
                if (v is bool vb && !vb) continue;
            }

            // Resolve sub-detail rows: relation first, then plain source.
            string? childSourceName = null;
            string? childFieldName = null;
            string? parentFieldName = null;
            if (primaryDef is not null)
            {
                foreach (var r in primaryDef.Relations)
                {
                    if (string.Equals(r.Name, sub.DataMember, StringComparison.Ordinal))
                    {
                        childSourceName = r.ChildSource;
                        childFieldName = r.ChildField;
                        parentFieldName = r.ParentField;
                        break;
                    }
                }
            }
            if (childSourceName is null && allSources.ContainsKey(sub.DataMember))
            {
                childSourceName = sub.DataMember;
            }
            if (childSourceName is null || !allSources.TryGetValue(childSourceName, out var allChildRows))
            {
                continue; // unknown data member — nothing to iterate
            }

            // Filter children when this is a relation-bound sub-band.
            List<IReadOnlyList<KeyValuePair<string, object?>>> matchedChildren;
            if (parentFieldName is not null && childFieldName is not null)
            {
                var parentKey = ValueOf(parentRow, parentFieldName);
                matchedChildren = new List<IReadOnlyList<KeyValuePair<string, object?>>>(allChildRows.Count);
                foreach (var row in allChildRows)
                {
                    if (KeysMatch(parentKey, ValueOf(row, childFieldName))) matchedChildren.Add(row);
                }
            }
            else
            {
                matchedChildren = new List<IReadOnlyList<KeyValuePair<string, object?>>>(allChildRows);
            }

            if (matchedChildren.Count == 0 && !sub.PrintIfEmpty) continue;

            // Header (renders once before child rows).
            if (sub.Header is { } header)
            {
                var hh = bandRenderer.Measure(header, ctx);
                EnsureRoom(page, hh, def, bandRenderer, ctx);
                var hl = bandRenderer.Render(header, page.Origin, ctx);
                page.Emit(hl.Primitives, hl.Height);
            }

            // For each child row: set source context + live Fields + render the sub-detail
            // elements via a transient DetailBand (BandRenderer's existing path).
            var transient = new Reporting.Bands.DetailBand(sub.Height, sub.Elements);
            foreach (var childRow in matchedChildren)
            {
                ctx.SetCurrentRowNoSnapshot(childRow);
                ctx.SetSourceCurrentRow(childSourceName, childRow);
                // Parent stays available via Fields.<Parent>.X (already published).
                var dh = bandRenderer.Measure(transient, ctx);
                EnsureRoom(page, dh, def, bandRenderer, ctx);
                var dl = bandRenderer.Render(transient, page.Origin, ctx);
                page.Emit(dl.Primitives, dl.Height);
            }

            // Restore parent row before rendering footer so its expressions see parent data.
            ctx.SetCurrentRowNoSnapshot(parentRow);
            if (primarySourceName is not null) ctx.SetSourceCurrentRow(primarySourceName, parentRow);

            // Footer (renders once after child rows).
            if (sub.Footer is { } footer)
            {
                var fh = bandRenderer.Measure(footer, ctx);
                EnsureRoom(page, fh, def, bandRenderer, ctx);
                var fl = bandRenderer.Render(footer, page.Origin, ctx);
                page.Emit(fl.Primitives, fl.Height);
            }
        }
    }

    /// <summary>Applies an RDL-style filter (drop rows whose expression evaluates falsy)
    /// followed by a multi-key stable sort. Returns a possibly-new list; if no filter and no
    /// sort apply, the input is returned as-is (zero-cost no-op).</summary>
    /// <remarks>
    /// The filter expression is treated as truthy iff (a) bool true, (b) non-empty string,
    /// (c) non-zero numeric, (d) non-null reference. The same convention applies everywhere
    /// expressions are coerced to bool. Sort comparators handle IComparable correctly and
    /// fall back to ordinal string compare when types differ — matches the SSRS behaviour.
    /// </remarks>
    private List<IterationRow> ApplyFilterAndSort(
        IReadOnlyList<IterationRow> rows,
        string? filterExpression,
        Reporting.Common.EquatableArray<Reporting.Data.SortDescriptor> sorts,
        ReportExpressionContext ctx,
        string? primarySourceName)
    {
        if (string.IsNullOrEmpty(filterExpression) && sorts.Count == 0)
        {
            return rows is List<IterationRow> alreadyList ? alreadyList : rows.ToList();
        }
        // Filter pass — evaluate expression per row.
        var filtered = new List<IterationRow>(rows.Count);
        foreach (var row in rows)
        {
            ctx.SetCurrentRowNoSnapshot(row.Fields);
            if (row.SourceRows is { } srs)
            {
                foreach (var kv in srs) ctx.SetSourceCurrentRow(kv.Key, kv.Value);
            }
            else if (primarySourceName is not null)
            {
                ctx.SetSourceCurrentRow(primarySourceName, row.Fields);
            }
            if (!string.IsNullOrEmpty(filterExpression))
            {
                var result = _evaluator.Evaluate(filterExpression, ctx);
                if (!IsTruthy(result)) continue;
            }
            filtered.Add(row);
        }
        // Sort pass — composite stable sort by evaluating each sort expression per row.
        if (sorts.Count == 0) return filtered;
        // Pre-evaluate sort keys for each row once to avoid re-evaluation during compare.
        var keyed = new (IterationRow Row, object?[] Keys)[filtered.Count];
        for (int i = 0; i < filtered.Count; i++)
        {
            var row = filtered[i];
            ctx.SetCurrentRowNoSnapshot(row.Fields);
            if (row.SourceRows is { } srs)
            {
                foreach (var kv in srs) ctx.SetSourceCurrentRow(kv.Key, kv.Value);
            }
            else if (primarySourceName is not null)
            {
                ctx.SetSourceCurrentRow(primarySourceName, row.Fields);
            }
            var keys = new object?[sorts.Count];
            for (int s = 0; s < sorts.Count; s++)
            {
                keys[s] = _evaluator.Evaluate(sorts[s].Expression, ctx);
            }
            keyed[i] = (row, keys);
        }
        Array.Sort(keyed, (a, b) =>
        {
            for (int s = 0; s < sorts.Count; s++)
            {
                var cmp = CompareValues(a.Keys[s], b.Keys[s]);
                if (cmp != 0)
                {
                    return sorts[s].Direction == Reporting.Data.SortDirection.Descending ? -cmp : cmp;
                }
            }
            return 0;
        });
        return keyed.Select(k => k.Row).ToList();
    }

    private static bool IsTruthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        string s => !string.IsNullOrEmpty(s),
        byte n => n != 0,
        short n => n != 0,
        int n => n != 0,
        long n => n != 0L,
        float n => n != 0f,
        double n => n != 0d,
        decimal n => n != 0m,
        _ => true,
    };

    private static int CompareValues(object? a, object? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        if (a is IComparable ca && a.GetType() == b.GetType()) return ca.CompareTo(b);
        // Numeric coercion — int vs decimal etc. compare by decimal.
        if (IsNumeric(a) && IsNumeric(b))
        {
            try { return Convert.ToDecimal(a).CompareTo(Convert.ToDecimal(b)); }
            catch { /* fall through */ }
        }
        return string.CompareOrdinal(a.ToString(), b.ToString());
    }

    private List<RenderedPage> ExecutePass(
        PaginationRequest request,
        IReadOnlyList<IterationRow> rows,
        IReadOnlyDictionary<string, List<IReadOnlyList<KeyValuePair<string, object?>>>> allSources,
        ITextMeasurer measurer,
        int totalPagesHint)
    {
        var def = request.Definition;
        // RDL <Report><Language> (carried in Metadata["Language"]) sets the report's culture, driving
        // Format/FormatDateTime/Style.Format. Opt-in: absent or invalid → null → the context's default culture.
        var culture = def.Metadata.TryGetValue("Language", out var lang) ? TryGetCulture(lang) : null;
        var ctx = new ReportExpressionContext(_evaluator, culture);
        ApplyParameters(ctx, request);
        ctx.TotalPages = totalPagesHint;
        ctx.ReportName = def.Name ?? string.Empty; // RDL Globals!ReportName

        // Expose every dataset's full rows for cross-dataset Lookup/LookupSet (SSRS-style).
        foreach (var (sourceName, sourceRows) in allSources)
        {
            ctx.RegisterDataset(sourceName, sourceRows);
        }

        // Resolve the primary-source name so we can also expose its current row via the
        // qualified-source lookup ({Fields.SourceName.X}). Same resolution as MaterializeRowsAsync
        // (shared ResolvePrimaryName) — must stay identical or materialization and render diverge.
        var primarySourceName = ResolvePrimaryName(request);
        // Look up the parent's DataSourceDefinition so EmitSubDetails can resolve relations
        // by name (e.g. "PedidosDeCliente" → ChildSource=Pedidos, ChildField=cliente_id).
        var primaryDef = primarySourceName is null
            ? null
            : request.Definition.DataSources.FirstOrDefault(d =>
                string.Equals(d.Name, primarySourceName, StringComparison.Ordinal));

        // RDL-style filter+sort, applied in the correct order:
        //   1. Data source level (defined on the DataSourceDefinition)
        //   2. Detail data region level (defined on the DetailBand)
        // Both run per row inside the report's expression context so they see Parameters
        // and Variables. Group-level filters/sorts run later, around the group transition.
        if (primaryDef is not null)
        {
            rows = ApplyFilterAndSort(rows, primaryDef.FilterExpression, primaryDef.SortExpressions,
                                       ctx, primarySourceName);
        }
        rows = ApplyFilterAndSort(rows, def.Detail.FilterExpression, def.Detail.SortExpressions,
                                   ctx, primarySourceName);

        // Prime the Report aggregate scope with the full (filtered/sorted) row set so report-scoped
        // aggregates — Sum/Avg/Count/Min/Max with no explicit scope — resolve to the dataset grand
        // total in EVERY band, including the ReportHeader and PageHeader that render before the
        // detail loop accumulates anything. The report footer sees the identical row set, so its
        // totals are unchanged; this only fixes the previously-empty header/early-band case.
        ctx.PrimeReportScope(rows.Select(r => r.Fields));

        var bandRenderer = new BandRenderer(_evaluator, _templates, measurer, allSources, primarySourceName,
            renderSubreport: (sub, subBounds, subCtx) => RenderSubreport(sub, subBounds, subCtx, request, measurer),
            mapTileResolver: request.MapTileResolver);
        var page = new PageAccumulator(def.PageSetup);

        var pageHeaderHeight = def.PageHeader?.Height ?? Unit.Zero;
        var pageFooterHeight = def.PageFooter?.Height ?? Unit.Zero;
        page.ContentBottom = def.PageSetup.IsContinuous
            ? new Unit(int.MaxValue / 2)
            : def.PageSetup.PageHeight - def.PageSetup.Margins.Bottom - pageFooterHeight;

        // Page 1 starts with the Report Header (banner that appears only once),
        // followed by the Page Header (which repeats on every subsequent page break too).
        if (def.ReportHeader is { } reportHeader)
        {
            var layout = bandRenderer.Render(reportHeader, page.Origin, ctx);
            page.Emit(layout.Primitives, layout.Height);
        }
        EmitPageHeader(def, page, bandRenderer, ctx);
        page.MarkColumnTop(); // snake columns begin below the report/page header

        // Iterate rows with group detection
        var openGroupKeys = new object?[def.Groups.Count];
        var groupOpen = new bool[def.Groups.Count];
        var newKeys = new object?[def.Groups.Count];
        IReadOnlyList<KeyValuePair<string, object?>>? lastCommittedRow = null;

        // RDL Detail.PageBreak.Start / StartAndEnd → break BEFORE the first detail iteration.
        // We hold the break until just before the first row so it only fires when there's
        // actually data; on empty data sets we fall through to NoRowsMessage instead.
        bool detailBreakStartPending = def.Detail.PageBreak == PageBreak.Start
                                        || def.Detail.PageBreak == PageBreak.StartAndEnd;

        // NoRowsMessage: when the filtered iteration produces zero rows, emit a centered
        // textbox where the Detail band would have rendered, matching RDL <NoRows>. We do
        // this BEFORE the report footer so the message is sandwiched between page headers
        // and the rest of the report — exactly where the data band would have appeared.
        if (rows.Count == 0 && !string.IsNullOrEmpty(def.Detail.NoRowsMessage))
        {
            var msg = def.Detail.NoRowsMessage!;
            var msgBand = BuildNoRowsBand(def, msg);
            EnsureRoom(page, msgBand.Height, def, bandRenderer, ctx);
            var layout = bandRenderer.Render(msgBand, page.Origin, ctx);
            page.Emit(layout.Primitives, layout.Height);
        }

        foreach (var iteration in rows)
        {
            if (detailBreakStartPending)
            {
                if (page.CurrentY > def.PageSetup.Margins.Top + pageHeaderHeight)
                {
                    BreakPage(def, page, bandRenderer, ctx);
                }
                detailBreakStartPending = false;
            }

            var row = iteration.Fields;
            ctx.PageNumber = page.PageNumber;

            // Master-detail / multi-source: publish every related source's current row before
            // any expression touches the context. Group keys and detail-band templates can use
            // {Fields.<ParentSource>.<field>} from here on. Updates happen on EVERY iteration
            // so the parent context tracks the iteration cursor correctly (Crystal/SSRS semantic).
            if (iteration.SourceRows is { Count: > 0 } sourceRows)
            {
                foreach (var kv in sourceRows)
                {
                    ctx.SetSourceCurrentRow(kv.Key, kv.Value);
                }
            }

            // Phase 1: probe the new row's group keys WITHOUT committing it to the accumulator
            // (so any group footer we emit reflects the closing group's totals, not the new row).
            ctx.SetCurrentRowNoSnapshot(row);
            for (int g = 0; g < def.Groups.Count; g++)
            {
                newKeys[g] = _evaluator.Evaluate(def.Groups[g].GroupExpression, ctx);
            }

            // Phase 2: close groups whose key changed (outermost-changed inward), in reverse order.
            // Before closing, restore the last committed row so the footer sees the *closing*
            // group's data — not the next row that triggered the close.
            bool willClose = false;
            for (int g = 0; g < def.Groups.Count; g++)
            {
                if (groupOpen[g] && !Equals(openGroupKeys[g], newKeys[g]))
                {
                    willClose = true;
                    break;
                }
            }
            if (willClose && lastCommittedRow is not null)
            {
                ctx.SetCurrentRowNoSnapshot(lastCommittedRow);
            }
            for (int g = 0; g < def.Groups.Count; g++)
            {
                if (groupOpen[g] && !Equals(openGroupKeys[g], newKeys[g]))
                {
                    for (int inner = def.Groups.Count - 1; inner >= g; inner--)
                    {
                        if (groupOpen[inner])
                        {
                            CloseGroup(def.Groups[inner], page, bandRenderer, ctx, def);
                            groupOpen[inner] = false;
                            openGroupKeys[inner] = null;
                            ctx.ResetGroup();
                        }
                    }
                    break;
                }
            }

            // Phase 3: commit the new row to accumulators (group/page/report).
            ctx.SetCurrentRow(row);
            // Re-assert source contexts after SetCurrentRow (in case the SetCurrentRow path
            // touched shared lookups). The unqualified {Fields.id} continues to use the
            // active source via the regular Fields path — no breaking change.
            if (iteration.SourceRows is { Count: > 0 } again)
            {
                foreach (var kv in again)
                {
                    ctx.SetSourceCurrentRow(kv.Key, kv.Value);
                }
            }
            else if (primarySourceName is not null)
            {
                ctx.SetSourceCurrentRow(primarySourceName, row);
            }
            // RDL CalculatedFields: evaluate each expression with the row in scope and
            // inject the result into Fields under the calculated field's name. Earlier
            // calcs are visible to later ones (sequential evaluation).
            if (primaryDef is { CalculatedFields.Count: > 0 })
            {
                foreach (var calc in primaryDef.CalculatedFields)
                {
                    object? value;
                    try { value = _evaluator.Evaluate(calc.Expression, ctx); }
                    catch { value = null; }
                    ctx.SetCalculatedField(calc.Name, value);
                }
            }
            lastCommittedRow = row;

            // Phase 4: open groups that aren't yet open.
            for (int g = 0; g < def.Groups.Count; g++)
            {
                if (!groupOpen[g])
                {
                    // RDL-style PageBreak unifies NewPageBefore + the new enum. Start /
                    // StartAndEnd / Between → break before this group instance opens.
                    var brk = def.Groups[g].EffectivePageBreak();
                    if ((brk == PageBreak.Start || brk == PageBreak.StartAndEnd || brk == PageBreak.Between)
                        && page.CurrentY > def.PageSetup.Margins.Top + pageHeaderHeight)
                    {
                        BreakPage(def, page, bandRenderer, ctx);
                    }
                    OpenGroup(def.Groups[g], page, bandRenderer, ctx, def, newKeys[g]);
                    groupOpen[g] = true;
                    openGroupKeys[g] = newKeys[g];
                }
            }
            ctx.GroupKey = def.Groups.Count > 0 ? newKeys[def.Groups.Count - 1] : null;

            // Phase 5: emit the detail band. A band taller than a full column can never fit even on a fresh
            // page, so it is split element-by-element across pages/columns (each element stays whole — text is
            // not cut mid-line in this static engine); otherwise it's placed as one unit on the page it fits.
            var detail = def.Detail;
            var detailHeight = bandRenderer.Measure(detail, ctx);
            if (detailHeight > page.FullColumnHeight)
            {
                EmitBandSplit(detail, page, bandRenderer, ctx, def, bandTarget: detailHeight);
            }
            else
            {
                EnsureRoom(page, detailHeight, def, bandRenderer, ctx);
                var detailLayout = bandRenderer.Render(detail, page.Origin, ctx);
                page.Emit(detailLayout.Primitives, detailLayout.Height);
            }

            // Phase 6: emit every declared sub-detail band — each one runs a nested loop over
            // its own DataMember (relation name or registered source). The parent context
            // stays in scope so {Fields.<Parent>.X} keeps resolving inside the child rows.
            if (detail.SubDetails.Count > 0)
            {
                EmitSubDetails(detail.SubDetails, iteration.SourceRows, primaryDef,
                               allSources, page, bandRenderer, ctx, def, primarySourceName, row);
                // After sub-details finish, the live "Fields" was overwritten by the last
                // child row — put the parent row back so subsequent groups/footers/aggregates
                // see the parent values (and not a stale child).
                ctx.SetCurrentRowNoSnapshot(row);
                if (primarySourceName is not null) ctx.SetSourceCurrentRow(primarySourceName, row);
            }
        }

        // Close remaining groups (outermost last)
        for (int inner = def.Groups.Count - 1; inner >= 0; inner--)
        {
            if (groupOpen[inner])
            {
                CloseGroup(def.Groups[inner], page, bandRenderer, ctx, def);
                groupOpen[inner] = false;
                ctx.ResetGroup();
            }
        }

        // RDL Detail.PageBreak.End / StartAndEnd → break AFTER the last detail row.
        if ((def.Detail.PageBreak == PageBreak.End || def.Detail.PageBreak == PageBreak.StartAndEnd)
            && rows.Count > 0)
        {
            BreakPage(def, page, bandRenderer, ctx);
        }

        // Report footer
        if (def.ReportFooter is { } reportFooter)
        {
            // RDL ReportFooter.PageBreak.Start / StartAndEnd → footer on its own page.
            if ((reportFooter.PageBreak == PageBreak.Start || reportFooter.PageBreak == PageBreak.StartAndEnd)
                && page.CurrentY > def.PageSetup.Margins.Top + pageHeaderHeight)
            {
                BreakPage(def, page, bandRenderer, ctx);
            }
            EnsureRoom(page, reportFooter.Height, def, bandRenderer, ctx);
            var layout = bandRenderer.Render(reportFooter, page.Origin, ctx);
            page.Emit(layout.Primitives, layout.Height);
        }

        EmitPageFooter(def, page, bandRenderer, ctx);
        page.Flush();

        return page.Pages.ToList();
    }

    /// <summary>Synthesizes a single-row DetailBand containing a centered Label with the
    /// configured NoRowsMessage. Width spans the body area; height is sized so the message
    /// is visually prominent (matches Crystal / SSRS conventions where the message takes
    /// the place the data would occupy).</summary>
    private static DetailBand BuildNoRowsBand(ReportDefinition def, string message)
    {
        var body = def.PageSetup.PageWidth - def.PageSetup.Margins.Left - def.PageSetup.Margins.Right;
        var height = Unit.FromMm(12);
        var label = new Elements.LabelElement
        {
            Text = message,
            Bounds = new Geometry.Rectangle(Unit.Zero, Unit.Zero, body, height),
            Style = Styling.Style.Default with
            {
                HorizontalAlignment = Styling.HorizontalAlignment.Center,
                VerticalAlignment = Styling.VerticalAlignment.Middle,
            },
        };
        return new DetailBand(height, new Reporting.Common.EquatableArray<Elements.ReportElement>(new[] { (Elements.ReportElement)label }));
    }

    private void OpenGroup(GroupBand group, PageAccumulator page, BandRenderer renderer,
        ReportExpressionContext ctx, ReportDefinition def, object? key)
    {
        if (group.Header is null)
        {
            return;
        }
        // KeepTogether: ensure header + footer fits on remaining space. In multi-column mode, try the next
        // column first (snake) before breaking the whole physical page — mirrors EnsureRoom.
        var needed = group.Header.Height + (group.Footer?.Height ?? Unit.Zero);
        if (group.KeepTogether && !page.Fits(needed) && !page.AdvanceColumn())
        {
            BreakPage(def, page, renderer, ctx);
        }
        else
        {
            EnsureRoom(page, group.Header.Height, def, renderer, ctx);
        }
        ctx.GroupKey = key;
        var layout = renderer.Render(group.Header, page.Origin, ctx);
        page.Emit(layout.Primitives, layout.Height);
    }

    private void CloseGroup(GroupBand group, PageAccumulator page, BandRenderer renderer,
        ReportExpressionContext ctx, ReportDefinition def)
    {
        if (group.Footer is null)
        {
            return;
        }
        EnsureRoom(page, group.Footer.Height, def, renderer, ctx);
        var layout = renderer.Render(group.Footer, page.Origin, ctx);
        page.Emit(layout.Primitives, layout.Height);

        // RDL PageBreak End/StartAndEnd/Between → break after this group instance closes.
        var brk = group.EffectivePageBreak();
        if (brk == PageBreak.End || brk == PageBreak.StartAndEnd || brk == PageBreak.Between)
        {
            BreakPage(def, page, renderer, ctx);
        }
    }

    /// <summary>Emits a band taller than a full column by splitting it ELEMENT-BY-ELEMENT across pages/columns:
    /// each element is placed whole on the page where it fits (never cut mid-element — text is not line-split in
    /// this static engine, which keeps VerticalAlignment well-defined). Elements are cut on a clean horizontal
    /// line (top-to-bottom order). A single element taller than a whole column is emitted alone on its own page
    /// so pagination always makes progress and terminates.</summary>
    private void EmitBandSplit(IBand band, PageAccumulator page, BandRenderer renderer,
        ReportExpressionContext ctx, ReportDefinition def, Unit bandTarget)
    {
        var remaining = band.Elements.OrderBy(e => e.Bounds.Y.Mils).ToList();
        Unit sliceTop = Unit.Zero; // band-space Y where the current slice begins; maps to the page's current Y
        Unit reached = Unit.Zero;  // band-space Y of the lowest content emitted so far (== contentExtent at the end)

        while (remaining.Count > 0)
        {
            var available = page.RemainingInColumn;
            var slice = new List<Elements.ReportElement>();
            Unit sliceBottom = sliceTop;
            foreach (var el in remaining)
            {
                var elemBottom = renderer.EffectiveElementBottom(el, ctx);
                if (elemBottom - sliceTop <= available)
                {
                    slice.Add(el);
                    if (elemBottom > sliceBottom)
                    {
                        sliceBottom = elemBottom;
                    }
                }
                else
                {
                    break; // this element and everything below it spill to the next slice
                }
            }

            if (slice.Count == 0)
            {
                // Nothing fits in the room left. If the column is already empty this element is taller than a
                // whole column: emit it alone (it overflows — text isn't line-split) so we make progress.
                if (page.AtColumnTop)
                {
                    var lone = remaining[0];
                    var loneBottom = renderer.EffectiveElementBottom(lone, ctx);
                    var loneOrigin = new Point(page.Origin.X, page.CurrentY - sliceTop);
                    page.Emit(renderer.RenderElements([lone], loneOrigin, ctx), loneBottom - sliceTop);
                    reached = loneBottom;
                    remaining.RemoveAt(0);
                    if (remaining.Count > 0)
                    {
                        sliceTop = remaining[0].Bounds.Y;
                        BreakOrAdvance(page, def, renderer, ctx);
                    }
                }
                else
                {
                    BreakOrAdvance(page, def, renderer, ctx); // just out of room → fresh column/page, full height
                }
                continue;
            }

            // Render the slice rebased so sliceTop lands at the page's current Y (top of the new page/column).
            var origin = new Point(page.Origin.X, page.CurrentY - sliceTop);
            page.Emit(renderer.RenderElements(slice, origin, ctx), sliceBottom - sliceTop);
            reached = sliceBottom;

            foreach (var el in slice)
            {
                remaining.Remove(el);
            }
            if (remaining.Count > 0)
            {
                sliceTop = remaining[0].Bounds.Y; // next slice starts at the first un-emitted element's top
                BreakOrAdvance(page, def, renderer, ctx);
            }
        }

        // Preserve the band's declared height: a band whose declared Height exceeds its content (intentional
        // trailing whitespace), or an empty oversized band, must still consume that height — the same total the
        // non-split path would (Measure = Max(band.Height, content)). Flow the trailing whitespace across pages.
        // A shrink-opt-in band has bandTarget == content, so trailing is zero and this is a no-op.
        var trailing = bandTarget - reached;
        while (trailing > Unit.Zero)
        {
            var available = page.RemainingInColumn;
            if (available <= Unit.Zero)
            {
                BreakOrAdvance(page, def, renderer, ctx);
                continue;
            }
            if (trailing <= available)
            {
                page.Emit([], trailing);
                break;
            }
            page.Emit([], available);
            trailing -= available;
            BreakOrAdvance(page, def, renderer, ctx);
        }
    }

    /// <summary>Snake to the next column if one is available, otherwise break to a new page.</summary>
    private void BreakOrAdvance(PageAccumulator page, ReportDefinition def, BandRenderer renderer, ReportExpressionContext ctx)
    {
        if (!page.AdvanceColumn())
        {
            BreakPage(def, page, renderer, ctx);
        }
    }

    private void EnsureRoom(PageAccumulator page, Unit needed, ReportDefinition def,
        BandRenderer renderer, ReportExpressionContext ctx)
    {
        if (page.Fits(needed))
        {
            return;
        }
        // Multi-column (snake): move to the next column on the same physical page before breaking it.
        if (page.AdvanceColumn())
        {
            return;
        }
        BreakPage(def, page, renderer, ctx);
    }

    private void BreakPage(ReportDefinition def, PageAccumulator page, BandRenderer renderer, ReportExpressionContext ctx)
    {
        EmitPageFooter(def, page, renderer, ctx);
        page.Flush();
        ctx.ResetPage();
        ctx.PageNumber = page.PageNumber;
        EmitPageHeader(def, page, renderer, ctx);
        page.MarkColumnTop(); // snake columns on the new page begin below its page header
    }

    private static void EmitPageHeader(ReportDefinition def, PageAccumulator page, BandRenderer renderer, IReportExpressionContext ctx)
    {
        if (def.PageHeader is null)
        {
            return;
        }
        // First-page suppression: known up front (page 1 is page 1 on both passes).
        if (page.PageNumber == 1 && !def.PageHeader.PrintOnFirstPage)
        {
            return;
        }
        // Last-page suppression: needs the total page count, which is only known on the second pass
        // (ctx.TotalPages > 0). Suppressing the last page's header never changes the count — the content
        // that fit on the last page WITH the header still fits without it, and nothing flows in after it.
        if (ctx.TotalPages > 0 && page.PageNumber == ctx.TotalPages && !def.PageHeader.PrintOnLastPage)
        {
            return;
        }
        var layout = renderer.Render(def.PageHeader, page.Origin, ctx);
        page.Emit(layout.Primitives, layout.Height);
    }

    private static void EmitPageFooter(ReportDefinition def, PageAccumulator page, BandRenderer renderer, IReportExpressionContext ctx)
    {
        if (def.PageFooter is null)
        {
            return;
        }
        // First-page / last-page suppression. The footer is bottom-anchored (EmitFixed below does NOT consume
        // content Y), so suppressing it never reflows content — page count is unaffected on either page.
        // Last-page gating needs the total, known only on the second pass (ctx.TotalPages > 0).
        if (page.PageNumber == 1 && !def.PageFooter.PrintOnFirstPage)
        {
            return;
        }
        if (ctx.TotalPages > 0 && page.PageNumber == ctx.TotalPages && !def.PageFooter.PrintOnLastPage)
        {
            return;
        }
        // PageFooter is anchored at the bottom of the page: origin Y = (pageHeight - margin.Bottom - footer.Height).
        if (def.PageSetup.IsContinuous)
        {
            // No bottom anchor for continuous paper — emit inline at current Y.
            var layout = renderer.Render(def.PageFooter, page.Origin, ctx);
            page.Emit(layout.Primitives, layout.Height);
            return;
        }
        var footerY = def.PageSetup.PageHeight - def.PageSetup.Margins.Bottom - def.PageFooter.Height;
        var origin = new Point(def.PageSetup.Margins.Left, footerY);
        var fixedLayout = renderer.Render(def.PageFooter, origin, ctx);
        page.EmitFixed(fixedLayout.Primitives);
    }

    private void ApplyParameters(ReportExpressionContext ctx, PaginationRequest request)
    {
        foreach (var p in request.Definition.Parameters)
        {
            object? value;
            if (request.Parameters.TryGetValue(p.Name, out var v))
            {
                value = v; // prompted/host-supplied value wins
            }
            else if (p.DefaultValue is not null)
            {
                value = p.DefaultValue; // literal default
            }
            else if (!string.IsNullOrEmpty(p.DefaultValueExpression))
            {
                // Expression default (=Today(), =DateAdd(...), =Parameters!Other.Value). Evaluated here, in
                // declaration order, so a default referencing an earlier parameter sees its seeded value. A
                // failing expression falls back to null rather than aborting the run.
                try { value = _evaluator.Evaluate(p.DefaultValueExpression, ctx); }
                catch { value = null; }
            }
            else
            {
                value = null;
            }
            ctx.ParametersStore.Set(p.Name, value);
        }
    }

    [GeneratedRegex(@"\bPage\.(Total|TotalPages)\b", RegexOptions.Compiled)]
    private static partial Regex PageTotalReference();

    /// <summary>True when the page header or footer must be suppressed on the last page
    /// (<c>PrintOnLastPage=false</c>). This requires a second pass to learn the total page count, because the
    /// last page can't be identified during the forward-only first pass.</summary>
    private static bool UsesLastPageGating(ReportDefinition def)
        => def.PageHeader is { PrintOnLastPage: false } || def.PageFooter is { PrintOnLastPage: false };

    /// <summary>Checks whether the definition references <c>Page.Total</c> anywhere — used to
    /// decide whether a second pass is necessary.</summary>
    private static bool UsesTotalPages(ReportDefinition def)
    {
        foreach (var element in EnumerateAllElements(def))
        {
            if (element is Elements.TextBoxElement tb && PageTotalReference().IsMatch(tb.Expression))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<Elements.ReportElement> EnumerateAllElements(ReportDefinition def)
    {
        foreach (var e in Enumerate(def.ReportHeader)) yield return e;
        foreach (var e in Enumerate(def.PageHeader)) yield return e;
        foreach (var group in def.Groups)
        {
            foreach (var e in Enumerate(group.Header)) yield return e;
            foreach (var e in Enumerate(group.Footer)) yield return e;
        }
        foreach (var e in def.Detail.Elements) yield return e;
        foreach (var e in Enumerate(def.PageFooter)) yield return e;
        foreach (var e in Enumerate(def.ReportFooter)) yield return e;
    }

    private static IEnumerable<Elements.ReportElement> Enumerate(IBand? band)
        => band is null ? [] : band.Elements;
}
