using FluentAssertions;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Rendering.Tests;

/// <summary>
/// Verifies the Skia renderer draws <see cref="FontStyle.Underline"/> / <see cref="FontStyle.Strikeout"/>
/// (Skia typefaces don't carry them, so they're stroked from font metrics). We render the same word with
/// each decoration and assert it adds a clear band of ink over the plain baseline — robust across platforms
/// without depending on exact glyph shapes.
/// </summary>
public class TextDecorationTests
{
    private static byte[] RenderWord(FontStyle style)
    {
        using var ctx = new Skia.SkiaRenderingContext(dpi: 96);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("Sublinhado",
            new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 12.Mm()),
            new TextStyle(new Font("Arial", 20, style), Color.Black));
        ctx.EndPage();
        return ctx.Pages[0].PngBytes;
    }

    [Fact]
    public void Underline_adds_ink_below_the_text()
    {
        // Scan only the top strip where the word sits (the page is mostly empty A4).
        var plain = SkiaTestHelpers.InkInRows(RenderWord(FontStyle.Regular), 0.0, 0.08);
        var underlined = SkiaTestHelpers.InkInRows(RenderWord(FontStyle.Underline), 0.0, 0.08);
        underlined.Should().BeGreaterThan(plain + 50, "the underline stroke adds a horizontal band of ink");
    }

    [Fact]
    public void Strikeout_adds_ink_over_the_text()
    {
        var plain = SkiaTestHelpers.InkInRows(RenderWord(FontStyle.Regular), 0.0, 0.08);
        var struck = SkiaTestHelpers.InkInRows(RenderWord(FontStyle.Strikeout), 0.0, 0.08);
        struck.Should().BeGreaterThan(plain + 50, "the strikeout stroke adds a horizontal band of ink");
    }

    [Fact]
    public void Underline_and_strikeout_combine()
    {
        var plain = SkiaTestHelpers.InkInRows(RenderWord(FontStyle.Regular), 0.0, 0.08);
        var both = SkiaTestHelpers.InkInRows(RenderWord(FontStyle.Underline | FontStyle.Strikeout), 0.0, 0.08);
        // Two strokes add more ink than either alone — confirms they coexist (and with Bold/Italic-free text).
        both.Should().BeGreaterThan(plain + 100, "both decorations draw together");
    }
}
