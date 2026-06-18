using SkiaSharp;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering.Skia;

/// <summary>Conversions between OmniReport's device-independent types and Skia's pixel-based types.</summary>
internal static class SkiaConversions
{
    /// <summary>
    /// Internal canvas DPI used by the Skia rendering context. 1 mil = (Dpi / 1000) pixels.
    /// We default to 96 dpi to match the typical screen / Blazor viewer; PDF/print scale via SkMatrix.
    /// </summary>
    public const float DefaultDpi = 96f;

    /// <summary>Converts a <see cref="Unit"/> to single-precision pixel coordinates for Skia.</summary>
    public static float Px(this Unit unit, float dpi = DefaultDpi)
        => unit.Mils * dpi / 1000f;

    public static SKRect ToSKRect(this Rectangle rect, float dpi = DefaultDpi)
        => SKRect.Create(rect.X.Px(dpi), rect.Y.Px(dpi), rect.Width.Px(dpi), rect.Height.Px(dpi));

    public static SKPoint ToSKPoint(this Point p, float dpi = DefaultDpi)
        => new(p.X.Px(dpi), p.Y.Px(dpi));

    public static SKColor ToSKColor(this Color color)
        => new(color.R, color.G, color.B, color.A);

    public static SKTextAlign ToSKTextAlign(this HorizontalAlignment alignment)
        => alignment switch
        {
            HorizontalAlignment.Center => SKTextAlign.Center,
            HorizontalAlignment.Right => SKTextAlign.Right,
            _ => SKTextAlign.Left,
        };

    public static SKFontStyle ToSKFontStyle(this FontStyle style)
    {
        var weight = (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
    }

    public static SKPathEffect? ToSKDashEffect(this BorderLineStyle style, float strokeWidth)
        => style switch
        {
            BorderLineStyle.Dashed => SKPathEffect.CreateDash([6 * strokeWidth, 4 * strokeWidth], 0),
            BorderLineStyle.Dotted => SKPathEffect.CreateDash([1 * strokeWidth, 2 * strokeWidth], 0),
            BorderLineStyle.DashDot => SKPathEffect.CreateDash([6 * strokeWidth, 2 * strokeWidth, 1 * strokeWidth, 2 * strokeWidth], 0),
            _ => null,
        };
}
