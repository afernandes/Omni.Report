using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Covers the property editors for the opaque advanced elements (DataBar, Sparkline, Map): the
/// PropertyGrid renders a section per kind, and the bound helper properties flow edits into the
/// preserved domain element so they survive <see cref="ElementViewModel.ToElement"/>.
/// </summary>
/// <remarks>
/// Render and mutation are exercised separately: mutating a view model that a live component is
/// subscribed to would raise <c>StateHasChanged</c> off the Blazor dispatcher. In the app, edits
/// arrive through <c>@onchange</c> (already on the dispatcher); here the round-trip is checked on
/// a view model with no rendered subscriber.
/// </remarks>
public class OpaqueEditorTests : Bunit.BunitContext
{
    public OpaqueEditorTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void DataBar_editor_section_renders()
    {
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.DataBar, "d1")));
        cut.Markup.Should().Contain("Barra de dados");
    }

    [Fact]
    public void DataBar_edits_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.DataBar, "d1");
        vm.DataBarValue = "Fields.pct";
        vm.DataBarMax = "200";

        var el = (DataBarElement)vm.ToElement();
        el.ValueExpression.Should().Be("Fields.pct");
        el.MaximumExpression.Should().Be("200");
    }

    [Fact]
    public void Sparkline_editor_section_renders()
    {
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Sparkline, "s1")));
        cut.Markup.Should().Contain("Mini-gráfico");
    }

    [Fact]
    public void Sparkline_edits_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.Sparkline, "s1");
        vm.SparkKind = SparklineKind.Column;
        vm.SparkValue = "Fields.v";

        var el = (SparklineElement)vm.ToElement();
        el.Kind.Should().Be(SparklineKind.Column);
        el.ValueExpression.Should().Be("Fields.v");
    }

    [Fact]
    public void Map_editor_section_renders()
    {
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Map, "m1")));
        cut.Markup.Should().Contain("Mapa");
    }

    [Fact]
    public void Map_edits_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.Map, "m1");
        vm.MapLatitude = "Fields.lat";
        vm.MapLongitude = "Fields.lon";

        var el = (MapElement)vm.ToElement();
        el.LatitudeExpression.Should().Be("Fields.lat");
        el.LongitudeExpression.Should().Be("Fields.lon");
    }

    [Fact]
    public void Gauge_editor_section_renders()
    {
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Gauge, "g1")));
        cut.Markup.Should().Contain("Medidor");
    }

    [Fact]
    public void Gauge_scalars_and_ranges_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.Gauge, "g1");
        vm.GaugeType = GaugeKind.Linear;
        vm.GaugeValue = "Sum(Fields.t)";
        vm.GaugeMax = "500";
        vm.AddGaugeRange();
        vm.SetGaugeRange(0, start: "0", end: "250", color: "#EF4444");

        var el = (GaugeElement)vm.ToElement();
        el.Kind.Should().Be(GaugeKind.Linear);
        el.ValueExpression.Should().Be("Sum(Fields.t)");
        el.MaximumExpression.Should().Be("500");
        el.Ranges.Should().HaveCount(1);
        el.Ranges[0].EndExpression.Should().Be("250");
        el.Ranges[0].ColorHex.Should().Be("#EF4444");
    }

    [Fact]
    public void Indicator_editor_section_renders()
    {
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Indicator, "i1")));
        cut.Markup.Should().Contain("Indicador");
    }

    [Fact]
    public void Indicator_scalars_and_states_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.Indicator, "i1");
        vm.IndicatorType = IndicatorKind.Shape;
        vm.IndicatorValue = "Fields.kpi";
        vm.AddIndicatorState();
        vm.SetIndicatorState(0, start: "0", end: "100", icon: "star");

        var el = (IndicatorElement)vm.ToElement();
        el.Kind.Should().Be(IndicatorKind.Shape);
        el.ValueExpression.Should().Be("Fields.kpi");
        el.States.Should().HaveCount(1);
        el.States[0].IconName.Should().Be("star");
    }

    [Fact]
    public void Tablix_editor_section_renders()
    {
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Tablix, "t1")));
        cut.Markup.Should().Contain("Tabela");
    }

    [Fact]
    public void Tablix_columns_round_trip()
    {
        var vm = new ElementViewModel(DesignerElementKind.Tablix, "t1");
        vm.TablixDataSet = "Vendas";
        vm.AddTablixColumn();
        vm.SetTablixColumn(0, header: "Produto", expression: "Fields.nome");
        vm.AddTablixColumn();
        vm.SetTablixColumn(1, header: "Preço", expression: "{Fields.preco:C}");

        var el = (TablixElement)vm.ToElement();
        el.DataSetName.Should().Be("Vendas");
        el.Cells.Should().HaveCount(4); // 2 columns × (header + detail)
        el.Cells.Count(c => c.RowIndex == 0).Should().Be(2);

        vm.TablixColumns.Should().HaveCount(2);
        vm.TablixColumns[1].Header.Should().Be("Preço");
    }

    [Fact]
    public void Rectangle_exposes_a_fill_colour_editor()
    {
        // Regression: the only fill-colour editor lived in the "Appearance" section, which is gated to
        // text elements (HasTextContent), so a rectangle had no way to set its background in the designer.
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Rectangle, "r1")));
        cut.Markup.Should().Contain("Forma");
        cut.Markup.Should().Contain("Preenchimento", "a rectangle's fill colour must be editable in the designer");
    }

    [Fact]
    public void Ellipse_exposes_a_fill_colour_editor()
    {
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Ellipse, "e1")));
        cut.Markup.Should().Contain("Preenchimento");
    }

    [Fact]
    public void Rectangle_fill_colour_round_trips()
    {
        var vm = new ElementViewModel(DesignerElementKind.Rectangle, "r1")
        {
            FillColor = Reporting.Styling.Color.FromHex("#FF8800"),
        };
        var rect = (RectangleElement)vm.ToElement();
        rect.FillColor.Should().NotBeNull();
        ElementViewModel.FromElement(rect).FillColor.Should().Be(vm.FillColor);
    }
}
