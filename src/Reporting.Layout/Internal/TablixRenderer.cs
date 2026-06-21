using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

/// <summary>
/// Renders a <see cref="TablixElement"/> as a banded table — the most common Tablix shape.
/// The header cells (row 0 of <see cref="TablixElement.Cells"/>) are drawn once; the detail
/// cells (row 1) form a template repeated for every row of the bound data source. The table
/// auto-grows downward and reports its actual height so the band layout accounts for it.
/// </summary>
/// <remarks>
/// Two shapes are covered: the flat table (static columns × data rows, equivalent to an RDL
/// <c>Table</c>) and the <b>matrix/crosstab</b> with <b>nested</b> row and column groups
/// (<see cref="RenderMatrix"/>) — outer levels span their children in an outline layout.
/// </remarks>
internal static class TablixRenderer
{
    private static readonly Color HeaderBg = Color.FromHex("#F1F5F9");   // slate-100
    private static readonly Color GridColor = Color.FromHex("#CBD5E1");  // slate-300
    private static readonly Color HeaderText = Color.FromHex("#0F172A"); // slate-900
    private static readonly Color BodyText = Color.FromHex("#1F2937");   // gray-800
    private const double RowHeightMm = 6.5;
    private const double PadMm = 1.0;

    public static IEnumerable<LayoutPrimitive> Render(
        TablixElement tablix,
        Rectangle bounds,
        IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> rows,
        ExpressionEvaluator ev,
        TemplateRenderer templates,
        IReportExpressionContext baseCtx,
        out Unit actualHeight)
    {
        var list = new List<LayoutPrimitive>();
        actualHeight = bounds.Height;

        // RDL NoRowsMessage: an empty dataset shows a centred message in place of the grid (both modes).
        if (rows.Count == 0 && tablix.NoRowsMessage is { Length: > 0 } noRows)
        {
            list.Add(new DrawTextPrimitive
            {
                Text = Resolve(ev, templates, noRows, baseCtx),
                Bounds = bounds,
                Style = new TextStyle(new Font("Arial", 9, FontStyle.Italic), BodyText,
                    HorizontalAlignment.Center, VerticalAlignment.Middle, WordWrap: true),
                SourceElementId = tablix.Id,
            });
            return list;
        }

        // Matrix / pivot mode: a row group AND a column group turn the Tablix into a crosstab
        // (any number of nested levels on each axis).
        if (tablix.RowGroups.Count >= 1 && tablix.ColumnGroups.Count >= 1)
        {
            return RenderMatrix(tablix, bounds, rows, ev, templates, baseCtx, out actualHeight);
        }

        // Split cells into the header template (row 0) and the detail template (row 1),
        // indexed by column. Column count is the widest column index seen.
        var headerCells = new Dictionary<int, ReportElement?>();
        var detailCells = new Dictionary<int, ReportElement?>();
        int colCount = 0;
        foreach (var cell in tablix.Cells)
        {
            colCount = Math.Max(colCount, cell.ColumnIndex + 1);
            if (cell.RowIndex == 0)
            {
                headerCells[cell.ColumnIndex] = cell.Content;
            }
            else if (cell.RowIndex == 1)
            {
                detailCells[cell.ColumnIndex] = cell.Content;
            }
        }

        double x0 = bounds.X.ToMm(), y0 = bounds.Y.ToMm(), w = bounds.Width.ToMm();
        if (colCount == 0 || w <= 1)
        {
            return list;
        }

        // Column left edges: proportional to ColumnWidths weights when supplied, else equal.
        double[] colLeft = ComputeColumnEdges(tablix.ColumnWidths, colCount, x0, w);
        bool hasHeader = headerCells.Count > 0;
        double y = y0;

        if (hasHeader)
        {
            list.Add(Fill(x0, y, w, RowHeightMm, HeaderBg, tablix.Id));
            for (int c = 0; c < colCount; c++)
            {
                headerCells.TryGetValue(c, out var content);
                EmitStyledCell(list, content, Text(content, ev, templates, baseCtx),
                    colLeft[c], y, colLeft[c + 1] - colLeft[c], defaultBold: true, HeaderText, ev, baseCtx, tablix.Id);
            }
            y += RowHeightMm;
        }

        foreach (var row in rows)
        {
            var rowCtx = new RowScopedContext(baseCtx, row);
            for (int c = 0; c < colCount; c++)
            {
                detailCells.TryGetValue(c, out var content);
                EmitStyledCell(list, content, Text(content, ev, templates, rowCtx),
                    colLeft[c], y, colLeft[c + 1] - colLeft[c], defaultBold: false, BodyText, ev, rowCtx, tablix.Id);
            }
            y += RowHeightMm;
        }

        double totalH = y - y0;
        var pen = new PenStyle(GridColor, Unit.FromPoint(0.5));
        int rowLines = (hasHeader ? 1 : 0) + rows.Count;
        for (int r = 0; r <= rowLines; r++)
        {
            double ly = y0 + r * RowHeightMm;
            list.Add(Line(x0, ly, x0 + w, ly, pen, tablix.Id));
        }
        for (int c = 0; c <= colCount; c++)
        {
            double lx = colLeft[c];
            list.Add(Line(lx, y0, lx, y0 + totalH, pen, tablix.Id));
        }

        actualHeight = Unit.FromMm(totalH);
        return list;
    }

