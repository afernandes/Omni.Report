using ClosedXML.Excel;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Excel;
using Reporting.Output.Pdf;
using Reporting.Samples.CodeFirst.Reports;
using Xunit;

namespace Reporting.Output.Excel.Tests;

public class ExcelExporterTests
{
    [Fact]
    public async Task Exports_sample01_vendas_into_workbook()
    {
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new ExcelExporter().ExportToBytes(rendered);

        bytes.Should().NotBeEmpty();

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        wb.Worksheets.Should().HaveCount(1);
        var ws = wb.Worksheet(1);

        // Sheet name = report name (truncated to 31).
        ws.Name.Should().Be("Vendas por Cliente");

        // "Produto" column header appears somewhere (in the row after the report title).
        var allCellText = ws.CellsUsed().Select(c => c.GetString()).ToList();
        allCellText.Should().Contain(s => s.Contains("Produto"));
        allCellText.Should().Contain(s => s.Contains("Ana Beatriz"));
    }

    [Fact]
    public async Task Sample03_caixa_emits_sum_formulas_for_subtotal_rows()
    {
        var rendered = await Sample03_RelatorioCaixa.Build().PaginateAsync();
        var bytes = new ExcelExporter().ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);

        // Walk every cell looking for SUM formulas; the caixa report has 4 group subtotals + total
        var formulaCells = ws.CellsUsed(c => c.HasFormula).Select(c => c.FormulaA1).ToList();
        formulaCells.Should().NotBeEmpty(because: "Subtotal/Total rows must be live SUM formulas");
        formulaCells.Should().Contain(f => f.Contains("SUM(", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sample19_wide_crosstab_tiles_columns_without_dropping_any_in_excel()
    {
        // Regression guard for horizontal column tiling (#209): a wide crosstab paginated by COLUMN must keep
        // every column when exported to Excel (the exporter walks the tiled pages' primitives into the grid).
        var rendered = await Sample19_CrosstabLargo.Build().PaginateAsync();
        rendered.Pages.Count.Should().BeGreaterThan(1, "the wide crosstab tiles its columns across pages");

        var bytes = new ExcelExporter().ExportToBytes(rendered);
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);

        var allText = wb.Worksheets.SelectMany(ws => ws.CellsUsed().Select(c => c.GetString())).ToList();
        for (int p = 1; p <= 18; p++)
        {
            allText.Should().Contain(s => s.Contains($"P{p:00}"), $"column header P{p:00} must survive the tiling into Excel");
        }
        allText.Should().Contain(s => s.Contains("Cliente 01"), "the row headers survive the tiling too");
    }

    [Fact]
    public async Task Workbook_properties_are_set()
    {
        var rendered = await Sample02_EspelhoProdutos.Build().PaginateAsync();
        var bytes = new ExcelExporter(new ExcelExportOptions
        {
            Author = "Ana",
            Title = "Catálogo",
            SheetName = "Produtos",
        }).ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        wb.Properties.Author.Should().Be("Ana");
        wb.Properties.Title.Should().Be("Catálogo");
        wb.Worksheet(1).Name.Should().Be("Produtos");
    }

    [Fact]
    public async Task Detail_numeric_cells_are_typed_as_numbers_not_strings()
    {
        var rendered = await Sample03_RelatorioCaixa.Build().PaginateAsync();
        var bytes = new ExcelExporter().ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        // At least one cell must be a true number (so Excel sums work natively).
        ws.CellsUsed(c => c.DataType == XLDataType.Number).Should().NotBeEmpty();
    }

    [Fact]
    public async Task First_row_is_frozen_for_easy_scrolling()
    {
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new ExcelExporter().ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        wb.Worksheet(1).SheetView.SplitRow.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Disable_formulas_keeps_subtotals_as_static_text()
    {
        var rendered = await Sample03_RelatorioCaixa.Build().PaginateAsync();
        var bytes = new ExcelExporter(new ExcelExportOptions { EmitFormulas = false }).ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        wb.Worksheet(1).CellsUsed(c => c.HasFormula).Should().BeEmpty();
    }

    [Fact]
    public void Exporter_metadata_set_correctly()
    {
        IReportExporter exporter = new ExcelExporter();
        exporter.Format.Should().Be("xlsx");
        exporter.FileExtension.Should().Be(".xlsx");
        exporter.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public async Task Long_report_name_is_truncated_to_31_chars()
    {
        // Build a sample with a deliberately long name to test sheet-name truncation.
        var report = ReportBuilder.Create("This name has more than thirty-one characters in it")
            .DataSource("X", new[] { new { A = 1 } })
            .Detail(d => d.Height(5).Text("{Fields.A}").At(0, 0).Size(20, 5))
            .Build();
        var rendered = await report.PaginateAsync();
        var bytes = new ExcelExporter().ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        wb.Worksheet(1).Name.Length.Should().BeLessThanOrEqualTo(31);
    }

    [Fact]
    public async Task Export_to_file_writes_xlsx_to_disk()
    {
        var rendered = await Sample02_EspelhoProdutos.Build().PaginateAsync();
        var temp = Path.ChangeExtension(Path.GetTempFileName(), ".xlsx");
        try
        {
            new ExcelExporter().ExportToFile(rendered, temp);
            new FileInfo(temp).Length.Should().BeGreaterThan(0);
            // Verify it can be re-opened.
            using var wb = new XLWorkbook(temp);
            wb.Worksheets.Should().HaveCount(1);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
