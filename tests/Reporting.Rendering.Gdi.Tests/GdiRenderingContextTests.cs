using System.Drawing.Imaging;
using System.Runtime.Versioning;
using FluentAssertions;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Gdi;
using Reporting.Styling;
using Xunit;
using GdiBitmap = System.Drawing.Bitmap;
using GdiGraphics = System.Drawing.Graphics;
using GdiColor = System.Drawing.Color;

namespace Reporting.Rendering.Gdi.Tests;

[SupportedOSPlatform("windows")]
public class GdiRenderingContextTests
{
    [Fact]
    public void Standalone_constructor_allocates_a_page_per_BeginPage()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.EndPage();

        ctx.Pages.Should().HaveCount(1);
        ctx.Pages[0].Width.Should().BeGreaterThan(100);
        ctx.Pages[0].Height.Should().BeGreaterThan(100);
    }

    [Fact]
    public void Draw_text_and_shapes_paints_non_white_pixels()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("Olá mundo",
            new Rectangle(10.Mm(), 10.Mm(), 100.Mm(), 8.Mm()),
            new TextStyle(new Font("Arial", 12), Color.Black));
        ctx.DrawLine(new Point(10.Mm(), 20.Mm()), new Point(100.Mm(), 20.Mm()), PenStyle.Default);
        ctx.DrawRectangle(new Rectangle(10.Mm(), 30.Mm(), 50.Mm(), 20.Mm()),
            PenStyle.Default, new BrushStyle(Color.LightGray));
        ctx.DrawEllipse(new Rectangle(10.Mm(), 60.Mm(), 30.Mm(), 30.Mm()), null, new BrushStyle(Color.Red));
        ctx.EndPage();

        var bitmap = ctx.Pages[0];
        HasNonWhitePixel(bitmap).Should().BeTrue();
    }

    [Fact]
    public void Get_page_png_returns_valid_png_signature()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("x", new Rectangle(5.Mm(), 5.Mm(), 50.Mm(), 5.Mm()), TextStyle.Default);
        ctx.EndPage();

        var png = ctx.GetPagePng(0);
        png.AsSpan(0, 4).ToArray().Should().Equal([0x89, 0x50, 0x4E, 0x47]);
    }

    [Fact]
    public void Bound_graphics_constructor_writes_to_caller_owned_surface()
    {
        using var bitmap = new GdiBitmap(400, 400);
        using var graphics = GdiGraphics.FromImage(bitmap);
        graphics.Clear(GdiColor.White);

        using (var ctx = new GdiRenderingContext(graphics))
        {
            ctx.DrawText("X", new Rectangle(5.Mm(), 5.Mm(), 50.Mm(), 8.Mm()), TextStyle.Default);
            ctx.DrawRectangle(new Rectangle(5.Mm(), 20.Mm(), 30.Mm(), 10.Mm()), PenStyle.Default, new BrushStyle(Color.Red));
        }
        HasNonWhitePixel(bitmap).Should().BeTrue();
        // Bound mode: dispose must NOT dispose the caller's Graphics.
        graphics.DpiX.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Measure_text_returns_non_zero_size()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        var size = ctx.MeasureText("Hello world", new TextStyle(new Font("Arial", 12), Color.Black));
        size.Width.Mils.Should().BeGreaterThan(0);
        size.Height.Mils.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Measure_text_works_without_active_page()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        var size = ctx.MeasureText("abc", TextStyle.Default);
        size.Width.Mils.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Empty_text_does_not_paint()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText(string.Empty, new Rectangle(0.Mm(), 0.Mm(), 10.Mm(), 10.Mm()), TextStyle.Default);
        ctx.EndPage();
        HasNonWhitePixel(ctx.Pages[0]).Should().BeFalse();
    }

    [Fact]
    public void Draw_path_renders_a_filled_triangle()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawPath(
            p => p.MoveTo(new Point(20.Mm(), 20.Mm()))
                  .LineTo(new Point(80.Mm(), 20.Mm()))
                  .LineTo(new Point(50.Mm(), 70.Mm()))
                  .Close(),
            PenStyle.Default, new BrushStyle(Color.Blue));
        ctx.EndPage();
        HasNonWhitePixel(ctx.Pages[0]).Should().BeTrue();
    }

    [Fact]
    public void Draw_image_renders_supplied_png_bytes()
    {
        // Create a tiny green PNG in memory.
        using var src = new GdiBitmap(20, 20);
        using (var g = GdiGraphics.FromImage(src))
        {
            g.Clear(GdiColor.LimeGreen);
        }
        using var imageMs = new MemoryStream();
        src.Save(imageMs, ImageFormat.Png);

        using var ctx = new GdiRenderingContext(dpi: 96);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawImage(imageMs.ToArray(), new Rectangle(10.Mm(), 10.Mm(), 20.Mm(), 20.Mm()));
        ctx.EndPage();
        HasNonWhitePixel(ctx.Pages[0]).Should().BeTrue();
    }

    [Fact]
    public void Calling_draw_without_begin_page_throws()
    {
        using var ctx = new GdiRenderingContext(dpi: 96);
        Action act = () => ctx.DrawLine(new Point(0.Mm(), 0.Mm()), new Point(10.Mm(), 10.Mm()), PenStyle.Default);
        act.Should().Throw<InvalidOperationException>();
    }

    private static bool HasNonWhitePixel(GdiBitmap bmp)
    {
        int step = Math.Max(1, Math.Min(bmp.Width, bmp.Height) / 50);
        for (int y = 0; y < bmp.Height; y += step)
        {
            for (int x = 0; x < bmp.Width; x += step)
            {
                var c = bmp.GetPixel(x, y);
                if (c.R < 250 || c.G < 250 || c.B < 250)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