    /// <summary>Renders a crosstab with <b>nested</b> row and column groups. Each row-group level is a
    /// column of headers down the left; each column-group level is a row of headers across the top;
    /// outer levels span their children (drawn once at the top of each block — an "outline" look).
    /// Every body cell = the SUM of the value expression over the rows whose full row-path × column-path
    /// land on that leaf intersection. The body template is the first cell at (row≥1, col≥1); the corner
    /// label is cell (0,0). A single level on each axis collapses to the classic 1×1 matrix.</summary>
    private static IEnumerable<LayoutPrimitive> RenderMatrix(
        TablixElement tablix,
        Rectangle bounds,
        IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> rows,
        ExpressionEvaluator ev,
        TemplateRenderer templates,
        IReportExpressionContext baseCtx,
        out Unit actualHeight)
    {
        var list = new List<LayoutPrimitive>();
        actualHeight = bounds.Height;

        var rowGroups = tablix.RowGroups.Where(g => !string.IsNullOrWhiteSpace(g.GroupExpression)).ToList();
        var colGroups = tablix.ColumnGroups.Where(g => !string.IsNullOrWhiteSpace(g.GroupExpression)).ToList();
        var rowExprs = rowGroups.Select(g => g.GroupExpression!).ToList();
        var colExprs = colGroups.Select(g => g.GroupExpression!).ToList();
        if (rowExprs.Count == 0 || colExprs.Count == 0)
        {
            return list;
        }
        int nRowLevels = rowExprs.Count, nColLevels = colExprs.Count;

        TextBoxElement? body = null;
        ReportElement? corner = null;
        foreach (var cell in tablix.Cells)
        {
            if (cell.RowIndex == 0 && cell.ColumnIndex == 0) corner = cell.Content;
            if (cell.RowIndex >= 1 && cell.ColumnIndex >= 1 && cell.Content is TextBoxElement tb) body ??= tb;
        }
        string valueExpr = body?.Expression ?? string.Empty;
        string? format = body?.Style.Format;
        string cornerText = (corner as LabelElement)?.Text ?? string.Empty;

        // One pass over the data: grow the ordered row/column group trees and accumulate the SUM per
        // full (rowPath, columnPath) leaf intersection.
        const char Sep = '\u0001'; // unlikely-in-data path separator
        var rowTree = new GroupNode();
        var colTree = new GroupNode();
        var sums = new Dictionary<(string Row, string Col), double>();
        foreach (var row in rows)
        {
            var rc = new RowScopedContext(baseCtx, row);
            var rk = new string[nRowLevels];
            for (int l = 0; l < nRowLevels; l++) rk[l] = Resolve(ev, templates, rowExprs[l], rc);
            var ck = new string[nColLevels];
            for (int l = 0; l < nColLevels; l++) ck[l] = Resolve(ev, templates, colExprs[l], rc);
            rowTree.Add(rk, rc);
            colTree.Add(ck, rc);
            string rKey = string.Join(Sep, rk), cKey = string.Join(Sep, ck);
            double v = EvalDouble(ev, valueExpr, rc);
            sums.TryGetValue((rKey, cKey), out var cur);
            sums[(rKey, cKey)] = cur + v;
        }
        // RDL group sorting: order each axis' group instances by their SortExpression (per-row keys like a
        // group label or field; aggregate sort keys are a follow-up — see GroupNode.Sort). No-op when unset.
        rowTree.Sort(rowGroups, ev);
        colTree.Sort(colGroups, ev);
        var rowLeaves = rowTree.Leaves(nRowLevels);
        var colLeaves = colTree.Leaves(nColLevels);
        if (rowLeaves.Count == 0 || colLeaves.Count == 0)
        {
            return list;
        }

        double x0 = bounds.X.ToMm(), y0 = bounds.Y.ToMm(), w = bounds.Width.ToMm();

        // Configurable total labels (SSRS-style): "{0}" in the subtotal label is the group value.
        string subLabelFmt = tablix.SubtotalLabel ?? "Total {0}";
        string grandLabel = tablix.GrandTotalLabel ?? "Total geral";

        // Visual columns: leaf data columns, optionally interleaved with a subtotal column after each outer
        // column-group block and a grand-total column at the right (the column-axis mirror of subtotal rows).
        bool colSub = tablix.ColumnSubtotals;
        var vcols = new List<VCol>();
        for (int j = 0; j < colLeaves.Count; j++)
        {
            vcols.Add(VCol.LeafCol(j));
            if (colSub && nColLevels >= 2)
            {
                for (int level = nColLevels - 2; level >= 0; level--)
                {
                    string pfx = string.Join(Sep, colLeaves[j].Take(level + 1));
                    bool boundary = j == colLeaves.Count - 1
                        || string.Join(Sep, colLeaves[j + 1].Take(level + 1)) != pfx;
                    if (!boundary)
                    {
                        continue;
                    }
                    int start = j;
                    while (start > 0 && string.Join(Sep, colLeaves[start - 1].Take(level + 1)) == pfx)
                    {
                        start--;
                    }
                    vcols.Add(VCol.Subtotal(start, j, SafeLabel(subLabelFmt, colLeaves[j][level], baseCtx.Culture)));
                }
            }
        }
        if (colSub)
        {
            vcols.Add(VCol.Subtotal(0, colLeaves.Count - 1, grandLabel)); // grand-total column
        }

        int totalCols = nRowLevels + vcols.Count;
        double colW = w / totalCols;
        double headerH = nColLevels * RowHeightMm;

        // Value of one visual column over a block of row leaves [rStart..rEnd] — a single leaf×leaf cell, a
        // row block × column block (subtotal), or the whole grid (grand total), all via the same sum.
        double CellValue(int rStart, int rEnd, VCol vc)
        {
            double t = 0;
            for (int r = rStart; r <= rEnd; r++)
            {
                string rKey = string.Join(Sep, rowLeaves[r]);
                for (int c = vc.Start; c <= vc.End; c++)
                {
                    sums.TryGetValue((rKey, string.Join(Sep, colLeaves[c])), out var s);
                    t += s;
                }
            }
            return t;
        }

        // Header band (corner + column-header rows) gets the header fill.
        list.Add(Fill(x0, y0, w, headerH, HeaderBg, tablix.Id));
        list.Add(CellText(cornerText, x0, y0, colW, bold: true, HeaderText, tablix.Id));

        // Column headers: leaf columns get each level's value spanning the columns it covers (true span);
        // a subtotal/grand column gets one label across the whole header band.
        int vh = 0;
        while (vh < vcols.Count)
        {
            if (vcols[vh].IsSubtotal)
            {
                list.Add(CellText(vcols[vh].Label, x0 + (nRowLevels + vh) * colW, y0, colW, bold: true, HeaderText, tablix.Id));
                vh++;
                continue;
            }
            int runEnd = vh; // a maximal run of consecutive leaf columns, spanned per level
            while (runEnd + 1 < vcols.Count && !vcols[runEnd + 1].IsSubtotal) runEnd++;
            for (int cl = 0; cl < nColLevels; cl++)
            {
                int k = vh;
                while (k <= runEnd)
                {
                    string prefix = string.Join(Sep, colLeaves[vcols[k].Leaf].Take(cl + 1));
                    int span = 1;
                    while (k + span <= runEnd && string.Join(Sep, colLeaves[vcols[k + span].Leaf].Take(cl + 1)) == prefix)
                    {
                        span++;
                    }
                    list.Add(CellText(colLeaves[vcols[k].Leaf][cl], x0 + (nRowLevels + k) * colW, y0 + cl * RowHeightMm,
                        span * colW, bold: true, HeaderText, tablix.Id));
                    k += span;
                }
            }
            vh = runEnd + 1;
        }

        // Emits one total row (subtotal of a group block, or the grand total): a spanning label over the
        // row-header columns + each visual column's block sum, all bold on the header fill.
        void TotalRow(double yy, string label, int start, int end)
        {
            list.Add(Fill(x0, yy, w, RowHeightMm, HeaderBg, tablix.Id));
            list.Add(CellText(label, x0, yy, nRowLevels * colW, bold: true, HeaderText, tablix.Id));
            for (int vIdx = 0; vIdx < vcols.Count; vIdx++)
            {
                list.Add(CellText(FormatNumber(CellValue(start, end, vcols[vIdx]), format, baseCtx.Culture),
                    x0 + (nRowLevels + vIdx) * colW, yy, colW, bold: true, HeaderText, tablix.Id));
            }
        }

        // Data rows: nested row headers (merged look) + each visual column's value, optionally with SSRS-style
        // subtotal rows after each outer group block and a grand total at the bottom.
        bool rowSub = tablix.RowSubtotals;
        double y = y0 + headerH;
        int bodyRows = 0; // visual rows below the header band (data + subtotals + grand total) for grid lines
        var prevRowPrefix = new string?[nRowLevels];
        for (int i = 0; i < rowLeaves.Count; i++)
        {
            for (int rl = 0; rl < nRowLevels; rl++)
            {
                string prefix = string.Join(Sep, rowLeaves[i].Take(rl + 1));
                if (prefix != prevRowPrefix[rl])
                {
                    list.Add(CellText(rowLeaves[i][rl], x0 + rl * colW, y, colW, bold: true, HeaderText, tablix.Id));
                    prevRowPrefix[rl] = prefix;
                    for (int d = rl + 1; d < nRowLevels; d++) prevRowPrefix[d] = null; // deeper levels restart
                }
            }
            for (int vIdx = 0; vIdx < vcols.Count; vIdx++)
            {
                bool sub = vcols[vIdx].IsSubtotal;
                list.Add(CellText(FormatNumber(CellValue(i, i, vcols[vIdx]), format, baseCtx.Culture),
                    x0 + (nRowLevels + vIdx) * colW, y, colW, bold: sub, sub ? HeaderText : BodyText, tablix.Id));
            }
            y += RowHeightMm;
            bodyRows++;

            // Subtotal each outer group level (innermost-outer first) whose block ends at this leaf.
            if (rowSub && nRowLevels >= 2)
            {
                for (int level = nRowLevels - 2; level >= 0; level--)
                {
                    string pfx = string.Join(Sep, rowLeaves[i].Take(level + 1));
                    bool boundary = i == rowLeaves.Count - 1
                        || string.Join(Sep, rowLeaves[i + 1].Take(level + 1)) != pfx;
                    if (!boundary)
                    {
                        continue;
                    }
                    int start = i;
                    while (start > 0 && string.Join(Sep, rowLeaves[start - 1].Take(level + 1)) == pfx)
                    {
                        start--;
                    }
                    TotalRow(y, SafeLabel(subLabelFmt, rowLeaves[i][level], baseCtx.Culture), start, i);
                    y += RowHeightMm;
                    bodyRows++;
                    for (int d = level; d < nRowLevels; d++) prevRowPrefix[d] = null; // next block redraws headers
                }
            }
        }
        if (rowSub)
        {
            TotalRow(y, grandLabel, 0, rowLeaves.Count - 1);
            y += RowHeightMm;
            bodyRows++;
        }

        double totalH = y - y0;
        var pen = new PenStyle(GridColor, Unit.FromPoint(0.5));
        int gridRows = nColLevels + bodyRows;
        for (int r = 0; r <= gridRows; r++)
        {
            double ly = y0 + r * RowHeightMm;
            list.Add(Line(x0, ly, x0 + w, ly, pen, tablix.Id));
        }
        for (int c = 0; c <= totalCols; c++)
        {
            double lx = x0 + c * colW;
            list.Add(Line(lx, y0, lx, y0 + totalH, pen, tablix.Id));
        }

        actualHeight = Unit.FromMm(totalH);
        return list;
    }

