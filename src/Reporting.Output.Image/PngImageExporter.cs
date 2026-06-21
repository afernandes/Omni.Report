using SkiaSharp;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;
using Reporting.Rendering.Skia;

namespace Reporting.Output.Image;

/// <summary>
/// Rasterises a <see cref="RenderedReport"/> to PNG using the same <c>SkiaPrimitiveRenderer</c> that
/// drives the PDF/SVG backends, so output is visually identical. As an <see cref="IReportExporter"/> the
/// stream is a single tall PNG with all pages stacked vertically (separated by a gap); use
/// <see cref="RenderPages"/> when you need one PNG per page (thumbnails, image viewers).
/// </summary>
public sealed class PngImageExporter : IReportExporter
{
    private readonly float _dpi;
    private readonly int _pageGapPx;

    /// <param name="dpi">Raster resolution. 96 ≈ screen; raise to ~150–300 for print-quality images.</param>
    /// <param name="pageGapPx">Vertical gap between stacked pages in the composite PNG.</param>
    public PngImageExporter(float dpi = 96f, int pageGapPx = 8)
    {
        _dpi = dpi <= 0 ? 96f : dpi;
        _pageGapPx = Math.Max(0, pageGapPx);
    }

    public string Format => "png";
    public string FileExtension => ".png";
    public string ContentType => "image/png";

    /// <summary>One PNG per page — the natural per-page raster, no stacking.</summary>
    public IReadOnlyList<byte[]> RenderPages(RenderedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var result = new List<byte[]>(report.Pages.Count);
        foreach (var page in report.Pages)
        {
            int w = Px(page.PageSetup.PageWidth);
            int h = PageHeightPx(page);
            using var bitmap = new SKBitmap(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                foreach (var primitive in page.Primitives)
                {
                    Replay(canvas, primitive, _dpi);
                }
            }
            result.Add(Encode(bitmap));
        }
        return result;
    }

    /// <summary>Single composite PNG: all pages stacked vertically, separated by the page gap.</summary>
    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var dims = new List<(int W, int H)>(report.Pages.Count);
        int maxWidth = 0, totalHeight = 0;
        foreach (var page in report.Pages)
        {
            int w = Math.Max(1, Px(page.PageSetup.PageWidth));
            int h = Math.Max(1, PageHeightPx(page));
            dims.Add((w, h));
            if (w > maxWidth) maxWidth = w;
            totalHeight += h;
        }
        if (report.Pages.Count > 1)
        {
            totalHeight += (report.Pages.Count - 1) * _pageGapPx;
        }
        if (maxWidth <= 0) maxWidth = 1;
        if (totalHeight <= 0) totalHeight = 1;

        using var bitmap = new SKBitmap(new SKImageInfo(maxWidth, totalHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            int currentY = 0;
            for (int i = 0; i < report.Pages.Count; i++)
            {
                canvas.Save();
                canvas.Translate(0, currentY);
                foreach (var primitive in report.Pages[i].Primitives)
                {
                    Replay(canvas, primitive, _dpi);
                }
                canvas.Restore();
                currentY += dims[i].H + _pageGapPx;
            }
        }

        var bytes = Encode(bitmap);
        output.Write(bytes, 0, bytes.Length);
    }

    private int Px(Unit u) => (int)Math.Ceiling(u.ToPoints() / 72.0 * _dpi);

    private int PageHeightPx(RenderedPage page)
    {
        if (!page.PageSetup.IsContinuous)
        {
            return Px(page.PageSetup.PageHeight);
        }
        // Continuous (thermal) paper: height = bottom of the lowest primitive + bottom margin.
        Unit maxBottom = Unit.Zero;
        foreach (var p in page.Primitives)
        {
            if (p.Bounds.Bottom > maxBottom)
            {
                maxBottom = p.Bounds.Bottom;
            }
        }
        return Math.Max(1, Px(maxBottom + page.PageSetup.Margins.Bottom));
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var img = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void Replay(SKCanvas canvas, LayoutPrimitive primitive, float dpi)
    {
        var clip = SkiaPrimitiveRenderer.BeginClip(canvas, primitive.ClipBounds, dpi);
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
