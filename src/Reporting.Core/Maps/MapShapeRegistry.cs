using System.Collections.Concurrent;

namespace Reporting.Maps;

/// <summary>
/// Process-wide registry of named GeoJSON shape sets used as a Map element's vector basemap.
/// The optional <c>Reporting.Maps</c> package registers bundled sets (e.g. <c>"brazil"</c>) via
/// <c>MapShapes.RegisterBuiltIns()</c>; hosts may register their own higher-resolution GeoJSON.
/// The layout engine resolves <see cref="Elements.MapElement.ShapeSet"/> against this registry.
/// Registration is idempotent and thread-safe.
/// </summary>
public static class MapShapeRegistry
{
    private static readonly ConcurrentDictionary<string, string> _shapes =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers (or replaces) the GeoJSON for a shape-set <paramref name="name"/>.</summary>
    public static void Register(string name, string geoJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(geoJson);
        _shapes[name] = geoJson;
    }

    /// <summary>Returns the GeoJSON registered for <paramref name="name"/>, or null when absent.</summary>
    public static string? Resolve(string? name)
        => !string.IsNullOrWhiteSpace(name) && _shapes.TryGetValue(name!, out var g) ? g : null;

    /// <summary>True when a shape set is registered under <paramref name="name"/>.</summary>
    public static bool Contains(string? name)
        => !string.IsNullOrWhiteSpace(name) && _shapes.ContainsKey(name!);

    /// <summary>The registered shape-set names (for discovery / designer UI).</summary>
    public static IReadOnlyCollection<string> Names => _shapes.Keys.ToArray();
}
