using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Styling;
using UglyToad.PdfPig;
using Xunit;

namespace Reporting.Output.Pdf.Tests;

public sealed class ContinuousPdfCanvasTests
{
    [Fact]
    public void EndPage_Thermal80Conteudo110mm_PreservaTamanhoFisicoETextoVetorial()
    {
        using var stream = new MemoryStream();
        using (var context = new SkiaPdfRenderingContext(stream, leaveOpen: true))
        {
            context.BeginPage(new PageSetup(PaperSize.Thermal80, Margins: Thickness.Uniform(5.Mm())));
            context.DrawText("Thermal receipt", new Rectangle(5.Mm(), 90.Mm(), 60.Mm(), 8.Mm()), TextStyle.Default);
            context.DrawRectangle(new Rectangle(10.Mm(), 100.Mm(), 20.Mm(), 10.Mm()), null, BrushStyle.Black);
            context.EndPage();
        }
        using var pdf = PdfDocument.Open(stream.ToArray());
        var page = pdf.GetPage(1);
        Assert.InRange(page.Width, PaperSize.Thermal80.Width.ToPoints() - 0.51, PaperSize.Thermal80.Width.ToPoints() + 0.51);
        Assert.InRange(page.Height, 115.Mm().ToPoints(), 115.Mm().ToPoints() + 1.51);
        Assert.Contains("Thermal receipt", page.Text);
        Assert.NotEmpty(page.Paths);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    public void ToPdfBytes_PaginaContinuaRasterizada_ConvertePixelsEmPontos(float dpi)
    {
        using var context = new SkiaRenderingContext(dpi);
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawRectangle(new Rectangle(10.Mm(), 100.Mm(), 20.Mm(), 10.Mm()), null, BrushStyle.Black);
        context.EndPage();
        using var pdf = PdfDocument.Open(context.ToPdfBytes());
        var page = pdf.GetPage(1);
        Assert.InRange(page.Width, context.Pages[0].WidthPx * 72d / dpi - 0.51, context.Pages[0].WidthPx * 72d / dpi + 0.51);
        Assert.InRange(page.Height, context.Pages[0].HeightPx * 72d / dpi - 0.51, context.Pages[0].HeightPx * 72d / dpi + 0.51);
    }

    [Fact]
    public void EndPage_RecorteEPaginaVazia_ReiniciaAlturaEntrePaginas()
    {
        using var stream = new MemoryStream();
        using (var context = new SkiaPdfRenderingContext(stream, leaveOpen: true))
        {
            context.BeginPage(new PageSetup(PaperSize.Thermal80));
            context.PushClip(new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 50.Mm()), 3.Mm());
            context.DrawRectangle(new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 300.Mm()), null, BrushStyle.Black);
            context.BeginPage(new PageSetup(PaperSize.Thermal80, Margins: Thickness.Uniform(5.Mm())));
            context.Close();
            context.Close();
        }
        using var pdf = PdfDocument.Open(stream.ToArray());
        Assert.Equal(2, pdf.NumberOfPages);
        Assert.InRange(pdf.GetPage(1).Height, 50.Mm().ToPoints() - 0.51, 50.Mm().ToPoints() + 0.51);
        Assert.InRange(pdf.GetPage(2).Height, 2 * 5.Mm().ToPoints() + 0.49, 2 * 5.Mm().ToPoints() + 1.51);
    }

    [Fact]
    public void DrawRectangle_AlturaExcessiva_RejeitaSemGerarPaginaGigante()
    {
        using var stream = new MemoryStream();
        using (var context = new SkiaPdfRenderingContext(stream, null, true, new ContinuousPageOptions { MaxHeight = 100.Mm() }))
        {
            context.BeginPage(new PageSetup(PaperSize.Thermal80));
            Assert.Contains("MaxHeight", Assert.Throws<InvalidOperationException>(() =>
                context.DrawRectangle(new Rectangle(0.Mm(), 101.Mm(), 10.Mm(), 10.Mm()), null, BrushStyle.Black)).Message);
            context.DrawLine(new Point(5.Mm(), 20.Mm()), new Point(20.Mm(), 20.Mm()), new PenStyle(Color.Black, 4.Mm()));
        }
        using var pdf = PdfDocument.Open(stream.ToArray());
        Assert.InRange(pdf.GetPage(1).Height, 22.Mm().ToPoints(), 22.Mm().ToPoints() + 1.51);
    }
    [Fact]
    public void PopClip_LimiteExcedido_RestauraEstadoEPermiteFinalizarPdf()
    {
        using var stream = new MemoryStream();
        using (var context = new SkiaPdfRenderingContext(stream, metadata: null, leaveOpen: true, continuousOptions: new() { MaxOperations = 1 }))
        {
            context.BeginPage(new PageSetup(PaperSize.Thermal80));
            context.PushClip(new Rectangle(0.Mm(), 0.Mm(), 20.Mm(), 20.Mm()), Unit.Zero);
            Assert.Contains("MaxOperations", Assert.Throws<InvalidOperationException>(context.PopClip).Message);
            context.EndPage();
        }
        using var pdf = PdfDocument.Open(stream.ToArray());
        Assert.Single(pdf.GetPages());
    }
}
