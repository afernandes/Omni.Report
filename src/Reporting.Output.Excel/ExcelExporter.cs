using ClosedXML.Excel;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Layout.Tabular;
using Reporting.Output.Pdf;

namespace Reporting.Output.Excel;

/// <summary>
/// Exports a <see cref="RenderedReport"/> to a <c>.xlsx</c> workbook using ClosedXML.
/// </summary>
/// <remarks>
/// <para>Strategy: walks every <see cref="DrawTextPrimitive"/> and lays it out on a grid by
/// quantizing the X / Y coordinates of each primitive into column and row indices via
/// <see cref="LayoutPrimitiveGrid"/>. After the grid is built, numeric columns are detected
/// (every non-header cell parses as a number or currency) and any row marked as a "subtotal"
/// or "total" — by the textual content of one of its cells — gets its numeric cells
/// rewritten as live <c>=SUM(range)</c> formulas referencing the detail rows above.</para>
/// </remarks>
public sealed class ExcelExporter : IReportExporter
{
    private readonly ExcelExportOptions _options;

    public ExcelExporter(ExcelExportOptions? options = null)
    {
        _options = options ?? ExcelExportOptions.Default;
    }

    public string Format => "xlsx";
    public string FileExtension => ".xlsx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        using var wb = new XLWorkbook();
        wb.Properties.Title = _options.Title ?? report.Name;
        if (_options.Author is not null)
        {
            wb.Properties.Author = _options.Author;
        }

        var sheetName = (_options.SheetName ?? report.Name);
        if (sheetName.Length > 31)
        {
            sheetName = sheetName[..31];
        }
        var ws = wb.AddWorksheet(sheetName);

        var grid = LayoutPrimitiveGrid.Build(report);
        WriteGrid(ws, grid);

        if (_options.FreezeHeader && grid.Rows.Count > 0)
        {
            ws.SheetView.FreezeRows(1);
        }
        if (_options.AlternateRowColors && grid.Rows.Count > 1)
        {
            ApplyZebraStripes(ws, grid);
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(output);
    }

    // ── Worksheet writer ────────────────────────────────────────────────────────

    private void WriteGrid(IXLWorksheet ws, LayoutPrimitiveGrid grid)
    {
        for (int r = 0; r < grid.Rows.Count; r++)
        {
            var gridRow = grid.Rows[r];
            int xlRow = r + 1;
            foreach (var kv in gridRow.Cells)
            {
                int xlCol = kv.Key + 1;
                var cell = ws.Cell(xlRow, xlCol);
                var parsed = LayoutPrimitiveGrid.TryParseDecimal(kv.Value);
                if (parsed is not null && gridRow.Kind == RowKind.Detail)
                {
                    cell.Value = parsed.Value;
                    cell.Style.NumberFormat.Format = LayoutPrimitiveGrid.LooksLikeCurrency(kv.Value) ? "R$ #,##0.00" : "#,##0.00";
                }
                else
                {
                    cell.Value = kv.Value;
                }
                if (gridRow.Kind == RowKind.Header)
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                else if (gridRow.Kind == RowKind.GroupHeader)
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.FromHtml("#C2410C");
                }
                else if (gridRow.Kind is RowKind.Subtotal or RowKind.Total)
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                }
            }
        }

        if (_options.EmitFormulas)
        {
            ApplySumFormulas(ws, grid);
        }
    }

    /// <summary>For every Subtotal/Total row:
    /// (a) any cell that already holds a parsed number is replaced by <c>=SUM(detailRange)</c>;
    /// (b) for every numeric column above the row that does NOT yet have a cell in this row,
    /// a fresh formula cell is INSERTED — handles "Subtotal: R$ X · N rows" wide-text footers
    /// that omit a separate numeric cell.</summary>
    private static void ApplySumFormulas(IXLWorksheet ws, LayoutPrimitiveGrid grid)
    {
        for (int r = 0; r < grid.Rows.Count; r++)
        {
            var row = grid.Rows[r];
            if (row.Kind is not (RowKind.Subtotal or RowKind.Total))
            {
                continue;
            }
            int start = r - 1;
            while (start >= 0 && grid.Rows[start].Kind == RowKind.Detail)
            {
                start--;
            }
            int detailStart = start + 1;
            int detailEnd = r - 1;
            if (detailEnd < detailStart)
            {
                continue;
            }
            int xlRow = r + 1;

            // Find every column that is numeric in the detail range.
            for (int colIdx = 0; colIdx < grid.ColumnXs.Count; colIdx++)
            {
                bool columnIsNumeric = false;
                bool currencyHint = false;
                for (int rr = detailStart; rr <= detailEnd; rr++)
                {
                    if (grid.Rows[rr].Cells.TryGetValue(colIdx, out var v) && LayoutPrimitiveGrid.TryParseDecimal(v) is not null)
                    {
                        columnIsNumeric = true;
                        if (LayoutPrimitiveGrid.LooksLikeCurrency(v))
                        {
                            currencyHint = true;
                        }
                    }
                }
                if (!columnIsNumeric)
                {
                    continue;
                }
                int xlCol = colIdx + 1;
                var colLetter = XLHelper.GetColumnLetterFromNumber(xlCol);
                var formula = $"=SUM({colLetter}{detailStart + 1}:{colLetter}{detailEnd + 1})";
                var cell = ws.Cell(xlRow, xlCol);
                cell.FormulaA1 = formula;
                cell.Style.NumberFormat.Format = currencyHint ? "R$ #,##0.00" : "#,##0.00";
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
        }
    }

    private static void ApplyZebraStripes(IXLWorksheet ws, LayoutPrimitiveGrid grid)
    {
        bool alt = false;
        for (int r = 0; r < grid.Rows.Count; r++)
        {
            if (grid.Rows[r].Kind != RowKind.Detail)
            {
                alt = false;
                continue;
            }
            if (alt)
            {
                int xlRow = r + 1;
                int lastCol = grid.ColumnXs.Count;
                ws.Range(xlRow, 1, xlRow, lastCol).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F7F1");
            }
            alt = !alt;
        }
    }
}
