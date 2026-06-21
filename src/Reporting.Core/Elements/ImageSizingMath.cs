using Reporting.Geometry;

namespace Reporting.Elements;

/// <summary>Computes how an image is placed inside its bounds for each <see cref="ImageSizing"/> mode —
/// shared by every backend so Skia (PDF/PNG/SVG/EscPos/viewer) and GDI agree. Pure geometry: the source
/// crop is returned as fractions (0..1) so each backend maps it to its own pixel/coordinate space.</summary>
public static class ImageSizingMath
{
    // DPI used to interpret an image's native pixel size for ImageSizing.Native (96 = the SSRS/CSS default).
    private const double NativeDpi = 96.0;

    /// <summary>Where to draw the image and which part of the source to use.</summary>
    /// <param name="Dest">Destination rectangle in page units.</param>
    /// <param name="SrcX">Left of the source crop, as a fraction (0..1) of the image width.</param>
    /// <param name="SrcY">Top of the source crop, as a fraction (0..1) of the image height.</param>
    /// <param name="SrcW">Width of the source crop, as a fraction (0..1).</param>
    /// <param name="SrcH">Height of the source crop, as a fraction (0..1).</param>
    /// <param name="Clip">When true the backend must clip drawing to the bounds (Native may overflow them).</param>
    public readonly record struct Placement(Rectangle Dest, double SrcX, double SrcY, double SrcW, double SrcH, bool Clip);

    /// <summary>Computes the placement for an image of <paramref name="imageWidth"/>×<paramref name="imageHeight"/>
    /// pixels inside <paramref name="bounds"/>. Degenerate inputs (zero dims) fall back to filling the bounds.</summary>
    public static Placement Compute(ImageSizing sizing, Rectangle bounds, int imageWidth, int imageHeight)
    {
        var full = new Placement(bounds, 0, 0, 1, 1, false);
        if (imageWidth <= 0 || imageHeight <= 0 || sizing == ImageSizing.Stretch)
        {
            return full;
        }
        double bw = bounds.Width.ToMm(), bh = bounds.Height.ToMm();
        if (bw <= 0 || bh <= 0)
        {
            return full;
        }
        double imgAspect = (double)imageWidth / imageHeight;
        double boxAspect = bw / bh;

        switch (sizing)
        {
            case ImageSizing.Fit: // letterbox: largest centred rect with the image's aspect, inside bounds
            {
                double w, h;
                if (imgAspect > boxAspect) { w = bw; h = bw / imgAspect; }
                else { h = bh; w = bh * imgAspect; }
                return new Placement(Centered(bounds, w, h), 0, 0, 1, 1, false);
            }
            case ImageSizing.Fill: // cover: crop the source to the bounds' aspect, draw to the full bounds
            {
                double sx = 0, sy = 0, sw = 1, sh = 1;
                if (imgAspect > boxAspect) { sw = boxAspect / imgAspect; sx = (1 - sw) / 2; }
                else { sh = imgAspect / boxAspect; sy = (1 - sh) / 2; }
                return new Placement(bounds, sx, sy, sw, sh, false);
            }
            case ImageSizing.Native: // intrinsic pixel size (at 96 dpi), anchored top-left, clipped to bounds
            {
                var dest = new Rectangle(bounds.X, bounds.Y,
                    Unit.FromPixels(imageWidth, NativeDpi), Unit.FromPixels(imageHeight, NativeDpi));
                return new Placement(dest, 0, 0, 1, 1, true);
            }
            default:
                return full;
        }
    }

    private static Rectangle Centered(Rectangle bounds, double wMm, double hMm)
    {
        var w = Unit.FromMm(wMm);
        var h = Unit.FromMm(hMm);
        return new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h);
    }
}
