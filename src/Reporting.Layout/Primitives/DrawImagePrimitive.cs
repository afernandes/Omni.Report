using Reporting.Common;
using Reporting.Geometry;
using Reporting.Rendering;

namespace Reporting.Layout.Primitives;

/// <summary>Draws a raster image into <see cref="LayoutPrimitive.Bounds"/> using the configured sizing mode.</summary>
public sealed record DrawImagePrimitive : LayoutPrimitive
{
    /// <summary>The encoded image bytes (PNG, JPEG, …). Carried by value so the primitive stays
    /// self-contained: it may be exported long after whatever produced it is gone.</summary>
    public required EquatableArray<byte> Data { get; init; }

    /// <summary>How the image fills <see cref="LayoutPrimitive.Bounds"/> — Stretch (distort), Fit (letterbox,
    /// the default), Fill (crop), or Native. Honoured by every backend via <c>ImageSizingMath</c>.</summary>
    public Reporting.Elements.ImageSizing Sizing { get; init; } = Reporting.Elements.ImageSizing.Fit;
}
