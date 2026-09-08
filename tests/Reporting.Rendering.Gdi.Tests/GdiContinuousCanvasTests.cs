using System.Runtime.Versioning;
using Reporting.Tests;
using Reporting.Geometry;
using Reporting.Paper;
using Xunit;

namespace Reporting.Rendering.Gdi.Tests;

[SupportedOSPlatform("windows")]
public sealed class GdiContinuousCanvasTests : ContinuousCanvasTests
{
    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    public void DrawText_PapelContinuo_DimensoesDosGlifosCorrespondemAoDpi(float dpi)
    {
        using var continuous = new GdiRenderingContext(dpi);
        using var finite = new GdiRenderingContext(dpi);
        continuous.BeginPage(new PageSetup(PaperSize.Thermal80));
        finite.BeginPage(new PageSetup(new PaperSize("reference", PaperSize.Thermal80.Width, 130.Mm())));
        foreach (var context in new[] { continuous, finite })
        {
            context.DrawText("Thermal receipt", new Rectangle(5.Mm(), 100.Mm(), 65.Mm(), 10.Mm()), TextStyle.Default);
            context.EndPage();
        }
        var actual = InkBounds(continuous.Pages[0]);
        var expected = InkBounds(finite.Pages[0]);
        Assert.InRange(actual.Width, expected.Width - 2, expected.Width + 2);
        Assert.InRange(actual.Height, expected.Height - 2, expected.Height + 2);
        Assert.True(actual.Height > 5);
    }

    private static (int Width, int Height) InkBounds(System.Drawing.Bitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = 0, bottom = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).R >= 100) continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        return (right - left + 1, bottom - top + 1);
    }

    protected override IRenderingContext Create(float dpi, ContinuousPageOptions options) => new GdiRenderingContext(dpi, options);
    protected override (int Width, int Height) Size(IRenderingContext context, int index = 0)
    {
        var page = ((GdiRenderingContext)context).Pages[index];
        return (page.Width, page.Height);
    }
    protected override bool IsBlack(IRenderingContext context, int x, int y) => ((GdiRenderingContext)context).Pages[0].GetPixel(x, y).R < 100;
}
