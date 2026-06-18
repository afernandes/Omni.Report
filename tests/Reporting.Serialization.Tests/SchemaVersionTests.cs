using FluentAssertions;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

public class SchemaVersionTests
{
    [Theory]
    [InlineData("1.0", 1, 0)]
    [InlineData("2.5", 2, 5)]
    [InlineData("0.99", 0, 99)]
    public void Parses_valid_versions(string text, int major, int minor)
    {
        var v = SchemaVersion.Parse(text);
        v.Major.Should().Be(major);
        v.Minor.Should().Be(minor);
        v.ToString().Should().Be(text);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("1")]
    [InlineData("a.b")]
    [InlineData("")]
    public void Rejects_invalid_versions(string text)
    {
        Action act = () => SchemaVersion.Parse(text);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Comparison_ordering()
    {
        SchemaVersion.Parse("1.0").Should().BeLessThan(SchemaVersion.Parse("1.1"));
        SchemaVersion.Parse("1.1").Should().BeLessThan(SchemaVersion.Parse("2.0"));
        SchemaVersion.Parse("1.5").Should().BeGreaterThan(SchemaVersion.Parse("1.3"));
        (SchemaVersion.Parse("1.0") <= SchemaVersion.Parse("1.0")).Should().BeTrue();
        (SchemaVersion.Parse("1.0") >= SchemaVersion.Parse("1.0")).Should().BeTrue();
    }

    [Fact]
    public void Current_is_v1_0()
    {
        SchemaVersion.Current.Should().Be(new SchemaVersion(1, 0));
    }
}
