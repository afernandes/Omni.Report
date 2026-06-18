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

/// <summary>
/// Renders a <see cref="MapElement"/> as a Web-Mercator map: an optional vector basemap
/// (GeoJSON polygons/lines and/or a lat-long graticule) drawn behind a marker layer that plots
/// each row's latitude/longitude. The projection preserves aspect ratio (no stretching) and the
/// view is fit to the union of the shapes and the data points.
/// </summary>
/// <remarks>
/// Shapes come from <see cref="MapElement.ShapesGeoJson"/> (inline) or <see cref="MapElement.ShapeSet"/>
/// (a name resolved via <see cref="MapShapeRegistry"/>). Tile basemaps (OpenStreetMap/Bing) are a
/// separate future online layer; this renderer is fully offline and vector, so it works in every
/// exporter (PDF/SVG/PNG/…).
/// </remarks>
internal static class MapRenderer
{
    private static readonly Color Background = Color.FromHex("#EFF6FF"); // blue-50 (water)
    private static readonly Color BorderColor = Color.FromHex("#93C5FD"); // blue-300
    private static readonly Color MarkerColor = Color.FromHex("#1D4ED8"); // blue-700
    private static readonly Color GraticuleColor = Color.FromHex("#C7D2DD");
    private static readonly Color GraticuleText = Color.FromHex("#7C8896");
    private const double InsetMm = 3.0;
    private const double MarkerRadiusMm = 1.1;
    private const double DegToRad = Math.PI / 180.0;
    private const double MaxMercLat = 85.05112878;

