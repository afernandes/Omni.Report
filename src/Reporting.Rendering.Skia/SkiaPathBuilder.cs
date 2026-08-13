using SkiaSharp;
using Reporting.Geometry;

namespace Reporting.Rendering.Skia;

/// <summary>Adapter from <see cref="IPathBuilder"/> to Skia's <see cref="SKPathBuilder"/>.</summary>
/// <remarks>
/// <para>SkiaSharp 4 made <see cref="SKPath"/> immutable: the mutating <c>MoveTo</c>/<c>LineTo</c>/… methods
/// are obsolete and geometry is accumulated in an <see cref="SKPathBuilder"/> instead, which then produces a
/// finished path. That is why this adapter owns a builder and hands out the path only at the end, via
/// <see cref="DetachPath"/>.</para>
///
/// <para>The builder is unmanaged and must be released, so the adapter is <see cref="IDisposable"/> — the
/// previous version had nothing to dispose because it exposed the <see cref="SKPath"/> directly.</para>
/// </remarks>
internal sealed class SkiaPathBuilder : IPathBuilder, IDisposable
{
    private readonly float _dpi;
    private readonly SKPathBuilder _builder = new();

    public SkiaPathBuilder(float dpi) => _dpi = dpi;

    /// <summary>Hands over the accumulated geometry as a finished path, resetting this builder.</summary>
    /// <remarks><c>Detach</c> rather than <c>Snapshot</c>: the caller draws the path once and disposes it, so
    /// transferring ownership avoids copying the geometry. Calling this twice yields an empty second path.</remarks>
    public SKPath DetachPath() => _builder.Detach();

    public IPathBuilder MoveTo(Point point)
    {
        _builder.MoveTo(point.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder LineTo(Point point)
    {
        _builder.LineTo(point.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder QuadraticTo(Point control, Point end)
    {
        _builder.QuadTo(control.ToSKPoint(_dpi), end.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder CubicTo(Point c1, Point c2, Point end)
    {
        _builder.CubicTo(c1.ToSKPoint(_dpi), c2.ToSKPoint(_dpi), end.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder Arc(Rectangle bounds, double startAngleDegrees, double sweepDegrees)
    {
        _builder.AddArc(bounds.ToSKRect(_dpi), (float)startAngleDegrees, (float)sweepDegrees);
        return this;
    }

    public IPathBuilder Close()
    {
        _builder.Close();
        return this;
    }

    public void Dispose() => _builder.Dispose();
}
