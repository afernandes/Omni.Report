using FluentAssertions;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Core.Tests;

/// <summary>Geometry of <see cref="ImageSizingMath"/> — the shared placement every backend uses so an
/// image honours Stretch/Fit/Fill/Native consistently.</summary>
public class ImageSizingMathTests
{
    // A 100mm × 50mm box (2:1 landscape).
    private static readonly Rectangle Box = new(Unit.FromMm(10), Unit.FromMm(20), Unit.FromMm(100), Unit.FromMm(50));

    [Fact]
    public void Stretch_fills_the_bounds_with_the_full_source()
    {
        var p = ImageSizingMath.Compute(ImageSizing.Stretch, Box, 400, 100);
        p.Dest.Should().Be(Box);
        (p.SrcX, p.SrcY, p.SrcW, p.SrcH).Should().Be((0d, 0d, 1d, 1d));
        p.Clip.Should().BeFalse();
    }

    [Fact]
    public void Fit_letterboxes_a_tall_image_centered_horizontally()
    {
        // 1:2 portrait image into a 2:1 box → fits to height (50mm), width 25mm, centred in X.
        var p = ImageSizingMath.Compute(ImageSizing.Fit, Box, 100, 200);
        p.Dest.Height.ToMm().Should().BeApproximately(50, 0.1);
        p.Dest.Width.ToMm().Should().BeApproximately(25, 0.1);
        // Centred: left margin = (100-25)/2 = 37.5mm from box X (10mm) → 47.5mm.
        p.Dest.X.ToMm().Should().BeApproximately(47.5, 0.2);
        p.Dest.Y.ToMm().Should().BeApproximately(20, 0.2); // full height, no vertical offset
        (p.SrcW, p.SrcH).Should().Be((1d, 1d)); // no crop
    }

    [Fact]
    public void Fit_pillarboxes_a_wide_image_centered_vertically()
    {
        // 4:1 image into a 2:1 box → fits to width (100mm), height 25mm, centred in Y.
        var p = ImageSizingMath.Compute(ImageSizing.Fit, Box, 400, 100);
        p.Dest.Width.ToMm().Should().BeApproximately(100, 0.1);
        p.Dest.Height.ToMm().Should().BeApproximately(25, 0.1);
        p.Dest.Y.ToMm().Should().BeApproximately(20 + 12.5, 0.2); // (50-25)/2 below box top
    }

    [Fact]
    public void Fill_crops_the_source_to_the_box_aspect_and_draws_full_bounds()
    {
        // 4:1 image into a 2:1 box → dest = bounds, source cropped horizontally to half width, centred.
        var p = ImageSizingMath.Compute(ImageSizing.Fill, Box, 400, 100);
        p.Dest.Should().Be(Box);
        p.SrcW.Should().BeApproximately(0.5, 0.001); // boxAspect/imgAspect = 2/4
        p.SrcX.Should().BeApproximately(0.25, 0.001); // centred
        p.SrcH.Should().Be(1d);
        p.Clip.Should().BeFalse();
    }

    [Fact]
    public void Native_uses_intrinsic_pixel_size_at_96dpi_and_clips()
    {
        // 96px → 1 inch = 25.4mm, anchored at box top-left, clip on (may overflow).
        var p = ImageSizingMath.Compute(ImageSizing.Native, Box, 96, 192);
        p.Dest.X.Should().Be(Box.X);
        p.Dest.Y.Should().Be(Box.Y);
        p.Dest.Width.ToMm().Should().BeApproximately(25.4, 0.1);
        p.Dest.Height.ToMm().Should().BeApproximately(50.8, 0.1);
        p.Clip.Should().BeTrue();
    }

    [Fact]
    public void Degenerate_dims_fall_back_to_filling_bounds()
    {
        ImageSizingMath.Compute(ImageSizing.Fit, Box, 0, 0).Dest.Should().Be(Box);
    }
}
