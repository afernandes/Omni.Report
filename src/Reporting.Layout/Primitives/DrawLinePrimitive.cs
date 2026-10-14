using Reporting.Common;
using Reporting.Geometry;
using Reporting.Rendering;

namespace Reporting.Layout.Primitives;

/// <summary>Draws a straight line from <c>From</c> to <c>To</c> with the given pen.</summary>
public sealed record DrawLinePrimitive : LayoutPrimitive
{
    /// <summary>Start point, in absolute page coordinates.</summary>
    public required Point From { get; init; }

    /// <summary>End point, in absolute page coordinates.</summary>
    public required Point To { get; init; }

    /// <summary>Stroke to draw the segment with.</summary>
    public required PenStyle Pen { get; init; }
}
