using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using SkiaSharp;

namespace Reporting.Rendering.Skia;

/// <summary>Renders onto a caller-owned canvas without allocating, encoding or resizing a page.
/// Page framing preserves the caller's transform and clip. Neither the canvas nor its bitmap is disposed.</summary>
public sealed class SkiaCanvasRenderingContext : IRenderingContext
{
    private readonly SKCanvas _canvas;
    private readonly float _dpi;
    private int? _pageState;
    private int _clipDepth;
    private bool _disposed;

    public SkiaCanvasRenderingContext(SKCanvas canvas, float dpi = 96f)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (!float.IsFinite(dpi) || dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        _canvas = canvas;
        _dpi = dpi;
    }

    private SKCanvas Canvas
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pageState is null) throw new InvalidOperationException("Nenhuma página está aberta.");
            return _canvas;
        }
    }

    public void BeginPage(PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(pageSetup);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pageState is not null) throw new InvalidOperationException("Já existe uma página aberta.");
        _pageState = _canvas.Save();
    }

    public void EndPage()
    {
        if (_pageState is not { } state) return;
        _canvas.RestoreToCount(state);
        _pageState = null;
        _clipDepth = 0;
    }

    public void DrawText(string text, Rectangle bounds, TextStyle style)
        => SkiaPrimitiveRenderer.DrawText(Canvas, text, bounds, style, _dpi);
    public void DrawLine(Point from, Point to, PenStyle pen)
        => SkiaPrimitiveRenderer.DrawLine(Canvas, from, to, pen, _dpi);
    public void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
        => SkiaPrimitiveRenderer.DrawRectangle(Canvas, bounds, pen, fill, _dpi);
    public void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
        => SkiaPrimitiveRenderer.DrawEllipse(Canvas, bounds, pen, fill, _dpi);
    public void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds, ImageSizing sizing = ImageSizing.Fit)
        => SkiaPrimitiveRenderer.DrawImage(Canvas, imageData, bounds, _dpi, sizing);
    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
        => SkiaPrimitiveRenderer.DrawPath(Canvas, build, pen, fill, _dpi);

    public void PushClip(Rectangle bounds, Unit cornerRadius)
    {
        var canvas = Canvas;
        var saved = canvas.Save();
        try
        {
            SkiaPrimitiveRenderer.ApplyClip(canvas, bounds, cornerRadius, _dpi);
            _clipDepth++;
        }
        catch
        {
            canvas.RestoreToCount(saved);
            throw;
        }
    }

    public void PopClip()
    {
        var canvas = Canvas;
        if (_clipDepth == 0) throw new InvalidOperationException("Não há recorte aberto para restaurar.");
        canvas.Restore();
        _clipDepth--;
    }

    public Size MeasureText(string text, TextStyle style, Unit? maxWidth = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SkiaPrimitiveRenderer.MeasureText(text, style, maxWidth, _dpi);
    }

    public void Dispose()
    {
        EndPage();
        _disposed = true;
    }
}
