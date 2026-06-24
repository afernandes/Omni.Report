using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>Positional align / distribute geometry (<see cref="Arrange"/>) — pure, browser-independent.</summary>
public class ArrangeTests
{
    private static Rectangle R(int x, int y, int w, int h) => new(new Unit(x), new Unit(y), new Unit(w), new Unit(h));

    [Fact]
    public void AlignLeft_moves_all_to_the_min_left_and_keeps_y()
    {
        var b = new[] { R(100, 0, 50, 10), R(40, 20, 30, 10), R(200, 40, 20, 10) };
        var r = Arrange.Compute(Arrange.Op.AlignLeft, b);
        r.Select(p => p.X.Mils).Should().AllBeEquivalentTo(40);
        r.Select(p => p.Y.Mils).Should().Equal(0, 20, 40);
    }

    [Fact]
    public void AlignRight_aligns_right_edges()
    {
        var b = new[] { R(100, 0, 50, 10), R(40, 20, 30, 10) }; // rights 150, 70 → 150
        var r = Arrange.Compute(Arrange.Op.AlignRight, b);
        (r[0].X.Mils + 50).Should().Be(150);
        (r[1].X.Mils + 30).Should().Be(150);
    }

    [Fact]
    public void AlignHCenter_centres_each_element_on_the_group_centre()
    {
        var b = new[] { R(0, 0, 40, 10), R(100, 0, 20, 10) }; // group centre = (0 + 120)/2 = 60
        var r = Arrange.Compute(Arrange.Op.AlignHCenter, b);
        (r[0].X.Mils + 20).Should().Be(60);
        (r[1].X.Mils + 10).Should().Be(60);
    }

    [Fact]
    public void DistributeH_makes_the_edge_gaps_equal()
    {
        var b = new[] { R(0, 0, 10, 10), R(30, 0, 10, 10), R(100, 0, 10, 10) }; // span 110, widths 30, gap 40
        var r = Arrange.Compute(Arrange.Op.DistributeH, b);
        r.Select(p => p.X.Mils).OrderBy(x => x).Should().Equal(0, 50, 100);
    }

    [Fact]
    public void AlignTop_aligns_top_edges_and_keeps_x()
    {
        var b = new[] { R(5, 100, 10, 10), R(9, 40, 10, 20) };
        var r = Arrange.Compute(Arrange.Op.AlignTop, b);
        r.Select(p => p.Y.Mils).Should().AllBeEquivalentTo(40);
        r.Select(p => p.X.Mils).Should().Equal(5, 9);
    }

    [Fact]
    public void DistributeV_makes_vertical_gaps_equal()
    {
        var b = new[] { R(0, 0, 10, 10), R(0, 30, 10, 10), R(0, 100, 10, 10) };
        var r = Arrange.Compute(Arrange.Op.DistributeV, b);
        r.Select(p => p.Y.Mils).OrderBy(y => y).Should().Equal(0, 50, 100);
    }

    [Fact]
    public void A_single_element_is_left_unchanged()
    {
        var r = Arrange.Compute(Arrange.Op.AlignLeft, new[] { R(5, 7, 10, 10) });
        r[0].X.Mils.Should().Be(5);
        r[0].Y.Mils.Should().Be(7);
    }
}
