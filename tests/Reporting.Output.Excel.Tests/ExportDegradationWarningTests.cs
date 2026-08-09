using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Excel;
using Reporting.Output.Pdf;
using Reporting.Elements;
using Reporting.Styling;
using Xunit;

namespace Reporting.Output.Excel.Tests;

/// <summary>
/// A workbook is a data surface: charts, images, barcodes, gauges and maps have no cell to live in, so the
/// Excel exporter drops them. That is legitimate — being <b>silent</b> about it is not, and it is how a user
/// finds out at the worst possible moment that their logo never made it into the spreadsheet.
/// </summary>
public class ExportDegradationWarningTests
{
    private sealed record Linha(string Nome, decimal Valor);

    /// <summary>A report whose visual content cannot survive a text grid: a chart plus a filled rectangle.</summary>
    private static Report WithNonTextContent() =>
        ReportBuilder.Create("Misto")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", new[] { new Linha("Um", 10m), new Linha("Dois", 20m) })
            .ReportHeader(h => h.Height(60)
                .Text("Relatório com gráfico").At(0, 0).Size(120, 8)
                .Rectangle().At(0, 10).Size(60, 20).Fill(Color.FromHex("#DDEEFF"))
                .Chart(ChartKind.Bar, "Valores").At(0, 34).Size(120, 24)
                    .Series("Valor", "Fields.Nome", "Fields.Valor"))
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 6))
            .Build();

    private static Report PureText() =>
        ReportBuilder.Create("SoTexto")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", new[] { new Linha("Um", 10m) })
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 6))
            .Build();

    [Fact]
    public async Task Dropped_visual_content_raises_a_warning()
    {
        var rendered = await WithNonTextContent().PaginateAsync();
        var warnings = new List<ExportWarning>();
        var exporter = new ExcelExporter(new ExcelExportOptions { OnWarning = warnings.Add });

        exporter.ExportToBytes(rendered);

        warnings.Should().NotBeEmpty("the chart and the rectangle cannot be represented in a worksheet");
        warnings.Should().OnlyContain(w => w.Code == ExportWarning.PrimitiveNotRepresentable);
        warnings.Sum(w => w.Count).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task The_warning_says_what_was_lost_and_how_many()
    {
        var rendered = await WithNonTextContent().PaginateAsync();
        var warnings = new List<ExportWarning>();

        new ExcelExporter(new ExcelExportOptions { OnWarning = warnings.Add }).ExportToBytes(rendered);

        // The message has to be actionable: what was dropped, how many, and what to do instead.
        warnings.Should().Contain(w => w.Message.Contains("planilha", StringComparison.Ordinal));
        warnings.Should().Contain(w => w.Message.Contains("PDF", StringComparison.Ordinal));
        warnings.Should().OnlyContain(w => w.Count >= 1);
    }

    [Fact]
    public async Task A_pure_text_report_raises_no_warning()
    {
        var rendered = await PureText().PaginateAsync();
        var warnings = new List<ExportWarning>();

        new ExcelExporter(new ExcelExportOptions { OnWarning = warnings.Add }).ExportToBytes(rendered);

        warnings.Should().BeEmpty("nothing was lost, so nothing should be reported");
    }

    [Fact]
    public async Task Without_a_handler_the_export_still_succeeds_silently()
    {
        // Backwards compatible: OnWarning is opt-in, so existing callers see no behaviour change.
        var rendered = await WithNonTextContent().PaginateAsync();

        var bytes = new ExcelExporter().ExportToBytes(rendered);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task The_handler_does_not_change_the_produced_workbook()
    {
        // Diagnostics must observe, never alter the output.
        var rendered = await WithNonTextContent().PaginateAsync();

        var quiet = new ExcelExporter(new ExcelExportOptions()).ExportToBytes(rendered);
        var loud = new ExcelExporter(new ExcelExportOptions { OnWarning = _ => { } }).ExportToBytes(rendered);

        loud.Length.Should().Be(quiet.Length, "warning is a side channel, not a content change");
    }
}
