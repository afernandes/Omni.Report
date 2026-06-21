using SkiaSharp;

namespace Reporting.Rendering.Tests;

/// <summary>
/// Shared pixel-inspection helpers for the Skia render tests. The renderer is validated
/// through its public surface (render a page → decode the PNG → count "ink" pixels), so we
/// never depend on platform-specific glyph shapes. A pixel counts as ink when any RGB channel
/// is darker/more-saturated than near-white (&lt; 240), matching the threshold the original
/// inline counters used.
/// </summary>
internal static class SkiaTestHelpers
{
    /// <summary>A near-white cutoff: any channel below this counts the pixel as "ink".</summary>
    private const byte InkThreshold = 240;

    /// <summary>True when the pixel is darker/more-saturated than near-white on any channel.</summary>
    public static bool IsInk(SKColor c) => c.Red < InkThreshold || c.Green < InkThreshold || c.Blue < InkThreshold;

    /// <summary>Counts ink pixels over an entire decoded bitmap.</summary>
    public static int CountInkPixels(SKBitmap bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);
        int ink = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                if (IsInk(bmp.GetPixel(x, y)))
                {
                    ink++;
                }
            }
        }
        return ink;
    }

    /// <summary>Counts ink pixels inside <paramref name="region"/> (clamped to the bitmap bounds).</summary>
    public static int CountInkPixels(SKBitmap bmp, SKRectI region)
    {
        ArgumentNullException.ThrowIfNull(bmp);
        int x0 = Math.Max(0, region.Left), y0 = Math.Max(0, region.Top);
        int x1 = Math.Min(bmp.Width, region.Right), y1 = Math.Min(bmp.Height, region.Bottom);
        int ink = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (IsInk(bmp.GetPixel(x, y)))
                {
                    ink++;
                }
            }
        }
        return ink;
    }

    /// <summary>Decodes <paramref name="pngBytes"/> and counts ink pixels over the whole page.</summary>
    public static int CountInkPixels(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        using var bmp = SKBitmap.Decode(pngBytes);
        return CountInkPixels(bmp);
    }

    /// <summary>Decodes <paramref name="pngBytes"/> and counts ink pixels inside <paramref name="region"/>.</summary>
    public static int CountInkPixels(byte[] pngBytes, SKRectI region)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        using var bmp = SKBitmap.Decode(pngBytes);
        return CountInkPixels(bmp, region);
    }

    /// <summary>
    /// Counts ink pixels within a horizontal band of the page expressed as fractions of the
    /// page height (<paramref name="topFraction"/>..<paramref name="bottomFraction"/>, 0 = top,
    /// 1 = bottom). Used to scan only the strip where short text sits on an otherwise empty page.
    /// </summary>
    public static int InkInRows(byte[] pngBytes, double topFraction, double bottomFraction)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        using var bmp = SKBitmap.Decode(pngBytes);
        int y0 = (int)(bmp.Height * topFraction);
        int y1 = (int)(bmp.Height * bottomFraction);
        return CountInkPixels(bmp, SKRectI.Create(0, y0, bmp.Width, y1 - y0));
    }

    /// <summary>
    /// Encodes a solid-colour PNG of <paramref name="width"/>×<paramref name="height"/> pixels —
    /// a deterministic, font-free image source for <c>DrawImage</c> sizing tests.
    /// </summary>
    public static byte[] SolidColorPng(int width, int height, SKColor color)
    {
        using var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(color);
        }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
