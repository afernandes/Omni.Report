using FluentAssertions;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Styling;
using SkiaSharp;
using Xunit;

namespace Reporting.Rendering.Tests;

/// <summary>
/// Verifies that emoji codepoints render as visible glyphs (not tofu/squares) via the
/// per-codepoint font fallback. We decode the page PNG and count non-white pixels in
/// the emoji's bounding region — a tofu would still produce *some* pixels but very few,
/// while a proper emoji glyph produces hundreds. We assert "noticeably more than text-only"
/// to keep the test robust across platforms (Segoe UI Emoji on Windows, Apple Color Emoji
/// on macOS, Noto Color Emoji on Linux).
/// </summary>
public class EmojiFallbackTests
{
    /// <summary>Counts non-white pixels in a PNG byte buffer. Used as a proxy for "is
    /// something visible?" without relying on platform-specific font output.</summary>
    private static int CountInkPixels(byte[] pngBytes)
    {
        using var bmp = SKBitmap.Decode(pngBytes);
        int ink = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                // Any pixel darker/coloured than near-white counts as ink.
                if (c.Red < 240 || c.Green < 240 || c.Blue < 240) ink++;
            }
        return ink;
    }

    [Fact]
    public void Emoji_codepoint_renders_visible_ink()
    {
        // This test depends on a system emoji font (Segoe UI Emoji on Windows, Apple Color
        // Emoji on macOS, Noto Color Emoji on Linux). Minimal CI runners (e.g. bare ubuntu)
        // ship no emoji font, so fallback has nothing to render. Detect that and skip the
        // ink assertion instead of failing — there is genuinely nothing to validate.
        const int emojiCodepoint = 0x1F464; // 👤 BUST IN SILHOUETTE
        using var emojiTypeface = SKFontManager.Default.MatchCharacter(emojiCodepoint);
        if (emojiTypeface is null) return; // host has no emoji-capable font

        // Render a string with an emoji at moderate size on an A4 page. If font fallback
        // works, the page picks up real emoji ink (color or monochrome — either is fine).
        // If it doesn't, only the trailing "ABC" letters contribute ink.
        using var ctxEmoji = new SkiaRenderingContext(dpi: 72);
        ctxEmoji.BeginPage(PageSetup.A4Portrait);
        ctxEmoji.DrawText("👤 ABC",
            new Rectangle(10.Mm(), 10.Mm(), 60.Mm(), 12.Mm()),
            new TextStyle(new Font("Arial", 18), Color.Black));
        ctxEmoji.EndPage();
        var inkWithEmoji = CountInkPixels(ctxEmoji.Pages[0].PngBytes);

        using var ctxNoEmoji = new SkiaRenderingContext(dpi: 72);
        ctxNoEmoji.BeginPage(PageSetup.A4Portrait);
        ctxNoEmoji.DrawText("ABC",
            new Rectangle(10.Mm(), 10.Mm(), 60.Mm(), 12.Mm()),
            new TextStyle(new Font("Arial", 18), Color.Black));
        ctxNoEmoji.EndPage();
        var inkLettersOnly = CountInkPixels(ctxNoEmoji.Pages[0].PngBytes);

        // The emoji glyph (typically ~16–24 px square at 18 pt) should add at least 50
        // ink pixels over the letters-only baseline. A tofu box would only outline the
        // square edges (≈4×16=64 pixels stroke), so we keep the threshold conservative
        // but require a clear positive delta — tofu doesn't fill the interior.
        var delta = inkWithEmoji - inkLettersOnly;
        delta.Should().BeGreaterThan(100,
            "the emoji must add visible ink — if fallback is missing, the delta drops near zero");
    }

    [Fact]
    public void Plain_ascii_still_renders_after_fallback_changes()
    {
        // Regression guard: the run-building path must not break plain Latin-1 text.
        // Specifically, the entire string should still produce one run (or near-one)
        // and render the same approximate amount of ink as before the refactor.
        using var ctx = new SkiaRenderingContext(dpi: 72);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawText("Olá, mundo — Bom dia.",
            new Rectangle(10.Mm(), 10.Mm(), 80.Mm(), 6.Mm()),
            new TextStyle(new Font("Arial", 12), Color.Black));
        ctx.EndPage();

        CountInkPixels(ctx.Pages[0].PngBytes).Should().BeGreaterThan(200,
            "plain text must still render normally after the fallback refactor");
    }
}
