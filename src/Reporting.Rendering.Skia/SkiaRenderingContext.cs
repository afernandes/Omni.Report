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
    private SKPictureRecorder? _recorder;
    private PageSetup? _currentPage;
    private ContinuousPageBounds? _bounds;
    private readonly ContinuousPageOptions _continuousOptions;

    public SkiaRenderingContext(float dpi = SkiaConversions.DefaultDpi)
        : this(new ContinuousPageOptions(), dpi) { }

    /// <summary>Creates a bitmap context with explicit limits for continuous pages.</summary>
    public SkiaRenderingContext(ContinuousPageOptions continuousOptions, float dpi = SkiaConversions.DefaultDpi)
    {
        ArgumentNullException.ThrowIfNull(continuousOptions);
        continuousOptions.Validate();
        if (!float.IsFinite(dpi) || dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        _continuousOptions = continuousOptions;
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
        if (pageSetup.IsContinuous)
        {
            try
            {
                _bounds = new ContinuousPageBounds(pageSetup, _dpi, _continuousOptions);
                _recorder = new SKPictureRecorder();
                _canvas = _recorder.BeginRecording(new SKRect(0, 0, pageSetup.PageWidth.Px(_dpi), (float)_bounds.MaxHeight));
            }
            catch { ReleasePage(); throw; }
            return;
        }
        int widthPx = checked((int)Math.Ceiling(pageSetup.PageWidth.ToPixels(_dpi)));
        int heightPx = checked((int)Math.Ceiling(pageSetup.PageHeight.ToPixels(_dpi)));
        CreateBitmap(widthPx, heightPx);
    }

    private void CreateBitmap(int width, int height)
    {
        _bitmap = new SKBitmap();
        if (!_bitmap.TryAllocPixels(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            _bitmap.Dispose();
            _bitmap = null;
            throw new InvalidOperationException("Unable to allocate the page bitmap.");
        }
        _canvas = new SKCanvas(_bitmap);
        _canvas.Clear(SKColors.White);
    }

    public void EndPage()
    {
        if (_canvas is null || _currentPage is null) return;
        try
        {
            if (_recorder is not null)
            {
                using var picture = _recorder.EndRecording();
                _canvas = null;
                var size = _bounds!.RasterSize();
                CreateBitmap(size.Width, size.Height);
                _canvas!.DrawPicture(picture);
            }
            using var img = SKImage.FromBitmap(_bitmap!);
            using var encoded = img.Encode(SKEncodedImageFormat.Png, 100);
            _pages.Add(new RenderedSurface(_currentPage, _bitmap!.Width, _bitmap.Height, encoded.ToArray()));
        }
        finally { ReleasePage(); }
    }

    private void ReleasePage()
    {
        if (_bitmap is not null) _canvas?.Dispose();
        _bitmap?.Dispose();
        _recorder?.Dispose();
        _canvas = null;
        _bitmap = null;
        _recorder = null;
        _currentPage = null;
        _bounds = null;
    }

    public void DrawText(string text, Rectangle bounds, TextStyle style)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawText(_canvas!, text, bounds, style, _dpi, _bounds is null ? null : Track);
    }

    public void DrawLine(Point from, Point to, PenStyle pen)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawLine(_canvas!, from, to, pen, _dpi, _bounds is null ? null : Track);
    }

    public void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawRectangle(_canvas!, bounds, pen, fill, _dpi, _bounds is null ? null : Track);
    }

    public void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawEllipse(_canvas!, bounds, pen, fill, _dpi, _bounds is null ? null : Track);
    }

    public void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawImage(_canvas!, imageData, bounds, _dpi, sizing, _bounds is null ? null : Track);
    }

    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawPath(_canvas!, build, pen, fill, _dpi, _bounds is null ? null : Track);
    }

    public void PushClip(Rectangle bounds, Unit cornerRadius)
    {
        EnsurePage();
        _bounds?.PushClip(bounds);
        _canvas!.Save();
        SkiaPrimitiveRenderer.ApplyClip(_canvas, bounds, cornerRadius, _dpi);
    }

    public void PopClip()
    {
        _canvas?.Restore();
        _bounds?.PopClip();
    }

    private void Track(SKRect bounds) => _bounds!.Include(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

    public Size MeasureText(string text, TextStyle style, Unit? maxWidth = null)
        => SkiaPrimitiveRenderer.MeasureText(text, style, maxWidth, _dpi);

    public Size Measure(string text, TextStyle style, Unit? maxWidth = null)
        => SkiaPrimitiveRenderer.MeasureText(text, style, maxWidth, _dpi);

    public void Dispose() => ReleasePage();

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
                using var canvas = document.BeginPage(page.WidthPx * 72f / _dpi, page.HeightPx * 72f / _dpi);
                using var data = SKData.CreateCopy(page.PngBytes);
                using var image = SKImage.FromEncodedData(data);
                if (image is not null)
                {
                    // Ver a nota sobre SKSamplingOptions.Default em SkiaPrimitiveRenderer.DrawImage.
                    canvas.Scale(72f / _dpi);
                    canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default, paint: null);
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
