using Reporting.Common;
using Reporting.Geometry;
using Reporting.Rendering;

namespace Reporting.Layout.Primitives;

/// <summary>Draws a rectangle filling <see cref="LayoutPrimitive.Bounds"/>, optionally stroked and/or filled.</summary>
public sealed record DrawRectanglePrimitive : LayoutPrimitive
{
    /// <summary>Outline stroke. Null draws no outline.</summary>
    public PenStyle? Pen { get; init; }

    /// <summary>Interior fill. Null leaves the interior untouched, showing whatever is beneath.</summary>
    public BrushStyle? Fill { get; init; }
}
