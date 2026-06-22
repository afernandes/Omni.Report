using FluentAssertions;
using Reporting.Styling;
using Xunit;

namespace Reporting.Core.Tests;

public class ColorTests
{
    [Theory]
    [InlineData("#FF0000", 255, 0, 0, 255)]
    [InlineData("#80808080", 128, 128, 128, 128)]
    [InlineData("#000000", 0, 0, 0, 255)]
    public void Parses_hex_literal(string hex, byte r, byte g, byte b, byte a)
    {
        var c = Color.FromHex(hex);
        c.R.Should().Be(r);
        c.G.Should().Be(g);
        c.B.Should().Be(b);
        c.A.Should().Be(a);
    }

    [Fact]
    public void Round_trip_hex()
    {
        var c = Color.FromHex("#C2410C");
        c.ToHex().Should().Be("#C2410C");
    }

    [Fact]
    public void Round_trip_hex_with_alpha()
    {
        var c = Color.FromHex("#80FF0000");
        c.ToHex().Should().Be("#80FF0000");
    }

    [Fact]
    public void Throws_on_bad_hex()
    {
        Action act = () => Color.FromHex("#XYZ");
        act.Should().Throw<FormatException>();
    }

    // ── Named colours (full CSS3 / RDL palette) ───────────────────────────────────

    [Theory]
    [InlineData("Maroon", "#800000")]
    [InlineData("Teal", "#008080")]
    [InlineData("Olive", "#808000")]
    [InlineData("Crimson", "#DC143C")]
    [InlineData("SteelBlue", "#4682B4")]
    [InlineData("RebeccaPurple", "#663399")]
    [InlineData("black", "#000000")]
    [InlineData("WHITE", "#FFFFFF")]
    public void FromName_resolves_extended_palette_case_insensitively(string name, string hex)
        => Color.FromName(name)!.Value.ToHex().Should().Be(hex);

    [Theory]
    [InlineData("gray")]
    [InlineData("grey")]
    [InlineData("DarkGray")]
    [InlineData("DarkGrey")]
    public void FromName_accepts_both_gray_and_grey_spellings(string name)
        => Color.FromName(name).Should().NotBeNull();

    [Fact]
    public void FromName_returns_null_for_unknown_name()
        => Color.FromName("notacolour").Should().BeNull();

    [Fact]
    public void FromName_handles_null_and_whitespace()
    {
        Color.FromName(null).Should().BeNull();
        Color.FromName("  teal  ").Should().NotBeNull("surrounding whitespace is trimmed");
    }
}
