using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Map</c> — geographic visualisation. Renders a Web-Mercator view with an optional
/// vector basemap (GeoJSON shapes and/or a lat/long graticule) and a marker layer plotting each
/// row's latitude/longitude. Online tile basemaps (OpenStreetMap) are an opt-in layer via a tile resolver.
/// </summary>
public sealed record MapElement : ReportElement
{
    /// <summary>Tile / basemap provider (e.g. "OpenStreetMap", "None"). Drives the online tile layer
    /// when a tile resolver is configured; the offline vector basemap is driven by the fields below.</summary>
    [PropertyGrid(Category = "Mapa", Order = 1, Label = "Basemap (tiles)", Placeholder = "OpenStreetMap / (nenhum)")]
    public string? Basemap { get; init; }

    /// <summary>Optional data source providing geo-coded points / polygons.</summary>
    [PropertyGrid(Category = "Mapa", Order = 2, Label = "Fonte", Placeholder = "(fonte primária)")]
    public string? DataSetName { get; init; }

    /// <summary>Expression resolving to the latitude of each row's point.</summary>
    [PropertyGrid(Category = "Mapa", Order = 3, Label = "Latitude", Placeholder = "Fields.lat")]
    public string? LatitudeExpression { get; init; }

    /// <summary>Expression resolving to the longitude of each row's point.</summary>
    [PropertyGrid(Category = "Mapa", Order = 4, Label = "Longitude", Placeholder = "Fields.lon")]
    public string? LongitudeExpression { get; init; }

    // ── Vector basemap (offline) ──────────────────────────────────────────────────

    /// <summary>Inline GeoJSON (FeatureCollection / Feature / Geometry) drawn as the vector
    /// basemap — Polygon/MultiPolygon are filled; LineString/MultiLineString are stroked. Takes
    /// precedence over <see cref="ShapeSet"/> when both are set.</summary>
    [PropertyGrid(Category = "Mapa", Order = 6, Label = "GeoJSON inline", Editor = "textarea")]
    public string? ShapesGeoJson { get; init; }

    /// <summary>Name of a built-in shape set resolved at render time via the map shape registry
    /// (e.g. "brazil", "south-america"). Lets a report reference bundled shapes without inlining
    /// the GeoJSON. Ignored when <see cref="ShapesGeoJson"/> is provided.</summary>
    [PropertyGrid(Category = "Mapa", Order = 5, Label = "Conjunto de shapes", Placeholder = "brazil, south-america")]
    public string? ShapeSet { get; init; }

    /// <summary>Draws a latitude/longitude graticule (grid + degree ticks) behind the data so the
    /// plot reads as a map even without shapes. Default <c>false</c>.</summary>
    [PropertyGrid(Category = "Mapa", Order = 7, Label = "Graticule")]
    public bool ShowGraticule { get; init; }

    /// <summary>Fill colour (hex) for shape polygons — the "land" colour.</summary>
    [PropertyGrid(Category = "Mapa", Order = 8, Label = "Preenchimento", Editor = "color-hex")]
    public string ShapeFill { get; init; } = "#E8EDE4";

    /// <summary>Stroke colour (hex) for shape outlines / graticule lines.</summary>
    [PropertyGrid(Category = "Mapa", Order = 9, Label = "Traço", Editor = "color-hex")]
    public string ShapeStroke { get; init; } = "#9CA3AF";
}
