using Reporting.DataSources;
using Reporting.Rendering;

namespace Reporting.Layout;

/// <summary>A single Web-Mercator tile the engine needs to draw a <see cref="Reporting.Elements.MapElement"/>
/// basemap. <see cref="Basemap"/> is the element's configured provider/URL-template (e.g.
/// <c>"https://tile.openstreetmap.org/{z}/{x}/{y}.png"</c>); <see cref="Z"/>/<see cref="X"/>/<see cref="Y"/>
/// are the standard slippy-map tile coordinates. The resolver turns this into image bytes.</summary>
public readonly record struct MapTileRequest(string Basemap, int Z, int X, int Y);
