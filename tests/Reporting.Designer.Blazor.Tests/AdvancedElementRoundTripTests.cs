using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Covers the "opaque advanced element" round-trip: Tablix, Gauge, DataBar, Sparkline, Indicator
/// and Map have no dedicated designer editor yet, but loading and re-saving them must NOT degrade
/// them to a TextBox — the designer preserves the full domain element and only updates its bounds.
/// </summary>
public class AdvancedElementRoundTripTests
{
    [Fact]
    public void Gauge_round_trips_losslessly_and_stays_movable()
    {
        var gauge = new GaugeElement
        {
            Id = "g1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(40)),
            Kind = GaugeKind.Radial,
            ValueExpression = "Sum(Fields.total)",
            MinimumExpression = "0",
            MaximumExpression = "1000",
            Ranges = new EquatableArray<GaugeRange>([new GaugeRange("0", "500", "#EF4444")]),
        };

        var vm = ElementViewModel.FromElement(gauge);
        vm.Kind.Should().Be(DesignerElementKind.Gauge);

        vm.X = Unit.FromMm(10); // move it in the designer

        var back = vm.ToElement();
        back.Should().BeOfType<GaugeElement>();
        var g = (GaugeElement)back;
        g.ValueExpression.Should().Be("Sum(Fields.total)");
        g.MaximumExpression.Should().Be("1000");
        g.Ranges.Should().HaveCount(1);
        g.Bounds.X.Should().Be(Unit.FromMm(10));
    }

    [Fact]
    public void Tablix_is_not_degraded_to_a_textbox_on_round_trip()
    {
        var tablix = new TablixElement
        {
            Id = "t1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(120), Unit.FromMm(30)),
            DataSetName = "Vendas",
            Cells = new EquatableArray<TablixCell>(
            [
                new TablixCell(0, 0, new LabelElement { Text = "Produto", Bounds = Rectangle.Empty }),
                new TablixCell(1, 0, new TextBoxElement { Expression = "Fields.nome", Bounds = Rectangle.Empty }),
            ]),
        };

        var back = ElementViewModel.FromElement(tablix).ToElement();

        back.Should().BeOfType<TablixElement>();
        var t = (TablixElement)back;
        t.DataSetName.Should().Be("Vendas");
        t.Cells.Should().HaveCount(2);
    }

    [Fact]
    public void Freshly_added_advanced_element_materialises_to_its_own_type()
    {
        // No source element (added from the toolbox) → ToElement must still produce the right type.
        var vm = new ElementViewModel(DesignerElementKind.Map, "m1")
        {
            Width = Unit.FromMm(80),
            Height = Unit.FromMm(60),
        };

        vm.ToElement().Should().BeOfType<MapElement>();
    }
}
