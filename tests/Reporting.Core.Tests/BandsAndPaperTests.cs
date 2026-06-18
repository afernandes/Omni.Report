using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Styling;
using Xunit;

namespace Reporting.Core.Tests;

public class BandsAndPaperTests
{
    [Fact]
    public void ReportBand_empty_factory()
    {
        var b = ReportBand.Empty(BandKind.PageHeader);
        b.Kind.Should().Be(BandKind.PageHeader);
        b.Height.Should().Be(Unit.Zero);
        b.Elements.Count.Should().Be(0);
    }

    [Fact]
    public void ReportBand_print_on_first_last_defaults_true()
    {
        var b = ReportBand.Empty(BandKind.PageFooter);
        b.PrintOnFirstPage.Should().BeTrue();
        b.PrintOnLastPage.Should().BeTrue();
    }

    [Fact]
    public void DetailBand_supports_grow_and_shrink()
    {
        var d = new DetailBand(6.Mm(), EquatableArray<ReportElement>.Empty, CanGrow: true, CanShrink: true);
        d.Kind.Should().Be(BandKind.Detail);
        d.CanGrow.Should().BeTrue();
        d.CanShrink.Should().BeTrue();
    }

    [Fact]
    public void GroupBand_height_sums_header_and_footer()
    {
        var header = new ReportBand(BandKind.GroupHeader, 10.Mm(), EquatableArray<ReportElement>.Empty);
        var footer = new ReportBand(BandKind.GroupFooter, 6.Mm(), EquatableArray<ReportElement>.Empty);
        var group = new GroupBand("Cliente", "Fields.Cliente", header, footer, KeepTogether: true);
        group.Height.ToMm().Should().BeApproximately(16, 0.1);
        group.KeepTogether.Should().BeTrue();
        group.Elements.Count.Should().Be(0);
        group.Kind.Should().Be(BandKind.GroupHeader);
    }

    [Fact]
    public void GroupBand_with_null_header_footer_has_zero_height()
    {
        var group = new GroupBand("X", "Fields.X");
        group.Height.Should().Be(Unit.Zero);
        group.Elements.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(Orientation.Portrait, 210, 297)]
    [InlineData(Orientation.Landscape, 297, 210)]
    public void PageSetup_orientation_swaps_dimensions(Orientation orientation, double w, double h)
    {
        var setup = new PageSetup(PaperSize.A4, orientation);
        setup.PageWidth.ToMm().Should().BeApproximately(w, 0.05);
        setup.PageHeight.ToMm().Should().BeApproximately(h, 0.05);
    }

    [Fact]
    public void PageSetup_content_size_subtracts_margins()
    {
        var setup = new PageSetup(PaperSize.A4, Margins: Thickness.Uniform(20.Mm()));
        setup.ContentWidth.ToMm().Should().BeApproximately(170, 0.05);
        setup.ContentHeight.ToMm().Should().BeApproximately(257, 0.05);
    }

    [Theory]
    [InlineData("A4", 210, 297)]
    [InlineData("A5", 148, 210)]
    [InlineData("Thermal58", 58, 0)]
    [InlineData("Thermal80", 80, 0)]
    public void Paper_presets_have_correct_dimensions(string name, double w, double h)
    {
        var paper = name switch
        {
            "A4" => PaperSize.A4,
            "A5" => PaperSize.A5,
            "Thermal58" => PaperSize.Thermal58,
            "Thermal80" => PaperSize.Thermal80,
            _ => throw new ArgumentException(name),
        };
        paper.Width.ToMm().Should().BeApproximately(w, 0.05);
        paper.Height.ToMm().Should().BeApproximately(h, 0.05);
        paper.Name.Should().Be(name);
    }

    [Fact]
    public void Letter_and_legal_use_inches()
    {
        PaperSize.Letter.Height.ToInches().Should().BeApproximately(11, 0.001);
        PaperSize.Legal.Height.ToInches().Should().BeApproximately(14, 0.001);
    }

    [Fact]
    public void Paper_rotated_swaps_dimensions()
    {
        var rotated = PaperSize.A4.Rotated();
        rotated.Width.Should().Be(PaperSize.A4.Height);
        rotated.Height.Should().Be(PaperSize.A4.Width);
    }
}

public class ParametersAndDataTests
{
    [Fact]
    public void ReportParameter_stores_metadata()
    {
        var p = new ReportParameter("DataInicio", typeof(DateTime), Prompt: "Data inicial");
        p.Name.Should().Be("DataInicio");
        p.ValueType.Should().Be(typeof(DateTime));
        p.Prompt.Should().Be("Data inicial");
        p.AllowMultiple.Should().BeFalse();
        p.Required.Should().BeTrue();
    }

    [Theory]
    [InlineData(VariableScope.Row)]
    [InlineData(VariableScope.Report)]
    [InlineData(VariableScope.Group)]
    public void Variable_scope_is_assignable(VariableScope scope)
    {
        var v = new ReportVariable("Acc", "Sum(Fields.X)", scope);
        v.Scope.Should().Be(scope);
        v.Expression.Should().Be("Sum(Fields.X)");
    }

    [Fact]
    public void Aggregate_scope_enum_has_expected_members()
    {
        Enum.GetNames<Reporting.Aggregates.AggregateScope>()
            .Should().Contain(["Report", "Page", "Group", "Running"]);
    }
}
