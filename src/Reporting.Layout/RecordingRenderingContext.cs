using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering;

namespace Reporting.Layout;

/// <summary>
/// <see cref="IRenderingContext"/> that <em>buffers</em> draw calls into a
/// <see cref="RenderedReport"/> instead of rasterizing/vector-emitting anywhere.
/// </summary>
/// <remarks>
/// <para>Useful when you author a report with the low-level canvas API
/// (<see cref="IRenderingContext"/> / <see cref="ITextMeasurer"/> directly) but still want
/// to feed the resulting page into consumers that expect a <see cref="RenderedReport"/> —
/// the XLSX exporter, the PDF exporter (vector), the Windows spooler printer, the ESC/POS
/// printer, and the Blazor viewer all consume <c>RenderedReport</c>.</para>
///
/// <para>Bridges the two worlds without forcing the caller to build a
/// <c>ReportDefinition</c> just to get tabular Excel output for a hand-drawn invoice.</para>
///
/// <para><b>Measurement</b> is delegated to a real <see cref="ITextMeasurer"/> passed in
/// the constructor (typically a <c>SkiaRenderingContext</c>) — recording can't fabricate
/// font metrics, but it also doesn't need its own rendering backend.</para>
///
/// <para><b>Paths</b> have no equivalent <c>LayoutPrimitive</c> today, so
/// <see cref="DrawPath"/> calls are silently dropped from the recording. If you need paths
/// in the rendered output, draw to <c>SkiaPdfRenderingContext</c> or <c>SkiaRenderingContext</c>
/// directly — this recorder is for the cell-grid / spreadsheet flow, where paths wouldn't
/// survive anyway.</para>
/// </remarks>
public sealed class RecordingRenderingContext : IRenderingContext, ITextMeasurer
{
    private readonly ITextMeasurer _measurer;
    private readonly List<RenderedPage> _pages = [];
    private List<LayoutPrimitive>? _current;
    private PageSetup? _currentSetup;
    private int _pageNumber;

    public RecordingRenderingContext(ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(measurer);
        _measurer = measurer;
    }

    /// <summary>Snapshots the recorded pages into a <see cref="RenderedReport"/>. Safe to call
    /// multiple times; closes the in-progress page if any.</summary>
    public RenderedReport ToRenderedReport(string name = "Canvas")
    {
        if (_current is not null)
        {
            EndPage();
        }
        return new RenderedReport(name, new EquatableArray<RenderedPage>([.. _pages]));
    }

    public IReadOnlyList<RenderedPage> Pages => _pages;

    public void BeginPage(PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(pageSetup);
        if (_current is not null)
        {
            EndPage();
        }
        _currentSetup = pageSetup;
        _current = [];
        _pageNumber++;
    }

    public void EndPage()
    {
        if (_current is null || _currentSetup is null)
        {
            return;
        }
        _pages.Add(new RenderedPage(
            _pageNumber,
            _currentSetup,
            new EquatableArray<LayoutPrimitive>([.. _current])));
        _current = null;
        _currentSetup = null;
    }

    public void DrawText(string text, Rectangle bounds, TextStyle style)
    {
        EnsurePage();
        _current!.Add(new DrawTextPrimitive
        {
            Bounds = bounds,
            Text = text,
            Style = style,
        });
    }

    public void DrawLine(Point from, Point to, PenStyle pen)
    {
        EnsurePage();
        // Bounds is the axis-aligned box that encloses the segment — keeps hit-testing
        // consistent with paginator-emitted primitives.
        var x = from.X < to.X ? from.X : to.X;
        var y = from.Y < to.Y ? from.Y : to.Y;
        var w = (from.X > to.X ? from.X : to.X) - x;
        var h = (from.Y > to.Y ? from.Y : to.Y) - y;
        _current!.Add(new DrawLinePrimitive
        {
            Bounds = new Rectangle(x, y, w, h),
            From = from,
            To = to,
            Pen = pen,
        });
    }

    public void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _current!.Add(new DrawRectanglePrimitive
        {
            Bounds = bounds,
            Pen = pen,
            Fill = fill,
        });
    }

    public void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        _current!.Add(new DrawEllipsePrimitive
        {
            Bounds = bounds,
            Pen = pen,
            Fill = fill,
        });
    }

    public void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
    {
        EnsurePage();
        var copy = imageData.ToArray();
        _current!.Add(new DrawImagePrimitive
        {
            Bounds = bounds,
            Data = new EquatableArray<byte>(copy),
            Sizing = sizing,
        });
    }

    /// <summary>Path primitives have no <see cref="LayoutPrimitive"/> equivalent — the call
    /// is silently dropped. See class-level remarks.</summary>
    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
    {
        EnsurePage();
        // intentionally no-op
    }

    public Size MeasureText(string text, TextStyle style, Unit? maxWidth = null)
        => _measurer.Measure(text, style, maxWidth);

    public Size Measure(string text, TextStyle style, Unit? maxWidth = null)
        => _measurer.Measure(text, style, maxWidth);

    public void Dispose()
    {
        if (_current is not null)
        {
            EndPage();
        }
    }

    private void EnsurePage()
    {
        if (_current is null)
        {
            throw new InvalidOperationException("No active page. Call BeginPage first.");
        }
    }
}
