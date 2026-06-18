using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using ReportingPoint = Reporting.Geometry.Point;
using ReportingRectangle = Reporting.Geometry.Rectangle;
using GdiPointF = System.Drawing.PointF;

namespace Reporting.Rendering.Gdi;

[SupportedOSPlatform("windows")]
internal sealed class GdiPathBuilder : IPathBuilder
{
    private readonly float _dpi;
    private ReportingPoint _currentPoint;

    public GdiPathBuilder(float dpi)
    {
        _dpi = dpi;
        Path = new GraphicsPath();
    }

    public GraphicsPath Path { get; }

    public IPathBuilder MoveTo(ReportingPoint point)
    {
        _currentPoint = point;
        Path.StartFigure();
        return this;
    }

    public IPathBuilder LineTo(ReportingPoint point)
    {
        Path.AddLine(_currentPoint.ToPointF(_dpi), point.ToPointF(_dpi));
        _currentPoint = point;
        return this;
    }

    public IPathBuilder QuadraticTo(ReportingPoint control, ReportingPoint end)
    {
        // GDI+ exposes only cubic Béziers; convert quadratic to cubic via the 1/3 + 2/3 rule.
        var p1 = LerpToward(_currentPoint, control, 2f / 3f);
        var p2 = LerpToward(end, control, 2f / 3f);
        Path.AddBezier(_currentPoint.ToPointF(_dpi), p1, p2, end.ToPointF(_dpi));
        _currentPoint = end;
        return this;
    }

    public IPathBuilder CubicTo(ReportingPoint c1, ReportingPoint c2, ReportingPoint end)
    {
        Path.AddBezier(_currentPoint.ToPointF(_dpi), c1.ToPointF(_dpi), c2.ToPointF(_dpi), end.ToPointF(_dpi));
        _currentPoint = end;
        return this;
    }

    public IPathBuilder Arc(ReportingRectangle bounds, double startAngleDegrees, double sweepDegrees)
    {
        Path.AddArc(bounds.ToRectF(_dpi), (float)startAngleDegrees, (float)sweepDegrees);
        return this;
    }

    public IPathBuilder Close()
    {
        Path.CloseFigure();
        return this;
    }

    private GdiPointF LerpToward(ReportingPoint endpoint, ReportingPoint other, float t)
    {
        var ex = endpoint.ToPointF(_dpi);
        var ox = other.ToPointF(_dpi);
        return new GdiPointF(ex.X + (ox.X - ex.X) * t, ex.Y + (ox.Y - ex.Y) * t);
    }
}