    public static IEnumerable<LayoutPrimitive> Render(
        MapElement map,
        Rectangle bounds,
        IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> rows,
        ExpressionEvaluator ev,
        IReportExpressionContext baseCtx)
    {
        var list = new List<LayoutPrimitive>();
        double x0 = bounds.X.ToMm(), y0 = bounds.Y.ToMm(), w = bounds.Width.ToMm(), h = bounds.Height.ToMm();
        if (w <= 1 || h <= 1)
        {
            return list;
        }

        // Water background + frame.
        list.Add(new DrawRectanglePrimitive
        {
            Bounds = bounds,
            Fill = new BrushStyle(Background),
            Pen = new PenStyle(BorderColor, Unit.FromPoint(0.75)),
            SourceElementId = map.Id,
        });

        // ── Gather geometry ──────────────────────────────────────────────────────
        var geoJson = !string.IsNullOrWhiteSpace(map.ShapesGeoJson)
            ? map.ShapesGeoJson
            : MapShapeRegistry.Resolve(map.ShapeSet);
        var shapes = string.IsNullOrWhiteSpace(geoJson) ? new List<GeoShape>() : ParseGeoJson(geoJson!);

        var points = CollectPoints(map, rows, ev, baseCtx);

        // Mercator bounds = union of shapes + points (or the whole world if only a graticule).
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        void Extend(double lon, double lat)
        {
            var (mx, my) = Mercator(lon, lat);
            if (mx < minX) minX = mx;
            if (mx > maxX) maxX = mx;
            if (my < minY) minY = my;
            if (my > maxY) maxY = my;
        }
        foreach (var s in shapes)
        {
            foreach (var (lon, lat) in s.Points) Extend(lon, lat);
        }
        foreach (var (lat, lon) in points) Extend(lon, lat);

        bool hasGeometry = maxX > minX || maxY > minY;
        if (!hasGeometry)
        {
            if (!map.ShowGraticule)
            {
                return list; // nothing to draw beyond the frame
            }
            // Graticule-only: show the whole world.
            (minX, minY) = Mercator(-180, -MaxMercLat);
            (maxX, maxY) = Mercator(180, MaxMercLat);
        }

        // Pad a degenerate span (single point / single meridian) so the fit is stable.
        if (maxX - minX < 1e-9) { minX -= 0.02; maxX += 0.02; }
        if (maxY - minY < 1e-9) { minY -= 0.02; maxY += 0.02; }

        double px0 = x0 + InsetMm, py0 = y0 + InsetMm;
        double pw = Math.Max(0.1, w - 2 * InsetMm), ph = Math.Max(0.1, h - 2 * InsetMm);
        double spanX = maxX - minX, spanY = maxY - minY;
        double scale = Math.Min(pw / spanX, ph / spanY);        // uniform → preserves aspect
        double offX = px0 + (pw - spanX * scale) / 2.0;
        double offY = py0 + (ph - spanY * scale) / 2.0;

        Point Project(double lon, double lat)
        {
            var (mx, my) = Mercator(lon, lat);
            double sx = offX + (mx - minX) * scale;
            double sy = offY + (maxY - my) * scale; // flip Y: north up
            return Pt(sx, sy);
        }

        // ── Graticule (drawn first, behind shapes) ───────────────────────────────
        if (map.ShowGraticule)
        {
            DrawGraticule(list, map.Id, px0, py0, pw, ph, offX, offY, minX, maxX, minY, maxY, scale);
        }

        // ── Shapes ───────────────────────────────────────────────────────────────
        var fill = ParseColor(map.ShapeFill, Color.FromHex("#E8EDE4"));
        var stroke = ParseColor(map.ShapeStroke, Color.FromHex("#9CA3AF"));
        foreach (var s in shapes)
        {
            if (s.Points.Count < 2) continue;
            var pts = new Point[s.Points.Count];
            for (int i = 0; i < s.Points.Count; i++)
            {
                pts[i] = Project(s.Points[i].Lon, s.Points[i].Lat);
            }
            list.Add(new DrawPolygonPrimitive
            {
                Points = new EquatableArray<Point>(pts),
                Closed = s.Filled,
                Fill = s.Filled ? new BrushStyle(fill) : null,
                Pen = new PenStyle(stroke, Unit.FromPoint(s.Filled ? 0.4 : 0.6)),
                Bounds = bounds,
                SourceElementId = map.Id,
            });
        }

        // ── Markers ──────────────────────────────────────────────────────────────
        foreach (var (lat, lon) in points)
        {
            var p = Project(lon, lat);
            double cx = p.X.ToMm(), cy = p.Y.ToMm();
            list.Add(new DrawEllipsePrimitive
            {
                Bounds = Rect(cx - MarkerRadiusMm, cy - MarkerRadiusMm, 2 * MarkerRadiusMm, 2 * MarkerRadiusMm),
                Fill = new BrushStyle(MarkerColor),
                Pen = new PenStyle(Color.White, Unit.FromPoint(0.5)),
                SourceElementId = map.Id,
            });
        }
        return list;
    }

    // ─── Graticule ─────────────────────────────────────────────────────────────────

    private static void DrawGraticule(
        List<LayoutPrimitive> list, string? id,
        double px0, double py0, double pw, double ph,
        double offX, double offY, double minX, double maxX, double minY, double maxY, double scale)
    {
        // Invert the plot-rect edges back to lon/lat to know the visible window.
        double visMinLon = InvLon(minX + (px0 - offX) / scale);
        double visMaxLon = InvLon(minX + (px0 + pw - offX) / scale);
        double visMaxLat = InvLat(maxY - (py0 - offY) / scale);
        double visMinLat = InvLat(maxY - (py0 + ph - offY) / scale);

        double stepLon = NiceStep(Math.Abs(visMaxLon - visMinLon));
        double stepLat = NiceStep(Math.Abs(visMaxLat - visMinLat));
        var pen = new PenStyle(GraticuleColor, Unit.FromPoint(0.4));

        // Meridians (constant longitude → vertical lines).
        for (double lon = Math.Ceiling(visMinLon / stepLon) * stepLon; lon <= visMaxLon; lon += stepLon)
        {
            var (mx, _) = Mercator(lon, 0);
            double sx = offX + (mx - minX) * scale;
            if (sx < px0 - 0.1 || sx > px0 + pw + 0.1) continue;
            list.Add(Line(sx, py0, sx, py0 + ph, pen, id));
            list.Add(Text(FormatLon(lon), sx + 0.6, py0 + ph - 3.2, 14, 3, id));
        }
        // Parallels (constant latitude → horizontal lines).
        for (double lat = Math.Ceiling(visMinLat / stepLat) * stepLat; lat <= visMaxLat; lat += stepLat)
        {
            var (_, my) = Mercator(0, lat);
            double sy = offY + (maxY - my) * scale;
            if (sy < py0 - 0.1 || sy > py0 + ph + 0.1) continue;
            list.Add(Line(px0, sy, px0 + pw, sy, pen, id));
            list.Add(Text(FormatLat(lat), px0 + 0.6, sy - 0.6, 14, 3, id));
        }
    }

