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

    [Fact]
    public void Map_every_field_is_editable_on_a_freshly_added_element()
    {
        // User adds a Map from the toolbox (no _sourceElement) and configures EVERY field in the
        // PropertyGrid — each setter must seed/mutate the domain element so ToElement() carries it.
        // This is the "build + edit any parameter in the designer, not only when loading a file" path.
        var vm = new ElementViewModel(DesignerElementKind.Map, "m1")
        {
            Width = Unit.FromMm(80),
            Height = Unit.FromMm(60),
        };

        vm.MapLatitude = "Fields.lat";
        vm.MapLongitude = "Fields.lon";
        vm.MapDataSet = "Filiais";
        vm.MapShapeSet = "brazil";
        vm.MapShapesGeoJson = "{\"type\":\"FeatureCollection\"}";
        vm.MapGraticule = true;
        vm.MapShapeFill = "#FFEEDD";
        vm.MapShapeStroke = "#112233";
        vm.MapBasemap = "OpenStreetMap";

        var map = vm.ToElement().Should().BeOfType<MapElement>().Subject;
        map.LatitudeExpression.Should().Be("Fields.lat");
        map.LongitudeExpression.Should().Be("Fields.lon");
        map.DataSetName.Should().Be("Filiais");
        map.ShapeSet.Should().Be("brazil");
        map.ShapesGeoJson.Should().Be("{\"type\":\"FeatureCollection\"}");
        map.ShowGraticule.Should().BeTrue();
        map.ShapeFill.Should().Be("#FFEEDD");
        map.ShapeStroke.Should().Be("#112233");
        map.Basemap.Should().Be("OpenStreetMap");
        map.Bounds.Width.Should().Be(Unit.FromMm(80));
    }

    [Fact]
    public void Map_loaded_element_surfaces_all_fields_and_edits_stick()
    {
        var map = new MapElement
        {
            Id = "m2",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(60)),
            LatitudeExpression = "Fields.lat",
            LongitudeExpression = "Fields.lon",
            ShapeSet = "south-america",
            ShowGraticule = true,
            Basemap = "OpenStreetMap",
        };

        var vm = ElementViewModel.FromElement(map);
        // Every field is surfaced for the PropertyGrid (not just preserved opaquely).
        vm.MapShapeSet.Should().Be("south-america");
        vm.MapGraticule.Should().BeTrue();
        vm.MapBasemap.Should().Be("OpenStreetMap");

        // Edit one in the designer, re-emit — the edit sticks and the rest is preserved.
        vm.MapShapeSet = "brazil";
        var back = (MapElement)vm.ToElement();
        back.ShapeSet.Should().Be("brazil");
        back.ShowGraticule.Should().BeTrue();
        back.LatitudeExpression.Should().Be("Fields.lat");
    }
}
