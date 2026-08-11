using Reporting.Geometry;

namespace Reporting.Rendering;

/// <summary>Vector path builder — independent of the underlying rendering backend.</summary>
public interface IPathBuilder
{
    /// <summary>Starts a new subpath at <paramref name="point"/> without drawing.</summary>
    IPathBuilder MoveTo(Point point);

    /// <summary>Draws a straight segment from the current position to <paramref name="point"/>.</summary>
    IPathBuilder LineTo(Point point);

    /// <summary>Draws a quadratic Bezier through one control point.</summary>
    /// <param name="control">The control point that bends the curve.</param>
    /// <param name="end">Where the curve finishes.</param>
    IPathBuilder QuadraticTo(Point control, Point end);

    /// <summary>Draws a cubic Bezier through two control points.</summary>
    /// <param name="c1">Control point leaving the current position.</param>
    /// <param name="c2">Control point arriving at <paramref name="end"/>.</param>
    /// <param name="end">Where the curve finishes.</param>
    IPathBuilder CubicTo(Point c1, Point c2, Point end);

    /// <summary>Draws an elliptical arc inscribed in <paramref name="bounds"/>.</summary>
    /// <param name="bounds">Box the full ellipse would occupy.</param>
    /// <param name="startAngleDegrees">Where the arc starts; 0 is the 3 o'clock direction.</param>
    /// <param name="sweepDegrees">How far the arc travels; positive sweeps clockwise.</param>
    IPathBuilder Arc(Rectangle bounds, double startAngleDegrees, double sweepDegrees);

    /// <summary>Closes the current subpath with a straight segment back to its start, making it fillable.</summary>
    IPathBuilder Close();
}
