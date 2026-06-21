using SkiaSharp;
using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Rendering.Skia;

/// <summary>
/// <see cref="IRenderingContext"/> implementation backed by SkiaSharp bitmaps. Each page is
/// rendered to an in-memory <see cref="SKBitmap"/>; callers retrieve PNG bytes per page via
/// <see cref="GetPagePng"/>, or compose a (raster-backed) PDF via <see cref="ToPdfBytes"/>.
/// </summary>
/// <remarks>
/// For a <em>vector-native</em> PDF whose text is selectable, use
/// <c>Reporting.Output.Pdf.SkiaPdfExporter</c> instead — it bypasses bitmap rasterization.
/// </remarks>
public sealed class SkiaRenderingContext : IRenderingContext, ITextMeasurer
{
    private readonly float _dpi;
    private readonly List<RenderedSurface> _pages = [];
    private SKBitmap? _bitmap;
    private SKCanvas? _canvas;
    private PageSetup? _currentPage;

    public SkiaRenderingContext(float dpi = SkiaConversions.DefaultDpi)
    {
        _dpi = dpi;
    }

    public IReadOnlyList<RenderedSurface> Pages => _pages;

    public void BeginPage(PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(pageSetup);
        if (_canvas is not null)
        {
            EndPage();
        }
        _currentPage = pageSetup;
        int widthPx = (int)Math.Ceiling(pageSetup.PageWidth.Px(_dpi));
        int heightPx = pageSetup.IsContinuous
            ? Math.Max(1, widthPx) // placeholder for thermal — actual content height applied at EndPage
            : (int)Math.Ceiling(pageSetup.PageHeight.Px(_dpi));
        _bitmap = new SKBitmap(new SKImageInfo(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Premul));
        _canvas = new SKCanvas(_bitmap);
        _canvas.Clear(SKColors.White);
    }

    public void EndPage()
    {
        if (_canvas is null || _bitmap is null || _currentPage is null)
        {
            return;
        }
        using var img = SKImage.FromBitmap(_bitmap);
        using var encoded = img.Encode(SKEncodedImageFormat.Png, 100);
        var pngBytes = encoded.ToArray();
        _pages.Add(new RenderedSurface(_currentPage, _bitmap.Width, _bitmap.Height, pngBytes));
        _canvas.Dispose();
        _bitmap.Dispose();
        _canvas = null;
        _bitmap = null;
        _currentPage = null;
    }

    public void DrawText(string text, Rectangle bounds, TextStyle style)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawText(_canvas!, text, bounds, style, _dpi);
    }

    public void DrawLine(Point from, Point to, PenStyle pen)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawLine(_canvas!, from, to, pen, _dpi);
    }

    public void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawRectangle(_canvas!, bounds, pen, fill, _dpi);
    }

    public void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawEllipse(_canvas!, bounds, pen, fill, _dpi);
    }

    public void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawImage(_canvas!, imageData, bounds, _dpi, sizing);
    }

    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawPath(_canvas!, build, pen, fill, _dpi);
    }

    public Size MeasureText(string text, TextStyle style, Unit? maxWidth = null)
        => SkiaPrimitiveRenderer.MeasureText(text, style, maxWidth, _dpi);

    public Size Measure(string text, TextStyle style, Unit? maxWidth = null)
        => SkiaPrimitiveRenderer.MeasureText(text, style, maxWidth, _dpi);

    public void Dispose()
    {
        _canvas?.Dispose();
        _bitmap?.Dispose();
        _canvas = null;
        _bitmap = null;
    }

    /// <summary>Returns the PNG bytes for the page at the given index.</summary>
    public byte[] GetPagePng(int pageIndex) => _pages[pageIndex].PngBytes;

    /// <summary>Encodes all rendered pages into a single PDF document by embedding each page
    /// as a raster image. Text is NOT selectable in the resulting PDF — use
    /// <c>Reporting.Output.Pdf.SkiaPdfExporter</c> for vector-native PDFs.</summary>
    public byte[] ToPdfBytes()
    {
        using var ms = new MemoryStream();
        using (var document = SKDocument.CreatePdf(ms))
        {
            foreach (var page in _pages)
            {
                using var canvas = document.BeginPage(page.WidthPx, page.HeightPx);
                using var data = SKData.CreateCopy(page.PngBytes);
                using var image = SKImage.FromEncodedData(data);
                if (image is not null)
                {
                    canvas.DrawImage(image, 0, 0);
                }
                document.EndPage();
            }
            document.Close();
        }
        return ms.ToArray();
    }

    private void EnsurePage()
    {
        if (_canvas is null)
        {
            throw new InvalidOperationException("No active page. Call BeginPage first.");
        }
    }
}

/// <summary>One rasterized page produced by <see cref="SkiaRenderingContext"/>.</summary>
public sealed record RenderedSurface(PageSetup PageSetup, int WidthPx, int HeightPx, byte[] PngBytes);
