using Reporting.Geometry;

namespace Reporting.Elements;

public static partial class ImageSizingMath
{

    /// <summary>Where to draw the image and which part of the source to use.</summary>
    /// <param name="Dest">Destination rectangle in page units.</param>
    /// <param name="SrcX">Left of the source crop, as a fraction (0..1) of the image width.</param>
    /// <param name="SrcY">Top of the source crop, as a fraction (0..1) of the image height.</param>
    /// <param name="SrcW">Width of the source crop, as a fraction (0..1).</param>
    /// <param name="SrcH">Height of the source crop, as a fraction (0..1).</param>
    /// <param name="Clip">When true the backend must clip drawing to the bounds (Native may overflow them).</param>
    public readonly record struct Placement(Rectangle Dest, double SrcX, double SrcY, double SrcW, double SrcH, bool Clip);
}