    /// <summary>A rendered matrix column: either a single leaf data column (<c>Leaf</c> ≥ 0, summing just
    /// that column-leaf), or a subtotal/grand-total column (<see cref="IsSubtotal"/>, summing the column-leaf
    /// block <c>[Start..End]</c> per row). Built once so headers, data rows and total rows all iterate the
    /// same column list.</summary>
    private readonly record struct VCol(int Leaf, int Start, int End, string Label, bool IsSubtotal)
    {
        public static VCol LeafCol(int j) => new(j, j, j, string.Empty, false);
        public static VCol Subtotal(int start, int end, string label) => new(-1, start, end, label, true);
    }

    /// <summary>An ordered prefix tree of group-key paths: children keep first-seen insertion order so
    /// flattening to <see cref="Leaves"/> yields the hierarchical row/column ordering of a crosstab.</summary>
    private sealed class GroupNode
    {
        private readonly List<string> _order = new();
        private readonly Dictionary<string, GroupNode> _children = new(StringComparer.Ordinal);
        private IReportExpressionContext? _sample; // first row that reached this node (for SortExpression)

        public void Add(IReadOnlyList<string> path, IReportExpressionContext rowCtx)
        {
            var cur = this;
            foreach (var k in path)
            {
                if (!cur._children.TryGetValue(k, out var child))
                {
                    child = new GroupNode();
                    child._sample = rowCtx;
                    cur._children[k] = child;
                    cur._order.Add(k);
                }
                cur = child;
            }
        }

