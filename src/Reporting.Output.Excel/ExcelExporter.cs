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
/// <see cref="LayoutPrimitiveGrid"/>. Original scalar values are preserved. Formulas require explicit metadata and opt-in; labels never determine numeric values.</para>
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
        ReportDroppedContent(grid);
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

    /// <summary>Reports what the text grid could not carry. A workbook is a data surface — charts, images,
    /// barcodes, gauges and maps have no cell to live in — so the loss is by design; what this removes is the
    /// SILENCE. Does nothing unless the caller wired <see cref="ExcelExportOptions.OnWarning"/>.</summary>
    private void ReportDroppedContent(LayoutPrimitiveGrid grid)
    {
        if (_options.OnWarning is not { } onWarning || grid.DroppedPrimitives.Count == 0)
        {
            return;
        }
        foreach (var (primitive, count) in grid.DroppedPrimitives.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            onWarning(new ExportWarning(
                ExportWarning.PrimitiveNotRepresentable,
                $"{count} × {FriendlyName(primitive)} não cabe numa planilha (o .xlsx é orientado a dados, só texto vira célula) — exporte em PDF/PNG/SVG para manter o visual.",
                count));
        }
    }

    /// <summary>Primitive type name → the word the report author actually used.</summary>
    private static string FriendlyName(string primitiveTypeName) => primitiveTypeName switch
    {
        "DrawImagePrimitive" => "imagem (foto, logo, gráfico rasterizado, barcode ou QR)",
        "DrawLinePrimitive" => "linha/régua",
        "DrawRectanglePrimitive" => "retângulo/preenchimento",
        "DrawEllipsePrimitive" => "elipse",
        "DrawPolygonPrimitive" => "polígono (mapa, medidor)",
        _ => primitiveTypeName,
    };

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
                gridRow.Sources.TryGetValue(kv.Key, out var source);
                var value = source?.SemanticValue ?? kv.Value;
                if (_options.EmitFormulas && source?.SpreadsheetFormula is { Length: > 0 } formula)
                {
                    cell.FormulaA1 = formula;
                }
                else if (value is decimal number)
                {
                    // XLSX numeric cells have binary/15-digit precision. Preserve larger decimals as text.
                    if (decimal.TryParse(((double)number).ToString("G15", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rounded) && rounded == number) cell.Value = number;
                    else cell.Value = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (value is int integer)
                {
                    cell.Value = integer;
                }
                else if (value is long large && large is >= -999999999999999 and <= 999999999999999)
                {
                    cell.Value = large;
                }
                else if (value is bool boolean)
                {
                    cell.Value = boolean;
                }
                else if (value is DateTime date)
                {
                    cell.Value = date;
                }
                else
                {
                    cell.Value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                }

                if (value is decimal && LayoutPrimitiveGrid.LooksLikeCurrency(kv.Value)) cell.Style.NumberFormat.Format = "\"R$\" #,##0.00";
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
