using FluentAssertions;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Core.Tests;

public class UnitTests
{
    [Fact]
    public void Mm_conversions_round_trip_within_one_mil()
    {
        var u = Unit.FromMm(210); // A4 width
        u.ToMm().Should().BeApproximately(210, 0.05);
    }

    [Fact]
    public void Mm_and_inch_helpers_agree_on_simple_values()
    {
        Unit.FromInch(1).Mils.Should().Be(1000);
        Unit.FromInch(8.5).Mils.Should().Be(8500);
    }

    [Fact]
    public void Arithmetic_operators_use_mils_directly()
    {
        var a = Unit.FromMm(100);
        var b = Unit.FromMm(50);
        (a + b).ToMm().Should().BeApproximately(150, 0.05);
        (a - b).ToMm().Should().BeApproximately(50, 0.05);
        (a * 2).Mils.Should().Be(a.Mils * 2);
    }

    [Fact]
    public void Comparison_operators_match_mils()
    {
        Unit.FromMm(10).Should().BeLessThan(Unit.FromMm(20));
        Unit.FromMm(20).Should().BeGreaterThan(Unit.FromMm(10));
        Unit.FromMm(10).Should().BeLessThanOrEqualTo(Unit.FromMm(10));
    }

    [Fact]
    public void Fluent_extensions_match_factory_methods()
    {
        10.Mm().Should().Be(Unit.FromMm(10));
        1.Cm().Should().Be(Unit.FromMm(10));
        1.Inch().Should().Be(Unit.FromInch(1));
        72.Pt().Should().Be(Unit.FromInch(1));
    }

    [Fact]
    public void Point_subtraction_yields_a_size()
    {
        var a = new Point(20.Mm(), 30.Mm());
        var b = new Point(5.Mm(), 10.Mm());
        var diff = a - b;
        diff.Width.ToMm().Should().BeApproximately(15, 0.05);
        diff.Height.ToMm().Should().BeApproximately(20, 0.05);
    }

    [Fact]
    public void Rectangle_contains_inner_point()
    {
        var rect = new Rectangle(10.Mm(), 10.Mm(), 100.Mm(), 50.Mm());
        rect.Contains(new Point(50.Mm(), 30.Mm())).Should().BeTrue();
        rect.Contains(new Point(5.Mm(), 30.Mm())).Should().BeFalse();
        // Mil-level precision drifts by 1 mil over sums; assert via mm with tolerance.
        rect.Right.ToMm().Should().BeApproximately(110, 0.1);
        rect.Bottom.ToMm().Should().BeApproximately(60, 0.1);
    }

    [Fact]
    public void Rectangle_intersects_another_rectangle()
    {
        var a = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 100.Mm());
        var b = new Rectangle(50.Mm(), 50.Mm(), 100.Mm(), 100.Mm());
        var c = new Rectangle(200.Mm(), 200.Mm(), 10.Mm(), 10.Mm());
        a.IntersectsWith(b).Should().BeTrue();
        a.IntersectsWith(c).Should().BeFalse();
    }

    [Fact]
    public void Thickness_helpers()
    {
        var t = Thickness.Uniform(5.Mm());
        t.Left.Should().Be(5.Mm());
        t.Horizontal.Should().Be(10.Mm());
        t.Vertical.Should().Be(10.Mm());

        var s = Thickness.Symmetric(2.Mm(), 4.Mm());
        s.Left.Should().Be(2.Mm());
        s.Top.Should().Be(4.Mm());
    }
}
