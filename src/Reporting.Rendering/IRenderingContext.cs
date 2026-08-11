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

    /// <summary>Draws <paramref name="text"/> inside <paramref name="bounds"/>, honouring the alignment,
    /// font and colour in <paramref name="style"/>.</summary>
    void DrawText(string text, Rectangle bounds, TextStyle style);

    /// <summary>Strokes a straight line between two points.</summary>
    void DrawLine(Point from, Point to, PenStyle pen);

    /// <summary>Draws a rectangle. <paramref name="fill"/> paints the interior and <paramref name="pen"/>
    /// strokes the outline; either may be null to skip that part.</summary>
    void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill);

    /// <summary>Draws an ellipse inscribed in <paramref name="bounds"/>, with the same null-means-skip
    /// convention as <see cref="DrawRectangle"/>.</summary>
    void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill);

    /// <summary>Decodes and draws an encoded image (PNG, JPEG, …) into <paramref name="bounds"/>, scaled
    /// according to <paramref name="sizing"/>.</summary>
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

    /// <summary>Measures the space <paramref name="text"/> would occupy in <paramref name="style"/>.
    /// When <paramref name="maxWidth"/> is given and the style wraps, the result accounts for the wrapping
    /// and the height grows by whole lines.</summary>
    Size MeasureText(string text, TextStyle style, Unit? maxWidth = null);
}

/// <summary>Pure text-measurement surface — exposed to the layout engine for measuring
/// without owning a full rendering context (e.g. during pagination).</summary>
public interface ITextMeasurer
{
    /// <summary>Measures the space <paramref name="text"/> would occupy in <paramref name="style"/>,
    /// wrapping within <paramref name="maxWidth"/> when one is given.</summary>
    Size Measure(string text, TextStyle style, Unit? maxWidth = null);
}
