using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Pdf;
using Reporting.Samples.CodeFirst.Reports;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Xunit;

namespace Reporting.Output.Pdf.Tests;

public class SkiaPdfExporterTests
{
    [Fact]
    public async Task Exports_sample01_with_selectable_text()
    {
        var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var exporter = new SkiaPdfExporter(new PdfExportOptions
        {
            Title = "Vendas",
            Author = "OmniReport",
            Subject = "Sample 01",
            Keywords = "vendas;cliente;pt-BR",
            Creator = "OmniReport Tests",
        });
        var bytes = exporter.ExportToBytes(report);

        // PDF signature + non-empty
        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");

        using var pdf = PdfDocument.Open(bytes);
        pdf.NumberOfPages.Should().Be(report.Pages.Count);

        // Aggregate text across pages — must contain known literals from the sample.
        var allText = string.Join(" ", pdf.GetPages().Select(p => p.Text));
        allText.Should().Contain("Relatório de Vendas");
        allText.Should().Contain("Ana Beatriz");
        allText.Should().Contain("Total geral");

        pdf.Information.Title.Should().Be("Vendas");
        pdf.Information.Author.Should().Be("OmniReport");
        pdf.Information.Subject.Should().Be("Sample 01");
        pdf.Information.Keywords.Should().Be("vendas;cliente;pt-BR");
    }

    [Fact]
    public async Task Pdf_text_is_word_segmented_not_glyph_blob()
    {
        var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(report);
        using var pdf = PdfDocument.Open(bytes);
        var firstPage = pdf.GetPages().First();
        var words = firstPage.GetWords().ToList();
        words.Should().NotBeEmpty();
        words.Select(w => w.Text).Should().Contain(w => w.Contains("Vendas"));
    }

    [Fact]
    public async Task Sample02_pdf_round_trip()
    {
        var report = await Sample02_EspelhoProdutos.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(report);
        using var pdf = PdfDocument.Open(bytes);
        pdf.NumberOfPages.Should().Be(report.Pages.Count);
        var text = pdf.GetPages().First().Text;
        text.Should().Contain("Espelho de Produtos");
    }

    [Fact]
    public async Task Sample03_pdf_renders_multi_group_report()
    {
        var report = await Sample03_RelatorioCaixa.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(report);
        using var pdf = PdfDocument.Open(bytes);
        var text = string.Join(" ", pdf.GetPages().Select(p => p.Text));
        text.Should().Contain("Movimento de Caixa");
        text.Should().Contain("Dinheiro");
        text.Should().Contain("PIX");
    }

    [Fact]
    public async Task Default_metadata_uses_report_name_as_title()
    {
        var rendered = await Sample02_EspelhoProdutos.Build().PaginateAsync();
        var bytes = new SkiaPdfExporter().ExportToBytes(rendered);
        using var pdf = PdfDocument.Open(bytes);
        pdf.Information.Title.Should().Be("Espelho de Produtos");
    }

    [Fact]
    public void Export_metadata_on_interface()
    {
        IReportExporter exporter = new SkiaPdfExporter();
        exporter.Format.Should().Be("pdf");
        exporter.FileExtension.Should().Be(".pdf");
        exporter.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Export_to_file_writes_bytes_to_disk()
    {
        var rendered = await Sample02_EspelhoProdutos.Build().PaginateAsync();
        var temp = Path.GetTempFileName();
        try
        {
            new SkiaPdfExporter().ExportToFile(rendered, temp);
            new FileInfo(temp).Length.Should().BeGreaterThan(0);
            // Check signature on disk
            var firstBytes = File.ReadAllBytes(temp)[..5];
            System.Text.Encoding.ASCII.GetString(firstBytes).Should().Be("%PDF-");
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