    /// <summary>Picks a "nice" degree step (…30/10/5/2/1/0.5…) targeting ~6 divisions.</summary>
    private static double NiceStep(double span)
    {
        if (span <= 0 || double.IsNaN(span)) return 10;
        double raw = span / 6.0;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag; // 1..10
        double nice = norm < 1.5 ? 1 : norm < 3 ? 2 : norm < 7 ? 5 : 10;
        return Math.Max(0.01, nice * mag);
    }

    private static string FormatLon(double lon)
    {
        if (Math.Abs(lon) < 1e-6) return "0°";
        return $"{Math.Abs(lon):0.#}°{(lon < 0 ? "O" : "L")}";
    }

    private static string FormatLat(double lat)
    {
        if (Math.Abs(lat) < 1e-6) return "0°";
        return $"{Math.Abs(lat):0.#}°{(lat < 0 ? "S" : "N")}";
    }

    // ─── Projection (Web Mercator, unit radius) ──────────────────────────────────────

    private static (double X, double Y) Mercator(double lon, double lat)
    {
        double c = Math.Max(-MaxMercLat, Math.Min(MaxMercLat, lat));
        return (lon * DegToRad, Math.Log(Math.Tan(Math.PI / 4 + c * DegToRad / 2)));
    }

    private static double InvLon(double x) => x / DegToRad;
    private static double InvLat(double y) => (2 * Math.Atan(Math.Exp(y)) - Math.PI / 2) / DegToRad;

    // ─── GeoJSON parsing ─────────────────────────────────────────────────────────────

    private sealed record GeoShape(IReadOnlyList<(double Lon, double Lat)> Points, bool Filled);

    private static List<GeoShape> ParseGeoJson(string geoJson)
    {
        var shapes = new List<GeoShape>();
        try
        {
            using var doc = JsonDocument.Parse(geoJson);
            Collect(doc.RootElement, shapes);
        }
        catch (JsonException) { /* malformed → no shapes (markers still render) */ }
        return shapes;
    }

    private static void Collect(JsonElement el, List<GeoShape> shapes)
    {
        if (el.ValueKind != JsonValueKind.Object) return;
        if (!el.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String) return;
        switch (t.GetString())
        {
            case "FeatureCollection":
                if (el.TryGetProperty("features", out var fs) && fs.ValueKind == JsonValueKind.Array)
                    foreach (var f in fs.EnumerateArray()) Collect(f, shapes);
                break;
            case "Feature":
                if (el.TryGetProperty("geometry", out var g)) Collect(g, shapes);
                break;
            case "GeometryCollection":
                if (el.TryGetProperty("geometries", out var gs) && gs.ValueKind == JsonValueKind.Array)
                    foreach (var gg in gs.EnumerateArray()) Collect(gg, shapes);
                break;
            case "Polygon":
                if (el.TryGetProperty("coordinates", out var pc)) AddPolygon(pc, shapes);
                break;
            case "MultiPolygon":
                if (el.TryGetProperty("coordinates", out var mp) && mp.ValueKind == JsonValueKind.Array)
                    foreach (var poly in mp.EnumerateArray()) AddPolygon(poly, shapes);
                break;
            case "LineString":
                if (el.TryGetProperty("coordinates", out var lc)) AddLine(lc, shapes);
                break;
            case "MultiLineString":
                if (el.TryGetProperty("coordinates", out var ml) && ml.ValueKind == JsonValueKind.Array)
                    foreach (var ln in ml.EnumerateArray()) AddLine(ln, shapes);
                break;
        }
    }

