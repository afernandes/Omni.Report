using SkiaSharp;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering.Skia;

namespace Reporting.Output.Image;

/// <summary>
/// Rasterises a set of <see cref="LayoutPrimitive"/> into a PNG, using the same
/// <see cref="SkiaPrimitiveRenderer"/> as the PDF/PNG/SVG backends so the output is visually identical.
/// Lets exporters that can't draw vectors (e.g. Word) embed a chart/visual sub-region as an inline image.
/// </summary>
public static class RegionRasterizer
{
    /// <summary>Renders <paramref name="primitives"/> into a PNG sized to <paramref name="region"/> (absolute
    /// page coordinates), translating so the region's top-left maps to the bitmap origin. Primitives outside
    /// the region are clipped by the bitmap bounds.</summary>
    public static byte[] RenderRegionPng(IEnumerable<LayoutPrimitive> primitives, Rectangle region, float dpi)
    {
        ArgumentNullException.ThrowIfNull(primitives);
        if (dpi <= 0)
        {
            dpi = 96f;
        }
        int w = Math.Max(1, ToPx(region.Width, dpi));
        int h = Math.Max(1, ToPx(region.Height, dpi));
        using var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            // Shift the page-absolute primitives so the region's top-left lands at (0,0).
            canvas.Translate(-(float)(region.X.ToPoints() / 72.0 * dpi), -(float)(region.Y.ToPoints() / 72.0 * dpi));
            foreach (var primitive in primitives)
            {
                Replay(canvas, primitive, dpi);
            }
        }
        using var img = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static int ToPx(Unit u, float dpi) => (int)Math.Ceiling(u.ToPoints() / 72.0 * dpi);

    /// <summary>Dispatches a single primitive onto the Skia canvas. Shared by <see cref="PngImageExporter"/>
    /// and <see cref="RenderRegionPng"/> so full-page and region rasterisation stay byte-identical.</summary>
    internal static void Replay(SKCanvas canvas, LayoutPrimitive primitive, float dpi)
    {
        var clip = SkiaPrimitiveRenderer.BeginClip(canvas, primitive.ClipBounds, primitive.ClipCornerRadius, dpi);
        switch (primitive)
        {
            case DrawTextPrimitive t:
                SkiaPrimitiveRenderer.DrawText(canvas, t.Text, t.Bounds, t.Style, dpi);
                break;
            case DrawLinePrimitive l:
                SkiaPrimitiveRenderer.DrawLine(canvas, l.From, l.To, l.Pen, dpi);
                break;
            case DrawRectanglePrimitive r:
                SkiaPrimitiveRenderer.DrawRectangle(canvas, r.Bounds, r.Pen, r.Fill, dpi);
                break;
            case DrawEllipsePrimitive e:
                SkiaPrimitiveRenderer.DrawEllipse(canvas, e.Bounds, e.Pen, e.Fill, dpi);
                break;
            case DrawImagePrimitive i:
                if (i.Data.Count > 0)
                {
                    var copy = new byte[i.Data.Count];
                    for (int k = 0; k < copy.Length; k++)
                    {
                        copy[k] = i.Data[k];
                    }
                    SkiaPrimitiveRenderer.DrawImage(canvas, copy, i.Bounds, dpi, i.Sizing);
                }
                break;
            case DrawPolygonPrimitive poly:
                SkiaPrimitiveRenderer.DrawPath(canvas, poly.BuildPath, poly.Pen, poly.Fill, dpi);
                break;
        }
        SkiaPrimitiveRenderer.EndClip(canvas, clip);
    }
}