        /// <summary>Orders siblings at each level by that level's group SortExpression, evaluated on a
        /// representative (first-seen) row of each group. This correctly orders by per-row keys (group label,
        /// a field); an AGGREGATE SortExpression (e.g. Sum) resolves against the whole dataset, not the
        /// group, so it can't order groups — use a per-row sort key for now. Levels without a SortExpression
        /// keep data order; ties keep data order too (stable).</summary>
        public void Sort(IReadOnlyList<TablixGroup> levels, ExpressionEvaluator ev, int depth = 0)
        {
            if (depth < levels.Count && !string.IsNullOrWhiteSpace(levels[depth].SortExpression))
            {
                var expr = levels[depth].SortExpression!;
                bool desc = levels[depth].SortDescending;
                // Stable sort: List<T>.Sort is unstable (introsort), so carry the original index as a
                // tiebreaker — equal sort keys keep their data order, and the result is deterministic.
                var ordered = _order.Select((k, i) => (Key: k, Index: i)).ToList();
                ordered.Sort((a, b) =>
                {
                    int cmp = CompareSortValues(EvalSort(ev, expr, _children[a.Key]._sample),
                                                EvalSort(ev, expr, _children[b.Key]._sample));
                    if (cmp != 0)
                    {
                        return desc ? -cmp : cmp;
                    }
                    return a.Index.CompareTo(b.Index); // tie → data order, regardless of direction
                });
                _order.Clear();
                _order.AddRange(ordered.Select(t => t.Key));
            }
            foreach (var child in _children.Values)
            {
                child.Sort(levels, ev, depth + 1);
            }
        }

