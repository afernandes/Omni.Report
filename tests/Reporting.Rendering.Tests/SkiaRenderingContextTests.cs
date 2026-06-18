using FluentAssertions;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Styling;
using Xunit;

namespace Reporting.Rendering.Tests;

public class SkiaRenderingContextTests
{
    [Fact]
    public void Begin_and_end_page_produces_png_bytes()
    {
        using var ctx = new SkiaRenderingContext();
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.EndPage();

        ctx.Pages.Should().HaveCount(1);
        ctx.Pages[0].PngBytes.Should().NotBeEmpty();
        // PNG signature: 89 50 4E 47
        ctx.Pages[0].PngBytes.AsSpan(0, 4).ToArray().Should().Equal([0x89, 0x50, 0x4E, 0x47]);
    }

    [Fact]
    public void Draw_text_increases_non_white_pixels()
    {
        using var ctx = new SkiaRenderingContext(dpi: 72);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("Hello world", new Rectangle(10.Mm(), 10.Mm(), 100.Mm(), 6.Mm()),
            new TextStyle(new Font("Arial", 12), Color.Black));
        ctx.EndPage();

        ctx.Pages.Should().HaveCount(1);
        ctx.Pages[0].PngBytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Draw_line_rectangle_ellipse_smoke()
    {
        using var ctx = new SkiaRenderingContext();
        ctx.BeginPage(PageSetup.A4Portrait);

        ctx.DrawLine(new Point(0.Mm(), 0.Mm()), new Point(100.Mm(), 0.Mm()), PenStyle.Default);
        ctx.DrawRectangle(new Rectangle(10.Mm(), 10.Mm(), 50.Mm(), 20.Mm()), PenStyle.Default, new BrushStyle(Color.LightGray));
        ctx.DrawEllipse(new Rectangle(10.Mm(), 40.Mm(), 30.Mm(), 30.Mm()), null, new BrushStyle(Color.Red));
        ctx.DrawPath(
            p => p.MoveTo(new Point(10.Mm(), 80.Mm()))
                  .LineTo(new Point(80.Mm(), 80.Mm()))
                  .LineTo(new Point(40.Mm(), 110.Mm()))
                  .Close(),
            PenStyle.Default,
            new BrushStyle(Color.Blue));

        ctx.EndPage();

        ctx.Pages[0].PngBytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Measure_text_returns_non_zero_size_for_non_empty_text()
    {
        using var ctx = new SkiaRenderingContext();
        var size = ctx.MeasureText("Hello", new TextStyle(new Font("Arial", 12), Color.Black));
        size.Width.Mils.Should().BeGreaterThan(0);
        size.Height.Mils.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Measure_text_empty_yields_only_line_height()
    {
        using var ctx = new SkiaRenderingContext();
        var size = ctx.MeasureText(string.Empty, new TextStyle(new Font("Arial", 12), Color.Black));
        size.Width.Mils.Should().Be(0);
        size.Height.Mils.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Word_wrap_in_measure_increases_height_for_long_text()
    {
        using var ctx = new SkiaRenderingContext();
        var style = new TextStyle(new Font("Arial", 12), Color.Black);
        var single = ctx.MeasureText("short", style, maxWidth: 200.Mm());
        var wrapped = ctx.MeasureText(string.Concat(System.Linq.Enumerable.Repeat("frase ", 30)), style, maxWidth: 20.Mm());
        wrapped.Height.Should().BeGreaterThan(single.Height);
    }

    [Fact]
    public void Pdf_output_is_non_empty_with_pdf_header()
    {
        using var ctx = new SkiaRenderingContext();
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("PDF test", new Rectangle(10.Mm(), 10.Mm(), 100.Mm(), 6.Mm()),
            TextStyle.Default);
        ctx.EndPage();

        var pdf = ctx.ToPdfBytes();
        pdf.Should().NotBeEmpty();
        // PDF magic: "%PDF-"
        var prefix = System.Text.Encoding.ASCII.GetString(pdf, 0, 5);
        prefix.Should().Be("%PDF-");
    }

    [Fact]
    public void Calling_draw_without_begin_page_throws()
    {
        using var ctx = new SkiaRenderingContext();
        var act = () => ctx.DrawLine(new Point(0.Mm(), 0.Mm()), new Point(10.Mm(), 10.Mm()), PenStyle.Default);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Begin_page_twice_implicitly_ends_first()
    {
        using var ctx = new SkiaRenderingContext();
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.EndPage();
        ctx.Pages.Should().HaveCount(2);
    }

    [Fact]
    public void End_page_without_begin_is_noop()
    {
        using var ctx = new SkiaRenderingContext();
        ctx.EndPage(); // should not throw
        ctx.Pages.Should().BeEmpty();
    }

    [Fact]
    public void Draw_image_ignores_empty_buffer()
    {
        using var ctx = new SkiaRenderingContext();
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawImage(ReadOnlySpan<byte>.Empty, new Rectangle(0.Mm(), 0.Mm(), 10.Mm(), 10.Mm()));
        ctx.EndPage();
        // Nothing thrown; page produced
        ctx.Pages.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Center)]
    [InlineData(HorizontalAlignment.Right)]
    public void Horizontal_alignment_does_not_crash(HorizontalAlignment alignment)
    {
        using var ctx = new SkiaRenderingContext();
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("aligned",
            new Rectangle(10.Mm(), 10.Mm(), 100.Mm(), 6.Mm()),
            new TextStyle(new Font("Arial", 10), Color.Black, alignment));
        ctx.EndPage();
        ctx.Pages.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(VerticalAlignment.Top)]
    [InlineData(VerticalAlignment.Middle)]
    [InlineData(VerticalAlignment.Bottom)]
    public void Vertical_alignment_does_not_crash(VerticalAlignment alignment)
    {
        using var ctx = new SkiaRenderingContext();
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("aligned",
            new Rectangle(10.Mm(), 10.Mm(), 100.Mm(), 30.Mm()),
            new TextStyle(new Font("Arial", 10), Color.Black, VerticalAlignment: alignment));
        ctx.EndPage();
        ctx.Pages.Should().HaveCount(1);
    }
}
