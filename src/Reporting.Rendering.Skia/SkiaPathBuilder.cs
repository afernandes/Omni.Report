using SkiaSharp;
using Reporting.Geometry;

namespace Reporting.Rendering.Skia;

/// <summary>Adapter from <see cref="IPathBuilder"/> to <see cref="SKPath"/>.</summary>
internal sealed class SkiaPathBuilder : IPathBuilder
{
    private readonly float _dpi;

    public SkiaPathBuilder(float dpi)
    {
        _dpi = dpi;
        Path = new SKPath();
    }

    public SKPath Path { get; }

    public IPathBuilder MoveTo(Point point)
    {
        Path.MoveTo(point.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder LineTo(Point point)
    {
        Path.LineTo(point.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder QuadraticTo(Point control, Point end)
    {
        Path.QuadTo(control.ToSKPoint(_dpi), end.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder CubicTo(Point c1, Point c2, Point end)
    {
        Path.CubicTo(c1.ToSKPoint(_dpi), c2.ToSKPoint(_dpi), end.ToSKPoint(_dpi));
        return this;
    }

    public IPathBuilder Arc(Rectangle bounds, double startAngleDegrees, double sweepDegrees)
    {
        Path.AddArc(bounds.ToSKRect(_dpi), (float)startAngleDegrees, (float)sweepDegrees);
        return this;
    }

    public IPathBuilder Close()
    {
        Path.Close();
        return this;
    }
}
