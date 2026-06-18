namespace Reporting.Maps;

/// <summary>
/// Bundled GeoJSON shape data. These are <b>simplified, low-resolution</b> outlines meant as a
/// usable default — not survey-grade borders. Replace with detailed GeoJSON via
/// <see cref="MapShapeRegistry.Register(string, string)"/> when accuracy matters.
/// </summary>
internal static class BuiltInShapes
{
    /// <summary>A coarse Brazil national outline (~34 vertices, clockwise) as a GeoJSON
    /// FeatureCollection with one Polygon. Coordinates are <c>[longitude, latitude]</c>.</summary>
    public const string BrazilGeoJson =
        "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{\"name\":\"Brasil\"}," +
        "\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[" +
        "[-60.7,5.0],[-51.2,4.1],[-50.0,0.0],[-48.5,-1.5],[-44.3,-2.5],[-41.8,-2.9],[-38.5,-3.7]," +
        "[-35.2,-5.8],[-34.8,-7.2],[-35.0,-9.7],[-37.0,-11.0],[-38.5,-13.0],[-39.0,-17.9],[-40.3,-20.3]," +
        "[-43.2,-23.0],[-46.4,-24.0],[-48.5,-25.5],[-48.6,-28.5],[-50.0,-30.0],[-52.2,-32.0],[-53.4,-33.7]," +
        "[-57.6,-30.2],[-56.0,-27.4],[-54.6,-25.6],[-57.9,-22.1],[-58.2,-19.8],[-60.0,-15.0],[-65.3,-11.0]," +
        "[-70.0,-11.0],[-73.99,-7.5],[-70.0,-4.2],[-67.3,1.2],[-64.0,2.5],[-60.7,5.0]" +
        "]]}}]}";
}