        public List<string[]> Leaves(int depth)
        {
            var result = new List<string[]>();
            var path = new string[depth];
            void Dfs(GroupNode node, int d)
            {
                if (d == depth)
                {
                    result.Add((string[])path.Clone());
                    return;
                }
                foreach (var k in node._order)
                {
                    path[d] = k;
                    Dfs(node._children[k], d + 1);
                }
            }
            Dfs(this, 0);
            return result;
        }
    }

    private static double EvalDouble(ExpressionEvaluator ev, string expr, IReportExpressionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return 0;
        }
        try
        {
            var v = ev.Evaluate(expr, ctx);
            return v is null ? 0 : Convert.ToDouble(v, ctx.Culture);
        }
        catch
        {
            return 0;
        }
    }

    private static object? EvalSort(ExpressionEvaluator ev, string expr, IReportExpressionContext? ctx)
    {
        if (ctx is null)
        {
            return null;
        }
        try
        {
            return ev.Evaluate(expr, ctx);
        }
        catch
        {
            return null; // a bad SortExpression must not break the render — fall back to data order for that pair
        }
    }

    // Orders sort keys by natural type when possible: same-typed comparables (DateTime/decimal/…) compare
    // directly; otherwise numeric when both parse as numbers, else ordinal string. Nulls sort first.
    private static int CompareSortValues(object? a, object? b)
    {
        if (a is null)
        {
            return b is null ? 0 : -1;
        }
        if (b is null)
        {
            return 1;
        }
        // Same runtime type that's comparable (DateTime, decimal, …) → natural order. Strings are excluded
        // so they use the ordinal path below (string.CompareTo is culture-sensitive).
        if (a is not string && b is not string && a.GetType() == b.GetType() && a is IComparable cmp)
        {
            return cmp.CompareTo(b);
        }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sa = Convert.ToString(a, inv) ?? string.Empty;
        var sb = Convert.ToString(b, inv) ?? string.Empty;
        if (double.TryParse(sa, System.Globalization.NumberStyles.Any, inv, out var da)
            && double.TryParse(sb, System.Globalization.NumberStyles.Any, inv, out var db))
        {
            return da.CompareTo(db);
        }
        return string.Compare(sa, sb, StringComparison.Ordinal);
    }

    private static string FormatNumber(double v, string? format, System.Globalization.CultureInfo culture)
    {
        try
        {
            return string.Format(culture, "{0:" + (string.IsNullOrEmpty(format) ? "N2" : format) + "}", v);
        }
        catch
        {
            return v.ToString(culture);
        }
    }

    /// <summary>Formats a user-supplied total label ("Total {0}") with the group value. A malformed
    /// template ("Total {1}", "Total {") would otherwise make <see cref="string.Format(IFormatProvider,
    /// string, object?)"/> throw and abort the whole render — so we fall back to the raw template, mirroring
    /// <see cref="FormatNumber"/>'s defensive guard.</summary>
    private static string SafeLabel(string format, string arg, System.Globalization.CultureInfo culture)
    {
        try
        {
            return string.Format(culture, format, arg);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    /// <summary>Returns the <paramref name="colCount"/>+1 column boundary x-coordinates across
    /// <paramref name="totalW"/> starting at <paramref name="x0"/>. Honours relative
    /// <paramref name="weights"/> when present (zero/missing entries fall back to the average
    /// weight so a partial spec still produces sane columns); otherwise splits equally.</summary>
    private static double[] ComputeColumnEdges(
        Reporting.Common.EquatableArray<double> weights, int colCount, double x0, double totalW)
    {
        var edges = new double[colCount + 1];
        var w = new double[colCount];
        double sum = 0;
        for (int c = 0; c < colCount; c++)
        {
            double wt = c < weights.Count && weights[c] > 0 ? weights[c] : 0;
            w[c] = wt;
            sum += wt;
        }
        if (sum <= 0)
        {
            for (int c = 0; c <= colCount; c++)
            {
                edges[c] = x0 + totalW * c / colCount;
            }
            return edges;
        }
        // Backfill any unset columns with the average of the specified weights, then normalise.
        double avg = sum / colCount;
        double total = 0;
        for (int c = 0; c < colCount; c++)
        {
            if (w[c] <= 0) w[c] = avg;
            total += w[c];
        }
        double acc = 0;
        edges[0] = x0;
        for (int c = 0; c < colCount; c++)
        {
            acc += w[c];
            edges[c + 1] = x0 + totalW * acc / total;
        }
        return edges;
    }

    private static string Text(ReportElement? content, ExpressionEvaluator ev, TemplateRenderer templates, IReportExpressionContext ctx)
        => content switch
        {
            LabelElement lbl => lbl.Text ?? string.Empty,
            TextBoxElement tb => Resolve(ev, templates, tb.Expression, ctx, tb.Style.Format),
            _ => string.Empty,
        };

    private static string Resolve(ExpressionEvaluator ev, TemplateRenderer templates, string expr, IReportExpressionContext ctx, string? elementFormat = null)
    {
        if (string.IsNullOrEmpty(expr))
        {
            return string.Empty;
        }
        // SSRS-style Format property: a single-value cell ("{Fields.preco}" or a lone expression) with no
        // inline ":format" honours the cell's Format. Mirrors BandRenderer.ResolveText so a flat Tablix
        // cell formats the same as a band textbox.
        if (!string.IsNullOrEmpty(elementFormat))
        {
            var single = TemplateRenderer.TryGetSingleExpression(expr, out var inner) ? inner
                : !TemplateRenderer.HasPlaceholders(expr) ? expr
                : null;
            if (single is not null)
            {
                try
                {
                    return ValueFormatter.Format(ev.Evaluate(single, ctx), elementFormat, ctx.Culture);
                }
                catch (ExpressionParseException)
                {
                    return expr;
                }
            }
        }
        if (TemplateRenderer.HasPlaceholders(expr))
        {
            return templates.Render(expr, ctx);
        }
        try
        {
            var v = ev.Evaluate(expr, ctx);
            return v is null ? string.Empty : Convert.ToString(v, ctx.Culture) ?? string.Empty;
        }
        catch (ExpressionParseException)
        {
            // Not an expression — treat as a literal label (mirrors BandRenderer.ResolveText).
            return expr;
        }
    }

    private static DrawRectanglePrimitive Fill(double xMm, double yMm, double wMm, double hMm, Color color, string? id)
        => new()
        {
            Bounds = Rect(xMm, yMm, wMm, hMm),
            Fill = new BrushStyle(color),
            Pen = null,
            SourceElementId = id,
        };

    private static DrawLinePrimitive Line(double x1, double y1, double x2, double y2, PenStyle pen, string? id)
        => new()
        {
            From = new Point(Unit.FromMm(x1), Unit.FromMm(y1)),
            To = new Point(Unit.FromMm(x2), Unit.FromMm(y2)),
            Pen = pen,
            Bounds = Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)),
            SourceElementId = id,
        };

    private static DrawTextPrimitive CellText(string text, double xMm, double yMm, double wMm, bool bold, Color color, string? id)
    {
        var font = new Font("Arial", 8.5, bold ? FontStyle.Bold : FontStyle.Regular);
        var style = new TextStyle(font, color, HorizontalAlignment.Left, VerticalAlignment.Middle, WordWrap: false);
        return new DrawTextPrimitive
        {
            Text = text ?? string.Empty,
            Bounds = Rect(xMm + PadMm, yMm, Math.Max(0, wMm - 2 * PadMm), RowHeightMm),
            Style = style,
            SourceElementId = id,
        };
    }

    // Emits a flat-table cell honouring the content's EFFECTIVE style — its own Style overlaid with any
    // matching conditional format (shared StyleResolver). So a cell can be bold / coloured / right-aligned
    // AND conditionally highlighted: e.g. negative values in red text over a background fill, like a band
    // textbox. Falls back to the table defaults per property when the cell leaves it unset.
    private static void EmitStyledCell(List<LayoutPrimitive> list, ReportElement? content, string text,
        double xMm, double yMm, double wMm, bool defaultBold, Color defaultColor,
        ExpressionEvaluator ev, IReportExpressionContext ctx, string? id)
    {
        var s = content is null ? null : StyleResolver.Resolve(content, ev, ctx);
        if (s?.BackColor is { } bg)
        {
            list.Add(Fill(xMm, yMm, wMm, RowHeightMm, bg, id));
        }
        var font = s?.Font ?? new Font("Arial", 8.5, defaultBold ? FontStyle.Bold : FontStyle.Regular);
        var align = s?.HorizontalAlignment ?? HorizontalAlignment.Left;
        var style = new TextStyle(font, s?.ForeColor ?? defaultColor, align, VerticalAlignment.Middle, WordWrap: false);
        list.Add(new DrawTextPrimitive
        {
            Text = text ?? string.Empty,
            Bounds = Rect(xMm + PadMm, yMm, Math.Max(0, wMm - 2 * PadMm), RowHeightMm),
            Style = style,
            SourceElementId = id,
        });
    }

    private static Rectangle Rect(double xMm, double yMm, double wMm, double hMm)
        => new(Unit.FromMm(xMm), Unit.FromMm(yMm), Unit.FromMm(Math.Max(0, wMm)), Unit.FromMm(Math.Max(0, hMm)));
}
