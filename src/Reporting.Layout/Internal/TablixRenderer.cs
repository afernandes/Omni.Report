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
/// v1 covers the flat table (static columns × data rows) with gridlines and a header band —
/// equivalent to an RDL <c>Table</c> data region. Nested row/column groups and matrix pivots
/// (the full Tablix) remain a future enhancement; the model already round-trips them.
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

        // Matrix / pivot mode: a row group AND a column group turn the Tablix into a crosstab.
        // (Single-level groups in v1; nested groups remain a future enhancement.)
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
                list.Add(CellText(Text(content, ev, templates, baseCtx),
                    colLeft[c], y, colLeft[c + 1] - colLeft[c], bold: true, HeaderText, tablix.Id));
            }
            y += RowHeightMm;
        }

        foreach (var row in rows)
        {
            var rowCtx = new RowScopedContext(baseCtx, row);
            for (int c = 0; c < colCount; c++)
            {
                detailCells.TryGetValue(c, out var content);
                list.Add(CellText(Text(content, ev, templates, rowCtx),
                    colLeft[c], y, colLeft[c + 1] - colLeft[c], bold: false, BodyText, tablix.Id));
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

    /// <summary>Renders a crosstab: distinct values of the row group down the left, distinct values
    /// of the column group across the top, and each body cell = the SUM of the cell's value
    /// expression over the rows that fall in that (rowValue, columnValue) intersection. Single-level
    /// groups; the body template is the cell at (row≥1, col≥1), the corner label is cell (0,0).</summary>
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

        string rowExpr = tablix.RowGroups[0].GroupExpression ?? string.Empty;
        string colExpr = tablix.ColumnGroups[0].GroupExpression ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rowExpr) || string.IsNullOrWhiteSpace(colExpr))
        {
            return list;
        }

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

        // Pass over the rows once: collect distinct row/column values (first-seen order) and the
        // running SUM per intersection.
        var rowVals = new List<string>();
        var colVals = new List<string>();
        var rowSeen = new HashSet<string>(StringComparer.Ordinal);
        var colSeen = new HashSet<string>(StringComparer.Ordinal);
        var sums = new Dictionary<(string Row, string Col), double>();
        foreach (var row in rows)
        {
            var rc = new RowScopedContext(baseCtx, row);
            string rv = Resolve(ev, templates, rowExpr, rc);
            string cv = Resolve(ev, templates, colExpr, rc);
            if (rowSeen.Add(rv)) rowVals.Add(rv);
            if (colSeen.Add(cv)) colVals.Add(cv);
            double v = EvalDouble(ev, valueExpr, rc);
            sums.TryGetValue((rv, cv), out var cur);
            sums[(rv, cv)] = cur + v;
        }
        if (rowVals.Count == 0 || colVals.Count == 0)
        {
            return list;
        }

        double x0 = bounds.X.ToMm(), y0 = bounds.Y.ToMm(), w = bounds.Width.ToMm();
        int nCols = colVals.Count + 1; // +1 for the row-header column
        double colW = w / nCols;

        // Header row: corner label + the distinct column values.
        list.Add(Fill(x0, y0, w, RowHeightMm, HeaderBg, tablix.Id));
        list.Add(CellText(cornerText, x0, y0, colW, bold: true, HeaderText, tablix.Id));
        for (int j = 0; j < colVals.Count; j++)
        {
            list.Add(CellText(colVals[j], x0 + (j + 1) * colW, y0, colW, bold: true, HeaderText, tablix.Id));
        }

        // Data rows: row header (bold) + the intersection sums.
        double y = y0 + RowHeightMm;
        foreach (var rv in rowVals)
        {
            list.Add(CellText(rv, x0, y, colW, bold: true, HeaderText, tablix.Id));
            for (int j = 0; j < colVals.Count; j++)
            {
                sums.TryGetValue((rv, colVals[j]), out var s);
                list.Add(CellText(FormatNumber(s, format, baseCtx.Culture), x0 + (j + 1) * colW, y, colW, bold: false, BodyText, tablix.Id));
            }
            y += RowHeightMm;
        }

        double totalH = y - y0;
        var pen = new PenStyle(GridColor, Unit.FromPoint(0.5));
        int gridRows = rowVals.Count + 1;
        for (int r = 0; r <= gridRows; r++)
        {
            double ly = y0 + r * RowHeightMm;
            list.Add(Line(x0, ly, x0 + w, ly, pen, tablix.Id));
        }
        for (int c = 0; c <= nCols; c++)
        {
            double lx = x0 + c * colW;
            list.Add(Line(lx, y0, lx, y0 + totalH, pen, tablix.Id));
        }

        actualHeight = Unit.FromMm(totalH);
        return list;
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
            TextBoxElement tb => Resolve(ev, templates, tb.Expression, ctx),
            _ => string.Empty,
        };

    private static string Resolve(ExpressionEvaluator ev, TemplateRenderer templates, string expr, IReportExpressionContext ctx)
    {
        if (string.IsNullOrEmpty(expr))
        {
            return string.Empty;
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

    private static Rectangle Rect(double xMm, double yMm, double wMm, double hMm)
        => new(Unit.FromMm(xMm), Unit.FromMm(yMm), Unit.FromMm(Math.Max(0, wMm)), Unit.FromMm(Math.Max(0, hMm)));
}
