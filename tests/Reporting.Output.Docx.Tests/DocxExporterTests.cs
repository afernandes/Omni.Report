using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Reporting.Output.Docx;
using Reporting.Output.Pdf;
using Reporting.Samples.CodeFirst.Reports;
using Xunit;

namespace Reporting.Output.Docx.Tests;

public class DocxExporterTests
{
    private static IEnumerable<string> CellTexts(WordprocessingDocument doc) =>
        doc.MainDocumentPart!.Document.Body!.Descendants<TableCell>()
            .Select(c => c.InnerText);

    [Fact]
    public async Task Exports_sample01_vendas_into_a_word_table()
    {
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new DocxExporter().ExportToBytes(rendered);

        bytes.Should().NotBeEmpty();

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        // Exactly one table carries the grid.
        doc.MainDocumentPart!.Document.Body!.Descendants<Table>().Should().ContainSingle();

        var texts = CellTexts(doc).ToList();
        texts.Should().Contain(s => s.Contains("Produto"), "the column header is a table cell");
        texts.Should().Contain(s => s.Contains("Ana Beatriz"), "a detail value is a table cell");
    }

    [Fact]
    public async Task Generated_docx_is_schema_valid()
    {
        // The exporter's whole point is a file Word opens WITHOUT a repair prompt — assert real OOXML validity
        // (the InnerText/Bold assertions above pass even on a malformed-but-readable document).
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(rendered));
        using var doc = WordprocessingDocument.Open(ms, false);

        var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).ToList();
        errors.Should().BeEmpty(string.Join("\n", errors.Select(e => $"{e.Path?.XPath}: {e.Description}")));
    }

    [Fact]
    public async Task Header_row_cells_are_bold()
    {
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(rendered));
        using var doc = WordprocessingDocument.Open(ms, false);

        var firstRow = doc.MainDocumentPart!.Document.Body!.Descendants<Table>().First()
            .Elements<TableRow>().First();
        var headerCells = firstRow.Descendants<TableCell>()
            .Where(c => !string.IsNullOrWhiteSpace(c.InnerText))
            .ToList();
        headerCells.Should().NotBeEmpty("the header row has labelled columns");
        // Every non-empty header cell is bold.
        headerCells.Should().OnlyContain(c => c.Descendants<Bold>().Any(), "header cells are bold");
    }

    [Fact]
    public async Task Package_properties_are_set()
    {
        var rendered = await Sample02_EspelhoProdutos.Build().PaginateAsync();
        var bytes = new DocxExporter(new DocxExportOptions
        {
            Author = "Ana",
            Title = "Catálogo",
        }).ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.PackageProperties.Title.Should().Be("Catálogo");
        doc.PackageProperties.Creator.Should().Be("Ana");
    }

    [Fact]
    public void Exporter_advertises_the_docx_format()
    {
        var x = new DocxExporter();
        x.Format.Should().Be("docx");
        x.FileExtension.Should().Be(".docx");
        x.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }
}
