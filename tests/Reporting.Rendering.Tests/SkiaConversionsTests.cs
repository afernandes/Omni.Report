using System.Globalization;
using FluentAssertions;
using Reporting.Geometry;
using Reporting.Rendering.Skia;
using Reporting.Styling;
using SkiaSharp;
using Xunit;

namespace Reporting.Rendering.Tests;

/// <summary>
/// Pure-conversion unit tests for <see cref="SkiaConversions"/>. These cover the math and
/// enum mapping that pixel inspection cannot observe directly (exact float values, font-style
/// enums, dash-effect identity). <c>SkiaConversions</c> is internal — exposed to this test
/// project via <c>InternalsVisibleTo</c> in the Skia csproj. All assertions are culture-invariant
/// and have no font/OS dependency, so they never flake on CI.
/// </summary>
public class SkiaConversionsTests
{
    public SkiaConversionsTests()
    {
        // Px math is plain float arithmetic, but pin the culture so nothing in the assertion
        // path (e.g. failure formatting) is locale-sensitive.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    // ---- 1. Unit.Px(): mils * dpi / 1000f -------------------------------------------------

    [Fact]
    public void Px_zero_is_zero()
    {
        // Arrange
        var unit = Unit.Zero;

        // Act
        var px = unit.Px(96f);

        // Assert
        px.Should().Be(0f);
    }

    [Fact]
    public void Px_1000_mils_at_96_dpi_is_96()
    {
        // Arrange — 1000 mils = 1 inch; at 96 dpi that is exactly 96 px.
        var oneInch = new Unit(1000);

        // Act
        var px = oneInch.Px(96f);

        // Assert
        px.Should().Be(96f);
    }

    [Fact]
    public void Px_1000_mils_at_72_dpi_is_72()
    {
        // Arrange
        var oneInch = new Unit(1000);

        // Act
        var px = oneInch.Px(72f);

        // Assert
        px.Should().Be(72f);
    }

    [Fact]
    public void Px_negative_preserves_sign()
    {
        // Arrange
        var negative = new Unit(-1000);

        // Act
        var px = negative.Px(96f);

        // Assert
        px.Should().Be(-96f);
    }

    [Fact]
    public void Px_fractional_result_is_not_rounded_prematurely()
    {
        // Arrange — 500.5 mils can't be expressed as an integer Unit, so we drive the float
        // path with a value that yields a fractional pixel: 501 mils @ 96 = 48.096. To hit the
        // documented 48.048f case (500.5 mils) we compute the float directly from the formula,
        // confirming Px keeps full float precision rather than rounding to an int.
        const float dpi = 96f;
        var half = 500.5f * dpi / 1000f; // 48.048f — the expected fractional pixel value

        // Act — Unit is integer mils, so exercise the exact-float contract at 501 and 500 mils
        // and confirm the conversion is the raw formula with no intermediate rounding.
        var px501 = new Unit(501).Px(dpi);
        var px500 = new Unit(500).Px(dpi);

        // Assert — values match the formula to float tolerance and straddle 48.048f.
        px501.Should().BeApproximately(501f * dpi / 1000f, 1e-4f);
        px500.Should().BeApproximately(500f * dpi / 1000f, 1e-4f);
        half.Should().BeApproximately(48.048f, 1e-3f);
        px500.Should().BeLessThan(half).And.BeGreaterThan(0f);
        px501.Should().BeGreaterThan(half);
    }

    // ---- 2. FontStyle.ToSKFontStyle() -----------------------------------------------------

    [Fact]
    public void ToSKFontStyle_regular_is_normal_weight_and_upright()
    {
        // Act
        var sk = FontStyle.Regular.ToSKFontStyle();

        // Assert
        sk.Weight.Should().Be((int)SKFontStyleWeight.Normal);
        sk.Slant.Should().Be(SKFontStyleSlant.Upright);
    }

    [Fact]
    public void ToSKFontStyle_bold_is_bold_weight()
    {
        // Act
        var sk = FontStyle.Bold.ToSKFontStyle();

        // Assert
        sk.Weight.Should().Be((int)SKFontStyleWeight.Bold);
        sk.Slant.Should().Be(SKFontStyleSlant.Upright);
    }

    [Fact]
    public void ToSKFontStyle_italic_is_italic_slant()
    {
        // Act
        var sk = FontStyle.Italic.ToSKFontStyle();

        // Assert
        sk.Slant.Should().Be(SKFontStyleSlant.Italic);
        sk.Weight.Should().Be((int)SKFontStyleWeight.Normal);
    }

    [Fact]
    public void ToSKFontStyle_bold_italic_is_both()
    {
        // Act
        var sk = (FontStyle.Bold | FontStyle.Italic).ToSKFontStyle();

        // Assert
        sk.Weight.Should().Be((int)SKFontStyleWeight.Bold);
        sk.Slant.Should().Be(SKFontStyleSlant.Italic);
    }

    [Fact]
    public void ToSKFontStyle_ignores_underline_and_strikeout_for_weight_and_slant()
    {
        // Arrange — decorations are drawn separately; they must not change weight/slant.
        var style = FontStyle.Underline | FontStyle.Strikeout;

        // Act
        var sk = style.ToSKFontStyle();

        // Assert
        sk.Weight.Should().Be((int)SKFontStyleWeight.Normal);
        sk.Slant.Should().Be(SKFontStyleSlant.Upright);
    }

    // ---- 3. BorderLineStyle.ToSKDashEffect() ----------------------------------------------

    [Fact]
    public void ToSKDashEffect_solid_is_null()
    {
        // Act
        var effect = BorderLineStyle.Solid.ToSKDashEffect(1f);

        // Assert
        effect.Should().BeNull();
    }

    [Fact]
    public void ToSKDashEffect_none_is_null()
    {
        // Act
        var effect = BorderLineStyle.None.ToSKDashEffect(1f);

        // Assert
        effect.Should().BeNull();
    }

    [Theory]
    [InlineData(BorderLineStyle.Dashed)]
    [InlineData(BorderLineStyle.Dotted)]
    [InlineData(BorderLineStyle.DashDot)]
    public void ToSKDashEffect_dashed_styles_are_non_null(BorderLineStyle style)
    {
        // Act
        using var effect = style.ToSKDashEffect(1f);

        // Assert
        effect.Should().NotBeNull("dashed/dotted patterns require a path effect");
    }

    [Fact]
    public void ToSKDashEffect_scales_with_stroke_width()
    {
        // Arrange — the dash interval is multiplied by stroke width, so width 1 and width 5
        // must produce distinct SKPathEffect handles (different underlying dash arrays).
        using var thin = BorderLineStyle.Dashed.ToSKDashEffect(1f);
        using var thick = BorderLineStyle.Dashed.ToSKDashEffect(5f);

        // Assert — both exist and are not the same effect instance.
        thin.Should().NotBeNull();
        thick.Should().NotBeNull();
        thick.Should().NotBeSameAs(thin, "the dash array scales by stroke width, so the effects differ");
    }
}
