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
/// <see cref="EndPage"/> with a height tight to the lowest drawn primitive plus the bottom margin.
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
    private bool _closed;

    public SkiaPdfRenderingContext(Stream stream, SKDocumentPdfMetadata? metadata = null, bool leaveOpen = false)
    {
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
            // Height is unknown until we know what was drawn — record into an oversized
            // bbox and tighten on EndPage.
            _recorder = new SKPictureRecorder();
            _canvas = _recorder.BeginRecording(new SKRect(0, 0, widthPt, 1_000_000f));
            _canvas.Clear(SKColors.White);
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
        if (_canvas is null || _currentPage is null)
        {
            return;
        }

        if (_recorder is not null)
        {
            using var picture = _recorder.EndRecording();
            float widthPt = (float)_currentPage.PageWidth.ToPoints();
            float topMarginPt = (float)_currentPage.Margins.Top.ToPoints();
            float bottomMarginPt = (float)_currentPage.Margins.Bottom.ToPoints();
            float contentBottom = picture.CullRect.Bottom;
            float heightPt = Math.Max(topMarginPt + 1f, contentBottom + bottomMarginPt);

            var pageCanvas = _document.BeginPage(widthPt, heightPt);
            pageCanvas.Clear(SKColors.White);
            pageCanvas.DrawPicture(picture);
            _document.EndPage();

            _recorder.Dispose();
            _recorder = null;
        }
        else
        {
            _document.EndPage();
        }

        _canvas = null;
        _currentPage = null;
    }

    public void DrawText(string text, Rectangle bounds, TextStyle style)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawText(_canvas!, text, bounds, style, PdfDpi);
    }

    public void DrawLine(Point from, Point to, PenStyle pen)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawLine(_canvas!, from, to, pen, PdfDpi);
    }

    public void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawRectangle(_canvas!, bounds, pen, fill, PdfDpi);
    }

    public void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawEllipse(_canvas!, bounds, pen, fill, PdfDpi);
    }

    public void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawImage(_canvas!, imageData, bounds, PdfDpi, sizing);
    }

    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        SkiaPrimitiveRenderer.DrawPath(_canvas!, build, pen, fill, PdfDpi);
    }

    public void PushClip(Rectangle bounds)
    {
        EnsurePage();
        _canvas!.Save();
        _canvas.ClipRect(bounds.ToSKRect(PdfDpi));
    }

    public void PopClip() => _canvas?.Restore();

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
        if (!_closed)
        {
            Close();
        }
        _recorder?.Dispose();
        _document.Dispose();
        if (!_leaveOpen)
        {
            _stream.Dispose();
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