    private static void AddPolygon(JsonElement rings, List<GeoShape> shapes)
    {
        if (rings.ValueKind != JsonValueKind.Array) return;
        foreach (var ring in rings.EnumerateArray())
        {
            var pts = ReadPositions(ring);
            if (pts.Count >= 3) shapes.Add(new GeoShape(pts, true));
        }
    }

    private static void AddLine(JsonElement coords, List<GeoShape> shapes)
    {
        var pts = ReadPositions(coords);
        if (pts.Count >= 2) shapes.Add(new GeoShape(pts, false));
    }

    private static List<(double Lon, double Lat)> ReadPositions(JsonElement arr)
    {
        var pts = new List<(double, double)>();
        if (arr.ValueKind != JsonValueKind.Array) return pts;
        foreach (var p in arr.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.Array) continue;
            double lon = 0, lat = 0; int i = 0;
            foreach (var n in p.EnumerateArray())
            {
                if (n.ValueKind == JsonValueKind.Number && n.TryGetDouble(out var v))
                {
                    if (i == 0) lon = v; else if (i == 1) lat = v;
                }
                i++;
            }
            if (i >= 2) pts.Add((lon, lat));
        }
        return pts;
    }

    // ─── Data points ─────────────────────────────────────────────────────────────────

    private static List<(double Lat, double Lon)> CollectPoints(
        MapElement map, IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> rows,
        ExpressionEvaluator ev, IReportExpressionContext baseCtx)
    {
        var points = new List<(double, double)>();
        if (string.IsNullOrEmpty(map.LatitudeExpression) || string.IsNullOrEmpty(map.LongitudeExpression))
        {
            return points;
        }
        foreach (var row in rows)
        {
            var ctx = new RowScopedContext(baseCtx, row);
            double? lat = EvalNullable(ev, map.LatitudeExpression, ctx);
            double? lon = EvalNullable(ev, map.LongitudeExpression, ctx);
            if (lat is not null && lon is not null) points.Add((lat.Value, lon.Value));
        }
        return points;
    }

    private static double? EvalNullable(ExpressionEvaluator ev, string expr, IReportExpressionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expr)) return null;
        object? v;
        try { v = ev.Evaluate(expr, ctx); }
        catch (ExpressionParseException) { return null; }
        return v switch
        {
            null => null,
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            string str => double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : null,
            IConvertible c => TryConvert(c),
            _ => null,
        };
    }

    private static double? TryConvert(IConvertible c)
    {
        try { return c.ToDouble(CultureInfo.InvariantCulture); }
        catch (FormatException) { return null; }
        catch (InvalidCastException) { return null; }
        catch (OverflowException) { return null; }
    }

    // ─── Primitive helpers ─────────────────────────────────────────────────────────────

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return Color.FromHex(hex); }
        catch (FormatException) { return fallback; }
    }

    private static DrawLinePrimitive Line(double x1, double y1, double x2, double y2, PenStyle pen, string? id)
        => new()
        {
            From = Pt(x1, y1),
            To = Pt(x2, y2),
            Pen = pen,
            Bounds = Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)),
            SourceElementId = id,
        };

    private static DrawTextPrimitive Text(string text, double xMm, double yMm, double wMm, double hMm, string? id)
    {
        var style = new TextStyle(new Font("Arial", 6, FontStyle.Regular), GraticuleText,
            HorizontalAlignment.Left, VerticalAlignment.Top, WordWrap: false);
        return new DrawTextPrimitive { Text = text, Bounds = Rect(xMm, yMm, wMm, hMm), Style = style, SourceElementId = id };
    }

    private static Rectangle Rect(double xMm, double yMm, double wMm, double hMm)
        => new(Unit.FromMm(xMm), Unit.FromMm(yMm), Unit.FromMm(Math.Max(0, wMm)), Unit.FromMm(Math.Max(0, hMm)));

    private static Point Pt(double xMm, double yMm) => new(Unit.FromMm(xMm), Unit.FromMm(yMm));
}
