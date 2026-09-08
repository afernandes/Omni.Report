using Reporting.Rendering.Skia;
using Reporting.Tests;
using SkiaSharp;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Rendering.Tests;

public sealed class SkiaContinuousCanvasTests : ContinuousCanvasTests
{
    [Fact]
    public void DrawText_TextoCresceAlemDaCaixa_ReservaAlturaDosGlifosEDecoracao()
    {
        using var context = new SkiaRenderingContext();
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawText("linha linha linha linha linha linha", new Rectangle(5.Mm(), 100.Mm(), 12.Mm(), 1.Mm()),
            new TextStyle(new Font("Arial", 16, FontStyle.Underline), Color.Black));
        context.EndPage();
        Assert.True(context.Pages[0].HeightPx > 120.Mm().ToPixels());
    }

    protected override IRenderingContext Create(float dpi, ContinuousPageOptions options) => new SkiaRenderingContext(options, dpi);
    protected override (int Width, int Height) Size(IRenderingContext context, int index = 0)
    {
        var page = ((SkiaRenderingContext)context).Pages[index];
        return (page.WidthPx, page.HeightPx);
    }
    protected override bool IsBlack(IRenderingContext context, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(((SkiaRenderingContext)context).GetPagePng(0));
        return bitmap.GetPixel(x, y).Red < 100;
    }
}
