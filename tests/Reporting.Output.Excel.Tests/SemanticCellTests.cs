using Reporting.Output.Pdf;
using ClosedXML.Excel;
using FluentAssertions;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering;
using Xunit;
namespace Reporting.Output.Excel.Tests;

public sealed class SemanticCellTests
{
    [Fact]
    public void Export_TextoNumericoEValorDecimal_PreservaTipoEPrecisao()
    {
        var rows = new[] { Cell("00123", 0), Cell("1.25", 10), Cell("1,25", 20, 1.25m), Cell("valor alto", 30, 12345678901234567890.12345678m), Cell("Total 7", 40, 7m) };
        using var workbook = Export(rows);
        var cells = workbook.Worksheet(1).CellsUsed().ToArray();
        cells[0].DataType.Should().Be(XLDataType.Text); cells[0].GetString().Should().Be("00123");
        cells[1].DataType.Should().Be(XLDataType.Text); cells[1].GetString().Should().Be("1.25");
        cells[2].GetDouble().Should().Be(1.25);
        cells[3].GetString().Should().Be("12345678901234567890.12345678");
        cells[4].GetDouble().Should().Be(7);
        cells.Should().OnlyContain(cell => !cell.HasFormula);
    }
    [Fact]
    public void Export_FormulaExplicita_ExigeOptIn()
    {
        var cell = Cell("3", 0, 3m) with { SpreadsheetFormula = "1+2" };
        using var disabled = Export([cell]);
        disabled.Worksheet(1).Cell(1, 1).HasFormula.Should().BeFalse();
        using var enabled = Export([cell], true);
        enabled.Worksheet(1).Cell(1, 1).FormulaA1.Should().Be("1+2");
    }
    private static DrawTextPrimitive Cell(string text, int y, object? value = null) => new()
    { Text = text, SemanticValue = value, Bounds = new Rectangle(0.Mm(), y.Mm(), 100.Mm(), 5.Mm()), Style = TextStyle.Default };
    private static XLWorkbook Export(DrawTextPrimitive[] cells, bool formulas = false)
    {
        var report = new RenderedReport("Valores", EquatableArray.Create(new RenderedPage(1, new PageSetup(PaperSize.A4), new EquatableArray<LayoutPrimitive>(cells))));
        var bytes = new ExcelExporter(new ExcelExportOptions { EmitFormulas = formulas }).ExportToBytes(report);
        return new XLWorkbook(new MemoryStream(bytes));
    }
}
