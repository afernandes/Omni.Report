using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Rendering;

/// <summary>
/// Device-independent rendering surface. Concrete implementations target SkiaSharp, GDI,
/// PDF, etc. The contract is intentionally narrow — text, lines, rectangles, images, and
/// arbitrary vector paths cover every report element.
/// </summary>
public interface IRenderingContext : IDisposable
{
    /// <summary>Starts a new physical page.</summary>
    void BeginPage(PageSetup pageSetup);

    /// <summary>Ends the current page. Calling <see cref="BeginPage"/> again opens the next.</summary>
    void EndPage();

    void DrawText(string text, Rectangle bounds, TextStyle style);

    void DrawLine(Point from, Point to, PenStyle pen);

    void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill);

    void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill);

    void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds, ImageSizing sizing = ImageSizing.Fit);

    /// <summary>Draws a vector path. The callback receives a builder; implementations
    /// allocate a backend-specific path object and stroke/fill it.</summary>
    void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill);

    /// <summary>Clips subsequent drawing to <paramref name="bounds"/> (absolute page coords) until the
    /// matching <see cref="PopClip"/> — used for container-rectangle children so overflow is cut.
    /// <paramref name="cornerRadius"/> rounds the clip region when the container rectangle is rounded (zero =
    /// square). Default no-op: a backend that doesn't support clipping renders unclipped (graceful, matching
    /// pre-container behaviour where overflow simply showed). Skia and GDI override these with real clipping.</summary>
    void PushClip(Rectangle bounds, Unit cornerRadius) { }

    /// <summary>Restores the clip region pushed by the matching <see cref="PushClip"/>.</summary>
    void PopClip() { }

    Size MeasureText(string text, TextStyle style, Unit? maxWidth = null);
}

/// <summary>Pure text-measurement surface — exposed to the layout engine for measuring
/// without owning a full rendering context (e.g. during pagination).</summary>
public interface ITextMeasurer
{
    Size Measure(string text, TextStyle style, Unit? maxWidth = null);
}
