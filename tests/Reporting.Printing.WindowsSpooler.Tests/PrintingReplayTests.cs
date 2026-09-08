using FluentAssertions;
using Reporting.Common;
using Reporting.Layout;
using Reporting.Rendering;
using Reporting.Rendering.Gdi;
using Reporting.Rendering.Skia;
using Reporting.Tests;
using SkiaSharp;
using UglyToad.PdfPig;
using Xunit;

namespace Reporting.Printing.WindowsSpooler.Tests;

[Collection("Windows print spooler")]
public sealed class PrintingReplayTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void PlayPage_PrimitivasConhecidas_ProduzPixelsNoGdi(int kind)
    {
        using var bitmap = new System.Drawing.Bitmap(203, 203);
        bitmap.SetResolution(203, 203);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.White);
        using var context = new GdiRenderingContext(graphics);
        RenderedReportPlayer.PlayPage(PrintingReplayFixture.Page(PrintingReplayFixture.Primitive(kind)), context);
        graphics.Flush();
        var ink = 0;
        for (int y = 0; y < 203; y++)
        {
            for (int x = 0; x < 203; x++)
            {
                if (bitmap.GetPixel(x, y).R < 128) ink++;
            }
        }
        ink.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayPage_ClipRetangularOuArredondado_PreservaRecorteEPrimitivaSeguinteNoGdi(bool rounded)
    {
        using var bitmap = new System.Drawing.Bitmap(203, 203);
        bitmap.SetResolution(203, 203);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.White);
        using var context = new GdiRenderingContext(graphics);
        RenderedReportPlayer.PlayPage(PrintingReplayFixture.ClippedPage(rounded), context);
        graphics.Flush();
        bitmap.GetPixel(70, 70).R.Should().Be(0);
        bitmap.GetPixel(150, 70).R.Should().Be(255);
        bitmap.GetPixel(175, 175).R.Should().Be(0);
        bitmap.GetPixel(23, 23).R.Should().Be(rounded ? (byte)255 : (byte)0);
    }

    [Fact]
    public void PlayPage_FalhaComClip_RestauraCanvasEmprestadoEPermiteNovaPagina()
    {
        using var bitmap = new SKBitmap(220, 220);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Translate(5, 5);
        canvas.ClipRect(new SKRect(0, 0, 205, 205));
        var matrix = canvas.TotalMatrix;
        var clip = canvas.DeviceClipBounds;
        var saveCount = canvas.SaveCount;
        using (var context = new SkiaCanvasRenderingContext(canvas, 203))
        {
            var unsupported = new UnsupportedPrintPrimitive
            {
                Bounds = PrintingReplayFixture.Bounds,
                ClipBounds = new Reporting.Geometry.Rectangle(new(100), new(100), new(100), new(100)),
            };
            Action act = () => RenderedReportPlayer.PlayPage(PrintingReplayFixture.Page(unsupported), context);
            act.Should().Throw<NotSupportedException>();
            canvas.SaveCount.Should().Be(saveCount);
            canvas.TotalMatrix.Should().Be(matrix);
            canvas.DeviceClipBounds.Should().Be(clip);
            RenderedReportPlayer.PlayPage(PrintingReplayFixture.Page(PrintingReplayFixture.Primitive(5)), context);
        }
        canvas.SaveCount.Should().Be(saveCount);
        canvas.TotalMatrix.Should().Be(matrix);
        bitmap.GetPixel(100, 100).Red.Should().Be(0);
        canvas.Clear(SKColors.White); // borrowed canvas remains usable after context disposal.
    }

    [Fact]
    public void PlayPage_FalhaComClip_RestauraGraphicsEmprestado()
    {
        using var bitmap = new System.Drawing.Bitmap(203, 203);
        bitmap.SetResolution(203, 203);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.White);
        using var context = new GdiRenderingContext(graphics);
        var unsupported = new UnsupportedPrintPrimitive
        {
            Bounds = PrintingReplayFixture.Bounds,
            ClipBounds = new Reporting.Geometry.Rectangle(new(100), new(100), new(100), new(100)),
        };
        var clip = graphics.ClipBounds;
        Action act = () => RenderedReportPlayer.PlayPage(PrintingReplayFixture.Page(unsupported), context);
        act.Should().Throw<NotSupportedException>();
        graphics.ClipBounds.Should().Be(clip);
        RenderedReportPlayer.PlayPage(PrintingReplayFixture.Page(PrintingReplayFixture.Primitive(5)), context);
        bitmap.GetPixel(100, 100).R.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrintAsync_PoligonoComClip_ProduzCaminhosVetoriaisNoPdfVirtual(bool rounded)
    {
        const string printerName = "Microsoft Print to PDF";
        var printer = new WindowsSpoolerPrinter();
        (await printer.ListPrintersAsync()).Should().Contain(p => p.Name == printerName);
        var output = Path.Combine(Path.GetTempPath(), $"omni-f15-{Guid.NewGuid():N}.pdf");
        try
        {
            var report = new RenderedReport("F15", EquatableArray.Create(PrintingReplayFixture.ClippedPage(rounded)));
            var result = await printer.PrintAsync(report, new PrintOptions(printerName) { OutputFile = output });
            result.Succeeded.Should().BeTrue(result.ErrorMessage);
            result.PagesPrinted.Should().Be(1);
            using var pdf = await PdfPrintOutput.OpenCompletedAsync(output);
            pdf.NumberOfPages.Should().Be(1);
            pdf.GetPage(1).Paths.Where(p => p.IsFilled).Sum(p => p.Count).Should().BeGreaterThanOrEqualTo(2,
                "o driver pode combinar as duas figuras como subpaths de um único caminho vetorial");
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }
}
