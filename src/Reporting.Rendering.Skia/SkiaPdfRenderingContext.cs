using SkiaSharp;
using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Rendering.Skia;

/// <summary>
/// <see cref="IRenderingContext"/> implementation that writes directly to a vector-native PDF
/// via SkiaSharp's <see cref="SKDocument"/>. Each <see cref="BeginPage"/>/<see cref="EndPage"/>
/// pair becomes a real PDF page, so text stays selectable and shapes remain vector — unlike
/// <see cref="SkiaRenderingContext.ToPdfBytes"/>, which rasterizes each page first.
/// </summary>
/// <remarks>
/// Use this when you're drawing through the low-level canvas API (the
/// <c>IRenderingContext</c> + <c>ITextMeasurer</c> interfaces directly) and want a
/// vector PDF as output. For the high-level pipeline that produces a
/// <c>RenderedReport</c>, prefer <c>Reporting.Output.Pdf.SkiaPdfExporter</c> instead.
/// <para>
/// Continuous-paper page setups (e.g. thermal 58/80mm) are supported: the page is recorded
/// to an <see cref="SKPictureRecorder"/> first and the actual PDF page is created on
/// <see cref="EndPage"/> using drawing bounds, clipping, a one-point safety allowance and the bottom margin.
/// Rounded clip envelopes can leave conservative whitespace. Limits are configured through <see cref="ContinuousPageOptions"/>.
/// </para>
/// </remarks>
public sealed class SkiaPdfRenderingContext : IRenderingContext, ITextMeasurer
{
    /// <summary>PDFs measure coordinates in PostScript points (1pt = 1/72 inch).</summary>
    public const float PdfDpi = 72f;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly SKDocument _document;

    private SKCanvas? _canvas;          // Active drawing surface (page canvas or recorder canvas).
    private SKPictureRecorder? _recorder; // Non-null only while a continuous page is in progress.
    private PageSetup? _currentPage;
    private ContinuousPageBounds? _bounds;
    private readonly ContinuousPageOptions _continuousOptions;
    private bool _closed;

    public SkiaPdfRenderingContext(Stream stream, SKDocumentPdfMetadata? metadata = null, bool leaveOpen = false)
        : this(stream, metadata, leaveOpen, new ContinuousPageOptions()) { }

    /// <summary>Creates a vector PDF context with explicit limits for continuous pages.</summary>
    public SkiaPdfRenderingContext(Stream stream, SKDocumentPdfMetadata? metadata, bool leaveOpen,
        ContinuousPageOptions continuousOptions)
    {
        ArgumentNullException.ThrowIfNull(continuousOptions);
        continuousOptions.Validate();
        _continuousOptions = continuousOptions;
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _leaveOpen = leaveOpen;
        _document = SKDocument.CreatePdf(stream, metadata ?? new SKDocumentPdfMetadata());
        if (_document is null)
        {
            throw new InvalidOperationException("Failed to create a PDF document — Skia returned null.");
        }
    }

    public void BeginPage(PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(pageSetup);
        if (_closed)
        {
            throw new InvalidOperationException("PDF document has already been closed.");
        }
        if (_canvas is not null)
        {
            EndPage();
        }

        _currentPage = pageSetup;
        float widthPt = (float)pageSetup.PageWidth.ToPoints();

        if (pageSetup.IsContinuous)
        {
            try
            {
                _bounds = new ContinuousPageBounds(pageSetup, PdfDpi, _continuousOptions);
                _recorder = new SKPictureRecorder();
                _canvas = _recorder.BeginRecording(new SKRect(0, 0, widthPt, (float)_bounds.MaxHeight));
            }
            catch
            {
                _recorder?.Dispose();
                _recorder = null;
                _bounds = null;
                _currentPage = null;
                throw;
            }
        }
        else
        {
            float heightPt = (float)pageSetup.PageHeight.ToPoints();
            _canvas = _document.BeginPage(widthPt, heightPt);
            _canvas.Clear(SKColors.White);
        }
    }

    public void EndPage()
    {
        if (_canvas is null || _currentPage is null) return;
        try
        {
            if (_recorder is not null)
            {
                using var picture = _recorder.EndRecording();
                var pageCanvas = _document.BeginPage((float)_currentPage.PageWidth.ToPoints(), (float)_bounds!.Height);
                try
                {
                    pageCanvas.Clear(SKColors.White);
                    pageCanvas.DrawPicture(picture);
                }
                finally { _document.EndPage(); }
            }
            else { _document.EndPage(); }
        }
        finally
        {
            _recorder?.Dispose();
            _recorder = null;
            _canvas = null;
            _currentPage = null;
            _bounds = null;
        }
    }

    public void DrawText(string text, Rectangle bounds, TextStyle style)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawText(_canvas!, text, bounds, style, PdfDpi, _bounds is null ? null : Track);
    }

    public void DrawLine(Point from, Point to, PenStyle pen)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawLine(_canvas!, from, to, pen, PdfDpi, _bounds is null ? null : Track);
    }

    public void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawRectangle(_canvas!, bounds, pen, fill, PdfDpi, _bounds is null ? null : Track);
    }

    public void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawEllipse(_canvas!, bounds, pen, fill, PdfDpi, _bounds is null ? null : Track);
    }

    public void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawImage(_canvas!, imageData, bounds, PdfDpi, sizing, _bounds is null ? null : Track);
    }

    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _bounds?.CountOperation();
        SkiaPrimitiveRenderer.DrawPath(_canvas!, build, pen, fill, PdfDpi, _bounds is null ? null : Track);
    }

    public void PushClip(Rectangle bounds, Unit cornerRadius)
    {
        EnsurePage();
        _bounds?.PushClip(bounds);
        _canvas!.Save();
        SkiaPrimitiveRenderer.ApplyClip(_canvas, bounds, cornerRadius, PdfDpi);
    }

    public void PopClip()
    {
        _canvas?.Restore();
        _bounds?.PopClip();
    }

    private void Track(SKRect bounds) => _bounds!.Include(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

    public Size MeasureText(string text, TextStyle style, Unit? maxWidth = null)
        => SkiaPrimitiveRenderer.MeasureText(text, style, maxWidth, PdfDpi);

    public Size Measure(string text, TextStyle style, Unit? maxWidth = null)
        => SkiaPrimitiveRenderer.MeasureText(text, style, maxWidth, PdfDpi);

    /// <summary>Finalizes the PDF and writes it to the underlying stream. Idempotent.</summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }
        if (_canvas is not null)
        {
            EndPage();
        }
        _document.Close();
        _closed = true;
    }

    public void Dispose()
    {
        try { Close(); }
        finally
        {
            _recorder?.Dispose();
            _recorder = null;
            _canvas = null;
            _bounds = null;
            _currentPage = null;
            _closed = true;
            _document.Dispose();
            if (!_leaveOpen) _stream.Dispose();
        }
    }

    private void EnsurePage()
    {
        if (_canvas is null)
        {
            throw new InvalidOperationException("No active page. Call BeginPage first.");
        }
    }
}
