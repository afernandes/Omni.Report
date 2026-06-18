using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record Cidade(string Nome, double Lat, double Lon);

/// <summary>
/// Covers the code-first map surface: the fluent <see cref="BandContent.Map"/> building the
/// <see cref="MapElement"/> model, and the paginator plotting one marker per data row.
/// </summary>
public class MapCodeFirstTests
{
    private static readonly Cidade[] Rows =
    [
        new("São Paulo", -23.55, -46.63),
        new("Rio de Janeiro", -22.91, -43.20),
        new("Belo Horizonte", -19.92, -43.94),
    ];

    [Fact]
    public void Map_fluent_builds_element_with_lat_long_and_dataset()
    {
        var def = ReportBuilder.Create("m")
            .DataSource("Cidades", Rows)
            .ReportHeader(h => h.Height(80)
                .Map("Fields.Lat", "Fields.Lon", "Cidades").At(0, 0).Size(120, 70))
            .Build().Definition;

        var map = def.ReportHeader!.Elements.OfType<MapElement>().Single();
        map.LatitudeExpression.Should().Be("Fields.Lat");
        map.LongitudeExpression.Should().Be("Fields.Lon");
        map.DataSetName.Should().Be("Cidades");
    }

    [Fact]
    public async Task Map_renders_one_marker_per_row_over_a_background()
    {
        var report = ReportBuilder.Create("Mapa")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Cidades", Rows)
            .ReportHeader(h => h.Height(80)
                .Map("Fields.Lat", "Fields.Lon").At(0, 0).Size(120, 70))
            .Build();

        var prims = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();

        prims.OfType<DrawEllipsePrimitive>().Count().Should().Be(3, "one marker per city");
        prims.OfType<DrawRectanglePrimitive>().Should().NotBeEmpty("the map draws a background panel");
    }

    [Fact]
    public void Map_fluent_sets_shapes_graticule_and_colors()
    {
        var def = ReportBuilder.Create("m")
            .DataSource("Cidades", Rows)
            .ReportHeader(h => h.Height(80)
                .Map("Fields.Lat", "Fields.Lon", "Cidades")
                    .Shapes("{\"type\":\"Polygon\",\"coordinates\":[[[-50,-25],[-40,-25],[-45,-15],[-50,-25]]]}")
                    .Graticule()
                    .ShapeColors("#EEEEEE", "#888888")
                    .At(0, 0).Size(120, 70))
            .Build().Definition;

        var map = def.ReportHeader!.Elements.OfType<MapElement>().Single();
        map.ShapesGeoJson.Should().Contain("Polygon");
        map.ShowGraticule.Should().BeTrue();
        map.ShapeFill.Should().Be("#EEEEEE");
        map.ShapeStroke.Should().Be("#888888");
    }

    [Fact]
    public async Task Map_with_geojson_shapes_and_graticule_draws_polygons_and_grid()
    {
        var report = ReportBuilder.Create("Mapa")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Cidades", Rows)
            .ReportHeader(h => h.Height(80)
                .Map("Fields.Lat", "Fields.Lon")
                    .Shapes("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{}," +
                            "\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[-55,-35],[-34,-35],[-34,5],[-55,5],[-55,-35]]]}}]}")
                    .Graticule()
                    .At(0, 0).Size(120, 70))
            .Build();

        var prims = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();

        prims.OfType<DrawPolygonPrimitive>().Should().NotBeEmpty("the GeoJSON shape renders as a filled polygon");
        prims.OfType<DrawLinePrimitive>().Should().NotBeEmpty("the graticule draws grid lines");
        prims.OfType<DrawEllipsePrimitive>().Count().Should().Be(3, "markers still plot on top");
    }

    [Fact]
    public async Task Map_shape_set_resolves_from_registry()
    {
        Reporting.Maps.MapShapeRegistry.Register("test-rect",
            "{\"type\":\"Polygon\",\"coordinates\":[[[-55,-35],[-34,-35],[-34,5],[-55,5],[-55,-35]]]}");

        var report = ReportBuilder.Create("Mapa")
            .DataSource("Cidades", Rows)
            .ReportHeader(h => h.Height(80)
                .Map("Fields.Lat", "Fields.Lon").ShapeSet("test-rect").At(0, 0).Size(120, 70))
            .Build();

        var prims = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
        prims.OfType<DrawPolygonPrimitive>().Should().NotBeEmpty("the named shape set resolves from the registry and renders");
    }
}
