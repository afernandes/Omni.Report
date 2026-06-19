using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed record GeoRow(double Lat, double Lon);

/// <summary>
/// Covers the raster basemap tile layer: when a <see cref="PaginationRequest.MapTileResolver"/> is
/// wired and the <see cref="MapElement.Basemap"/> is set, the engine computes the visible Web-Mercator
/// tile grid and emits one <see cref="DrawImagePrimitive"/> per fetched tile (behind the vector layer).
/// Without a resolver the map renders vector-only — proving the tile layer is real, render-time, and
/// the network fetch is cleanly externalised (the resolver here just returns opaque bytes).
/// </summary>
public class MapTilesTests
{
    private static PaginationRequest MapRequest(System.Func<MapTileRequest, byte[]?>? resolver)
    {
        var map = new MapElement
        {
            Id = "map",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 120.Mm(), 80.Mm()),
            Basemap = "https://tile.example/{z}/{x}/{y}.png",
            LatitudeExpression = "Fields.Lat",
            LongitudeExpression = "Fields.Lon",
            ShowGraticule = true,
        };
        var detail = new DetailBand(Unit.FromMm(80), new EquatableArray<ReportElement>(new ReportElement[] { map }));
        var def = new ReportDefinition("m", PageSetup.A4Portrait, detail);

        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<GeoRow>("Geo", [new GeoRow(-23.55, -46.63)]));
        return new PaginationRequest { Definition = def, DataSources = registry, MapTileResolver = resolver };
    }

    [Fact]
    public async Task Map_basemap_with_tile_resolver_emits_tile_images()
    {
        int calls = 0;
        var req = MapRequest(_ => { calls++; return new byte[] { 1, 2, 3, 4 }; });

        var report = await new ReportPaginator().PaginateAsync(req);
        var tiles = report.Pages[0].Primitives.OfType<DrawImagePrimitive>().ToList();

        calls.Should().BeGreaterThan(0, "the engine must request basemap tiles for the viewport");
        tiles.Should().NotBeEmpty("each fetched tile becomes a DrawImagePrimitive behind the map");
        tiles.Should().OnlyContain(t => t.Data.Count == 4);
    }

    [Fact]
    public async Task Map_without_resolver_renders_vector_only()
    {
        var report = await new ReportPaginator().PaginateAsync(MapRequest(resolver: null));
        report.Pages[0].Primitives.OfType<DrawImagePrimitive>().Should().BeEmpty(
            "no tile resolver → no raster basemap, just the vector graticule/shapes/markers");
    }
}
