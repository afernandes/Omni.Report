using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Maps;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal static partial class MapRenderer
{

    // ─── GeoJSON parsing ─────────────────────────────────────────────────────────────

    private sealed record GeoShape(IReadOnlyList<(double Lon, double Lat)> Points, bool Filled);
}
