using FluentAssertions;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;
using Reporting.Paper;
using Reporting.Rendering;
using UglyToad.PdfPig;
using Xunit;

namespace Reporting.Output.Pdf.Tests;

public sealed class ConcurrentTextExportTests
{
    [Fact]
    public async Task ExportAsync_MesmoRelatorio_PreservaTextoPosicoesFontesETamanhoDasPaginas()
    {
        var report = new RenderedReport("Equivalência", new EquatableArray<RenderedPage>(
            Enumerable.Range(1, 10).Select(page => new RenderedPage(page, PageSetup.A4Portrait,
                new EquatableArray<LayoutPrimitive>(Enumerable.Range(0, 40).Select(row => new DrawTextPrimitive
                {
                    Text = $"Item {(page - 1) * 40 + row}: ação café € Ω",
                    Bounds = new Rectangle(15.Mm(), (15 + row * 6).Mm(), 180.Mm(), 6.Mm()),
                    Style = TextStyle.Default
                }))))));
        var exporter = new SkiaPdfExporter(new PdfExportOptions
        {
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        using var sync = PdfDocument.Open(exporter.ExportToBytes(report));
        using var asynchronous = PdfDocument.Open(await exporter.ExportToBytesAsync(report));
        sync.NumberOfPages.Should().Be(report.PageCount);
        asynchronous.NumberOfPages.Should().Be(report.PageCount);
        for (int number = 1; number <= report.PageCount; number++)
        {
            var left = sync.GetPage(number);
            var right = asynchronous.GetPage(number);
            string expected = string.Concat(report.Pages[number - 1].Primitives.OfType<DrawTextPrimitive>().Select(p => p.Text));
            left.Text.Should().Be(expected);
            right.Text.Should().Be(expected);
            right.Width.Should().Be(left.Width);
            right.Height.Should().Be(left.Height);
            right.Letters.Select(l => new { l.Value, l.BoundingBox, l.FontName, l.FontSize })
                .Should().Equal(left.Letters.Select(l => new { l.Value, l.BoundingBox, l.FontName, l.FontSize }));
        }
    }

    [Fact]
    public async Task Export_ExecucoesConcorrentes_PreservaTextoUnicodeSelecionavel()
    {
        var exporter = new SkiaPdfExporter(new PdfExportOptions
        {
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await Task.WhenAll(Enumerable.Range(0, 8).Select(session => Task.Run(async () =>
        {
            for (int iteration = 0; iteration < 10; iteration++)
            {
                string text = $"Sessão {session}, execução {iteration}: ação café € Ω 0123456789";
                var page = new RenderedPage(1, PageSetup.A4Portrait,
                    EquatableArray.Create<LayoutPrimitive>(new DrawTextPrimitive
                    {
                        Text = text,
                        Bounds = new Rectangle(10.Mm(), 10.Mm(), 180.Mm(), 10.Mm()),
                        Style = TextStyle.Default
                    }));
                var report = new RenderedReport("Unicode", EquatableArray.Create(page));
                var bytes = iteration % 2 == 0
                    ? exporter.ExportToBytes(report)
                    : await exporter.ExportToBytesAsync(report);
                using var document = PdfDocument.Open(bytes);
                document.NumberOfPages.Should().Be(1);
                document.GetPage(1).Text.Should().Be(text);
            }
        }))).WaitAsync(TimeSpan.FromSeconds(30));
    }
}
