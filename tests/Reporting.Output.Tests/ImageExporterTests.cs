using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Image;
using Reporting.Output.Pdf;
using Xunit;

namespace Reporting.Output.Tests;

/// <summary>
/// The PNG exporter rasterises pages with the shared SkiaPrimitiveRenderer. Verifies a valid PNG
/// (signature bytes) for the stacked single-stream output and one PNG per page via RenderPages.
/// </summary>
public class ImageExporterTests
{
    private static Report BuildReport() =>
        ReportBuilder.Create("Img")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", new[] { new Linha("Ana", "1200"), new Linha("Bia", "50") })
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(60, 6))
            .Build();

    private static void ShouldBePng(byte[] bytes)
    {
        bytes.Length.Should().BeGreaterThan(8);
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50); // 'P'
        bytes[2].Should().Be(0x4E); // 'N'
        bytes[3].Should().Be(0x47); // 'G'
    }

    [Fact]
    public async Task Exports_a_valid_stacked_png()
    {
        var rendered = await BuildReport().PaginateAsync();
        var bytes = new PngImageExporter().ExportToBytes(rendered);
        ShouldBePng(bytes);
    }

    [Fact]
    public async Task RenderPages_returns_one_valid_png_per_page()
    {
        var rendered = await BuildReport().PaginateAsync();
        var pages = new PngImageExporter().RenderPages(rendered);

        pages.Should().HaveCount(rendered.Pages.Count);
        pages.Should().NotBeEmpty();
        foreach (var p in pages)
        {
            ShouldBePng(p);
        }
    }

    [Fact]
    public void Declares_png_format_metadata()
    {
        var ex = new PngImageExporter();
        ex.Format.Should().Be("png");
        ex.FileExtension.Should().Be(".png");
        ex.ContentType.Should().Be("image/png");
    }
}
