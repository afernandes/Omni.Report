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
}
