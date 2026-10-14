using Reporting.Common;
using Reporting.Geometry;
using Reporting.Rendering;

namespace Reporting.Layout.Primitives;

/// <summary>Draws an ellipse inscribed in <see cref="LayoutPrimitive.Bounds"/>, optionally stroked and/or filled.</summary>
public sealed record DrawEllipsePrimitive : LayoutPrimitive
{
    /// <summary>Outline stroke. Null draws no outline.</summary>
    public PenStyle? Pen { get; init; }

    /// <summary>Interior fill. Null leaves the interior untouched, showing whatever is beneath.</summary>
    public BrushStyle? Fill { get; init; }
}
