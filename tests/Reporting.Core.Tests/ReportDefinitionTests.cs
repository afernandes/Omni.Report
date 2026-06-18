using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Core.Tests;

public class ReportDefinitionTests
{
    [Fact]
    public void Empty_definition_is_valid()
    {
        var def = ReportDefinition.Empty("blank");
        def.Name.Should().Be("blank");
        def.PageSetup.Paper.Should().Be(PaperSize.A4);
        def.Detail.Elements.Count.Should().Be(0);
    }

    [Fact]
    public void Two_structurally_identical_definitions_are_equal()
    {
        var a = BuildSimpleDefinition();
        var b = BuildSimpleDefinition();
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Mutated_definition_differs_from_original()
    {
        var a = BuildSimpleDefinition();
        var b = a with { Name = "differentName" };
        a.Should().NotBe(b);
    }

    [Fact]
    public void Element_id_persists_through_with_expression()
    {
        var tb = new TextBoxElement
        {
            Id = "id-123",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()),
            Expression = "Fields.Total",
        };
        tb.Id.Should().Be("id-123");
        var updated = tb with { Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 6.Mm()) };
        updated.Id.Should().Be("id-123");
    }

    [Fact]
    public void Equatable_array_preserves_structural_equality()
    {
        var a = new EquatableArray<int>([1, 2, 3]);
        var b = new EquatableArray<int>([1, 2, 3]);
        var c = new EquatableArray<int>([1, 2, 4]);

        a.Equals(b).Should().BeTrue();
        a.Equals(c).Should().BeFalse();
        a.GetHashCode().Should().Be(b.GetHashCode());
        (a == b).Should().BeTrue();
        (a == c).Should().BeFalse();
    }

    [Fact]
    public void PageSetup_continuous_paper_has_zero_height()
    {
        PaperSize.Thermal80.Height.Should().Be(Unit.Zero);
        var setup = new PageSetup(PaperSize.Thermal80);
        setup.IsContinuous.Should().BeTrue();
    }

    [Fact]
    public void Landscape_orientation_swaps_dimensions()
    {
        var setup = new PageSetup(PaperSize.A4, Orientation.Landscape);
        setup.PageWidth.Should().Be(Unit.FromMm(297));
        setup.PageHeight.Should().Be(Unit.FromMm(210));
    }

    [Fact]
    public void Border_uniform_produces_identical_sides()
    {
        var b = Border.Uniform(BorderLineStyle.Solid, Unit.FromPoint(1), Color.Black);
        b.Left.Should().Be(b.Right);
        b.Left.Should().Be(b.Top);
        b.Left.Should().Be(b.Bottom);
    }

    private static ReportDefinition BuildSimpleDefinition()
    {
        var label = new LabelElement
        {
            Id = "lbl-title",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 8.Mm()),
            Text = "Header",
        };
        var detail = new DetailBand(
            6.Mm(),
            new EquatableArray<ReportElement>(new ReportElement[]
            {
                new TextBoxElement
                {
                    Id = "tb-total",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 30.Mm(), 6.Mm()),
                    Expression = "Fields.Total",
                },
            }));
        var header = new ReportBand(
            BandKind.ReportHeader,
            10.Mm(),
            new EquatableArray<ReportElement>(new ReportElement[] { label }));
        return ReportDefinition.Empty("VendasReport") with
        {
            ReportHeader = header,
            Detail = detail,
        };
    }
}
