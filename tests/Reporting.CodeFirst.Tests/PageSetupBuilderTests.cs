using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Paper;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public class PageSetupBuilderTests
{
    [Fact]
    public void Defaults_to_a4_portrait_15mm_uniform()
    {
        var setup = new PageSetupBuilder();
        var built = InvokeBuild(setup);
        built.Paper.Should().Be(PaperSize.A4);
        built.Orientation.Should().Be(Orientation.Portrait);
        built.Margins.Top.ToMm().Should().BeApproximately(15, 0.1);
    }

    [Theory]
    [InlineData("A5", 148, 210)]
    [InlineData("Letter", 215.9, 279.4)]
    [InlineData("Thermal58", 58, 0)]
    [InlineData("Thermal80", 80, 0)]
    public void Paper_shortcuts(string preset, double widthMm, double heightMm)
    {
        var builder = new PageSetupBuilder();
        _ = preset switch
        {
            "A5" => builder.A5(),
            "Letter" => builder.Letter(),
            "Thermal58" => builder.Thermal58(),
            "Thermal80" => builder.Thermal80(),
            _ => builder,
        };
        var built = InvokeBuild(builder);
        built.Paper.Width.ToMm().Should().BeApproximately(widthMm, 0.5);
        built.Paper.Height.ToMm().Should().BeApproximately(heightMm, 0.5);
    }

    [Fact]
    public void Custom_paper_uses_supplied_dimensions()
    {
        var b = new PageSetupBuilder().CustomPaper("Square", 100, 100);
        InvokeBuild(b).Paper.Name.Should().Be("Square");
    }

    [Fact]
    public void Margins_uniform_and_per_side()
    {
        var u = InvokeBuild(new PageSetupBuilder().Margins(5));
        u.Margins.Top.ToMm().Should().BeApproximately(5, 0.1);
        u.Margins.Left.ToMm().Should().BeApproximately(5, 0.1);

        var s = InvokeBuild(new PageSetupBuilder().Margins(top: 10, right: 20, bottom: 30, left: 40));
        s.Margins.Top.ToMm().Should().BeApproximately(10, 0.1);
        s.Margins.Right.ToMm().Should().BeApproximately(20, 0.1);
        s.Margins.Bottom.ToMm().Should().BeApproximately(30, 0.1);
        s.Margins.Left.ToMm().Should().BeApproximately(40, 0.1);
    }

    [Fact]
    public void Landscape_swaps_dimensions()
    {
        var built = InvokeBuild(new PageSetupBuilder().Landscape());
        built.PageWidth.ToMm().Should().BeApproximately(297, 0.1);
        built.PageHeight.ToMm().Should().BeApproximately(210, 0.1);
    }

    [Fact]
    public void Columns_at_least_one()
    {
        var built = InvokeBuild(new PageSetupBuilder().Columns(3, spacingMm: 10));
        built.Columns.Should().Be(3);
        built.ColumnSpacing.ToMm().Should().BeApproximately(10, 0.1);

        var clamped = InvokeBuild(new PageSetupBuilder().Columns(0));
        clamped.Columns.Should().Be(1);
    }

    [Fact]
    public void Portrait_is_idempotent()
    {
        var built = InvokeBuild(new PageSetupBuilder().Landscape().Portrait());
        built.Orientation.Should().Be(Orientation.Portrait);
    }

    // ── Helper ──────────────────────────────────────────────────────────────────

    private static PageSetup InvokeBuild(PageSetupBuilder b)
    {
        var method = typeof(PageSetupBuilder).GetMethod("Build",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (PageSetup)method.Invoke(b, null)!;
    }
}
