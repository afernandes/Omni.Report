using Reporting.Common;
using Reporting.Geometry;
using Reporting.Rendering;

namespace Reporting.Layout.Primitives;

/// <summary>A polyline or filled polygon defined by a vertex list. Charts emit these: a bar is
/// a closed 4-point box, a line series is an open polyline, a pie slice is a closed arc
/// approximated by many points. Backends draw it through <see cref="IPathBuilder"/>.</summary>
public sealed record DrawPolygonPrimitive : LayoutPrimitive
{
    /// <summary>Vertices in absolute page coordinates.</summary>
    public required EquatableArray<Point> Points { get; init; }

    /// <summary>When true the figure is closed (last point connects to the first) and fillable;
    /// when false it's an open polyline that is only stroked.</summary>
    public bool Closed { get; init; } = true;

    /// <summary>Outline stroke. Null draws no outline.</summary>
    public PenStyle? Pen { get; init; }

    /// <summary>Interior fill. Only meaningful when <see cref="Closed"/> is true.</summary>
    public BrushStyle? Fill { get; init; }

    /// <summary>Replays the vertices onto a path builder. Centralised so every backend
    /// (Skia/GDI via <c>DrawPath</c>, the PDF and SVG exporters) shares one
    /// move/line/close implementation.</summary>
    public void BuildPath(IPathBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Points.Count == 0)
        {
            return;
        }
        builder.MoveTo(Points[0]);
        for (int i = 1; i < Points.Count; i++)
        {
            builder.LineTo(Points[i]);
        }
        if (Closed)
        {
            builder.Close();
        }
    }
}
