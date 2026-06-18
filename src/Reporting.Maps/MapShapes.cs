namespace Reporting.Maps;

/// <summary>
/// Entry point for the bundled vector shape sets. Call <see cref="RegisterBuiltIns"/> once at
/// startup so <c>MapElement.ShapeSet = "brazil"</c> (etc.) resolves to its GeoJSON via
/// <see cref="MapShapeRegistry"/>.
/// </summary>
/// <remarks>
/// The bundled outlines are deliberately <b>simplified / low-resolution</b> — enough to give a
/// recognisable basemap out of the box. For production-grade borders, register your own detailed
/// GeoJSON: <c>MapShapeRegistry.Register("brazil", File.ReadAllText("brazil.geojson"))</c>.
/// </remarks>
public static class MapShapes
{
    /// <summary>Registered name of the bundled simplified Brazil national outline.</summary>
    public const string Brazil = "brazil";

    /// <summary>Registers every bundled shape set into <see cref="MapShapeRegistry"/>. Idempotent.</summary>
    public static void RegisterBuiltIns()
    {
        MapShapeRegistry.Register(Brazil, BuiltInShapes.BrazilGeoJson);
    }
}
