using Reporting.Geometry;

namespace Reporting.Rendering;

/// <summary>Vector path builder — independent of the underlying rendering backend.</summary>
public interface IPathBuilder
{
    IPathBuilder MoveTo(Point point);
    IPathBuilder LineTo(Point point);
    IPathBuilder QuadraticTo(Point control, Point end);
    IPathBuilder CubicTo(Point c1, Point c2, Point end);
    IPathBuilder Arc(Rectangle bounds, double startAngleDegrees, double sweepDegrees);
    IPathBuilder Close();
}
