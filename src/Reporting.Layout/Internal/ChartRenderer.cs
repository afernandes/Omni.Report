using System.Globalization;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

/// <summary>
/// Vector renderer for <see cref="ChartElement"/>. Iterates the chart's bound rows, evaluates
/// each series' category/value expressions, and emits pure <see cref="LayoutPrimitive"/>s
/// (rectangles for bars, polylines for line series, filled polygons approximating arcs for pie
/// slices, plus axes, gridlines, labels, title and legend). Output is resolution-independent —
/// every backend (Skia, GDI, PDF, SVG) draws it without a chart-specific code path.
/// </summary>
/// <remarks>
/// Categories are aggregated (summed) across rows that share a category value, so a chart bound
/// to raw detail rows (e.g. category = month, value = total) behaves like an implicit group-by —
/// the natural expectation for a summary chart. Rows are read once and evaluated per series.
/// </remarks>
internal static class ChartRenderer
{
    // Categorical palette used when a series declares no explicit colour. Tuned to read well on
    // white paper and stay distinguishable in grayscale print.
    private static readonly Color[] Palette =
    [
        Color.FromHex("#C2410C"), // orange-700
        Color.FromHex("#1D4ED8"), // blue-700
        Color.FromHex("#15803D"), // green-700
        Color.FromHex("#B45309"), // amber-700
        Color.FromHex("#7C3AED"), // violet-600
        Color.FromHex("#0E7490"), // cyan-700
        Color.FromHex("#BE123C"), // rose-700
        Color.FromHex("#4D7C0F"), // lime-700
    ];

    private static readonly Color AxisColor = Color.FromHex("#9CA3AF");  // gray-400
    private static readonly Color GridColor = Color.FromHex("#E5E7EB");  // gray-200
    private static readonly Color LabelColor = Color.FromHex("#374151"); // gray-700
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static IEnumerable<LayoutPrimitive> Render(
        ChartElement chart,
        Rectangle bounds,
        IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> rows,
        ExpressionEvaluator evaluator,
        IReportExpressionContext baseCtx,
        out bool emitted)
    {
        var list = new List<LayoutPrimitive>(64);
        emitted = false;

        double x0 = bounds.X.ToMm(), y0 = bounds.Y.ToMm();
        double w = bounds.Width.ToMm(), h = bounds.Height.ToMm();
        if (w <= 1 || h <= 1)
        {
            return list;
        }

        var data = BuildData(chart, rows, evaluator, baseCtx);
        if (data.Series.Count == 0 || data.Categories.Count == 0)
        {
            return list;
        }

        double titleH = string.IsNullOrEmpty(chart.Title) ? 0 : Math.Min(7, h * 0.16);
        double legendH = chart.ShowLegend ? Math.Min(6, h * 0.14) : 0;

        if (titleH > 0)
        {
            list.Add(Text(chart.Title!, x0, y0, w, titleH, 9, bold: true, HorizontalAlignment.Center, chart.Id));
        }

        double plotX = x0;
        double plotY = y0 + titleH;
        double plotW = w;
        double plotH = h - titleH - legendH;
        if (plotH <= 1)
        {
            return list;
        }

        switch (chart.Kind)
        {
            case ChartKind.Pie:
                RenderPie(chart, data, plotX, plotY, plotW, plotH, list);
                break;
            case ChartKind.Radar:
                RenderRadar(chart, data, plotX, plotY, plotW, plotH, list);
                break;
            case ChartKind.Line:
                RenderCartesian(chart, data, plotX, plotY, plotW, plotH, list, line: true);
                break;
            case ChartKind.Area:
                RenderCartesian(chart, data, plotX, plotY, plotW, plotH, list, line: true, area: true);
                break;
            case ChartKind.Scatter:
                RenderCartesian(chart, data, plotX, plotY, plotW, plotH, list, line: false, markers: true);
                break;
            case ChartKind.Bubble:
                RenderCartesian(chart, data, plotX, plotY, plotW, plotH, list, line: false, bubble: true);
                break;
            case ChartKind.Stock:
                RenderCartesian(chart, data, plotX, plotY, plotW, plotH, list, line: false, stock: true);
                break;
            default:
                RenderCartesian(chart, data, plotX, plotY, plotW, plotH, list, line: false);
                break;
        }

        if (legendH > 0)
        {
            RenderLegend(chart, data, x0, y0 + h - legendH, w, legendH, list);
        }

        emitted = list.Count > 0;
        return list;
    }

    // ─── Data ──────────────────────────────────────────────────────────────────────

    private sealed class SeriesData
    {
        public required string Name { get; init; }
        public required Color Color { get; init; }
        public List<double> Values { get; } = []; // aligned to ChartData.Categories
        public List<double> Sizes { get; } = [];  // bubble radius input (summed per category)
        public List<double> Highs { get; } = [];  // stock high (max per category)
        public List<double> Lows { get; } = [];   // stock low (min per category)
    }

    private sealed class ChartData
    {
        public List<string> Categories { get; } = [];
        public List<SeriesData> Series { get; } = [];
        public double MaxValue { get; set; }
        public double MinValue { get; set; }
    }

    private static ChartData BuildData(
        ChartElement chart,
        IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> rows,
        ExpressionEvaluator evaluator,
        IReportExpressionContext baseCtx)
    {
        var data = new ChartData();
        var catIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        // Per-series raw tuples collected over the rows. Size/High/Low are only meaningful for
        // Bubble/Stock; when the series doesn't set them they default (size 0, high/low = value).
        var raw = new List<(string Cat, double Val, double Size, double High, double Low)>[chart.Series.Count];
        for (int s = 0; s < chart.Series.Count; s++)
        {
            raw[s] = [];
            var series = chart.Series[s];
            bool hasSize = !string.IsNullOrWhiteSpace(series.SizeExpression);
            bool hasHigh = !string.IsNullOrWhiteSpace(series.HighExpression);
            bool hasLow = !string.IsNullOrWhiteSpace(series.LowExpression);
            foreach (var row in rows)
            {
                var ctx = new RowScopedContext(baseCtx, row);
                string cat = EvalString(evaluator, series.CategoryExpression, ctx);
                double val = EvalDouble(evaluator, series.ValueExpression, ctx);
                double size = hasSize ? EvalDouble(evaluator, series.SizeExpression!, ctx) : 0;
                double high = hasHigh ? EvalDouble(evaluator, series.HighExpression!, ctx) : val;
                double low = hasLow ? EvalDouble(evaluator, series.LowExpression!, ctx) : val;
                raw[s].Add((cat, val, size, high, low));
                if (!catIndex.ContainsKey(cat))
                {
                    catIndex[cat] = data.Categories.Count;
                    data.Categories.Add(cat);
                }
            }
        }

        for (int s = 0; s < chart.Series.Count; s++)
        {
            var color = chart.Series[s].Color ?? Palette[s % Palette.Length];
            var sd = new SeriesData { Name = chart.Series[s].Name, Color = color };
            int n = data.Categories.Count;
            var aggVal = new double[n];
            var aggSize = new double[n];
            var aggHigh = new double[n];
            var aggLow = new double[n];
            var seen = new bool[n];
            foreach (var (cat, val, size, high, low) in raw[s])
            {
                int ci = catIndex[cat];
                aggVal[ci] += val;       // value/size aggregate by SUM
                aggSize[ci] += size;
                aggHigh[ci] = seen[ci] ? Math.Max(aggHigh[ci], high) : high; // high = MAX, low = MIN
                aggLow[ci] = seen[ci] ? Math.Min(aggLow[ci], low) : low;
                seen[ci] = true;
            }
            sd.Values.AddRange(aggVal);
            sd.Sizes.AddRange(aggSize);
            sd.Highs.AddRange(aggHigh);
            sd.Lows.AddRange(aggLow);
            data.Series.Add(sd);
        }

        double max = 0, min = 0;
        foreach (var sd in data.Series)
        {
            foreach (var v in sd.Values)
            {
                if (v > max) max = v;
                if (v < min) min = v;
            }
            // Stock range bars extend to the highs/lows, so the axis must reach them.
            foreach (var v in sd.Highs) if (v > max) max = v;
            foreach (var v in sd.Lows) { if (v < min) min = v; if (v > max) max = v; }
        }
        data.MaxValue = max;
        data.MinValue = min;
        return data;
    }

    // ─── Cartesian (bar + line) ──────────────────────────────────────────────────────

    private static void RenderCartesian(
        ChartElement chart, ChartData data,
        double px, double py, double pw, double ph,
        List<LayoutPrimitive> list, bool line, bool area = false, bool markers = false,
        bool bubble = false, bool stock = false)
    {
        const double leftGutter = 12; // mm reserved for y-axis labels
        const double bottomGutter = 6; // mm reserved for x-axis labels
        double ax = px + leftGutter;
        double ay = py;
        double aw = pw - leftGutter - 2;
        double ah = ph - bottomGutter;
        if (aw <= 2 || ah <= 2)
        {
            return;
        }

        double top = NiceCeil(data.MaxValue <= 0 ? 1 : data.MaxValue);
        double baseY = ay + ah; // y for value 0
        double YOf(double v) => baseY - (v / top) * ah;

        var gridPen = new PenStyle(GridColor, Unit.FromPoint(0.5));
        var axisPen = new PenStyle(AxisColor, Unit.FromPoint(0.75));

        const int divs = 4;
        for (int i = 0; i <= divs; i++)
        {
            double v = top * i / divs;
            double y = YOf(v);
            list.Add(Line(ax, y, ax + aw, y, gridPen, chart.Id));
            list.Add(Text(FormatValue(v), px, y - 2.5, leftGutter - 1.5, 5, 6, false, HorizontalAlignment.Right, chart.Id));
        }

        // Axes (drawn after gridlines so they sit on top).
        list.Add(Line(ax, ay, ax, baseY, axisPen, chart.Id));
        list.Add(Line(ax, baseY, ax + aw, baseY, axisPen, chart.Id));

        int cats = data.Categories.Count;
        double slot = aw / cats;

        if (markers)
        {
            // Scatter: an ellipse marker per (category, value) point, no connecting line.
            for (int s = 0; s < data.Series.Count; s++)
            {
                var sd = data.Series[s];
                for (int c = 0; c < cats; c++)
                {
                    double cx = ax + c * slot + slot / 2;
                    double cy = YOf(sd.Values[c]);
                    const double rr = 1.4;
                    list.Add(new DrawEllipsePrimitive
                    {
                        Bounds = Rect(cx - rr, cy - rr, rr * 2, rr * 2),
                        Fill = new BrushStyle(sd.Color),
                        Pen = null,
                        SourceElementId = chart.Id,
                    });
                }
            }
        }
        else if (bubble)
        {
            double maxSize = 0;
            foreach (var sd in data.Series)
            {
                foreach (var sz in sd.Sizes)
                {
                    if (sz > maxSize) maxSize = sz;
                }
            }
            double maxR = Math.Max(1.2, Math.Min(slot * 0.5, ah / 6));
            for (int s = 0; s < data.Series.Count; s++)
            {
                var sd = data.Series[s];
                for (int c = 0; c < cats; c++)
                {
                    double cx = ax + c * slot + slot / 2;
                    double cy = YOf(sd.Values[c]);
                    double rr = maxSize > 0 ? maxR * Math.Sqrt(Math.Max(0, sd.Sizes[c]) / maxSize) : 1.5;
                    if (rr < 1) rr = 1;
                    list.Add(new DrawEllipsePrimitive
                    {
                        Bounds = Rect(cx - rr, cy - rr, rr * 2, rr * 2),
                        Fill = new BrushStyle(sd.Color with { A = 140 }),
                        Pen = new PenStyle(sd.Color, Unit.FromPoint(0.5)),
                        SourceElementId = chart.Id,
                    });
                }
            }
        }
        else if (stock)
        {
            for (int s = 0; s < data.Series.Count; s++)
            {
                var sd = data.Series[s];
                var pen = new PenStyle(sd.Color, Unit.FromPoint(1.25));
                for (int c = 0; c < cats; c++)
                {
                    double cx = ax + c * slot + slot / 2;
                    list.Add(Line(cx, YOf(sd.Highs[c]), cx, YOf(sd.Lows[c]), pen, chart.Id)); // high → low range
                    double yClose = YOf(sd.Values[c]);
                    list.Add(Line(cx, yClose, cx + slot * 0.22, yClose, pen, chart.Id)); // close tick
                }
            }
        }
        else if (!line)
        {
            int sCount = data.Series.Count;
            double groupPad = slot * 0.15;
            double groupW = slot - 2 * groupPad;
            double barW = groupW / sCount;
            for (int c = 0; c < cats; c++)
            {
                double gx = ax + c * slot + groupPad;
                for (int s = 0; s < sCount; s++)
                {
                    double v = data.Series[s].Values[c];
                    double bx = gx + s * barW;
                    double yTop = YOf(Math.Max(0, v));
                    double yBot = YOf(Math.Min(0, v));
                    double bh = Math.Abs(yBot - yTop);
                    if (bh < 0.1) bh = 0.1;
                    list.Add(new DrawRectanglePrimitive
                    {
                        Bounds = Rect(bx, yTop, Math.Max(0.3, barW * 0.92), bh),
                        Fill = new BrushStyle(data.Series[s].Color),
                        Pen = null,
                        SourceElementId = chart.Id,
                    });
                }
            }
        }
        else
        {
            for (int s = 0; s < data.Series.Count; s++)
            {
                var sd = data.Series[s];
                var pts = new Point[cats];
                for (int c = 0; c < cats; c++)
                {
                    double cx = ax + c * slot + slot / 2;
                    pts[c] = Pt(cx, YOf(sd.Values[c]));
                }
                // Area mode fills below the polyline first (translucent), so the stroked line
                // sits on top of its own shaded region.
                if (area && cats > 0)
                {
                    var poly = new Point[cats + 2];
                    for (int c = 0; c < cats; c++) poly[c] = pts[c];
                    poly[cats] = Pt(ax + (cats - 1) * slot + slot / 2, baseY);
                    poly[cats + 1] = Pt(ax + slot / 2, baseY);
                    list.Add(new DrawPolygonPrimitive
                    {
                        Points = new EquatableArray<Point>(poly),
                        Closed = true,
                        Pen = null,
                        Fill = new BrushStyle(sd.Color with { A = 70 }),
                        Bounds = Rect(ax, ay, aw, ah),
                        SourceElementId = chart.Id,
                    });
                }
                list.Add(new DrawPolygonPrimitive
                {
                    Points = new EquatableArray<Point>(pts),
                    Closed = false,
                    Pen = new PenStyle(sd.Color, Unit.FromPoint(1.5)),
                    Fill = null,
                    Bounds = Rect(ax, ay, aw, ah),
                    SourceElementId = chart.Id,
                });
            }
        }

        for (int c = 0; c < cats; c++)
        {
            list.Add(Text(data.Categories[c], ax + c * slot, baseY + 0.5, slot, bottomGutter - 0.5,
                6, false, HorizontalAlignment.Center, chart.Id));
        }
    }

    // ─── Radar (polar) ───────────────────────────────────────────────────────────────

    private static void RenderRadar(
        ChartElement chart, ChartData data,
        double px, double py, double pw, double ph,
        List<LayoutPrimitive> list)
    {
        int cats = data.Categories.Count;
        if (cats < 3)
        {
            // A radar needs at least 3 axes to form a web — fall back to bars for 1–2 categories.
            RenderCartesian(chart, data, px, py, pw, ph, list, line: false);
            return;
        }

        double size = Math.Min(pw, ph);
        double cx = px + pw / 2;
        double cy = py + ph / 2;
        double r = size / 2 * 0.78;
        double top = NiceCeil(data.MaxValue <= 0 ? 1 : data.MaxValue);
        double Angle(int c) => -Math.PI / 2 + 2 * Math.PI * c / cats; // start at 12 o'clock, clockwise

        var gridPen = new PenStyle(GridColor, Unit.FromPoint(0.5));
        var axisPen = new PenStyle(AxisColor, Unit.FromPoint(0.6));

        // Concentric grid rings (polygons through the axis directions).
        const int rings = 4;
        for (int ring = 1; ring <= rings; ring++)
        {
            double rr = r * ring / rings;
            var ringPts = new Point[cats];
            for (int c = 0; c < cats; c++)
            {
                ringPts[c] = Pt(cx + rr * Math.Cos(Angle(c)), cy + rr * Math.Sin(Angle(c)));
            }
            list.Add(new DrawPolygonPrimitive
            {
                Points = new EquatableArray<Point>(ringPts),
                Closed = true,
                Pen = gridPen,
                Fill = null,
                Bounds = Rect(px, py, pw, ph),
                SourceElementId = chart.Id,
            });
        }

        // Spokes + category labels at each axis tip.
        for (int c = 0; c < cats; c++)
        {
            double ex = cx + r * Math.Cos(Angle(c));
            double ey = cy + r * Math.Sin(Angle(c));
            list.Add(Line(cx, cy, ex, ey, axisPen, chart.Id));
            double lx = cx + (r + 3) * Math.Cos(Angle(c)) - 6;
            double ly = cy + (r + 3) * Math.Sin(Angle(c)) - 2;
            list.Add(Text(data.Categories[c], lx, ly, 12, 4, 6, false, HorizontalAlignment.Center, chart.Id));
        }

        // Each series → a closed web at value-scaled radii.
        for (int s = 0; s < data.Series.Count; s++)
        {
            var sd = data.Series[s];
            var pts = new Point[cats];
            for (int c = 0; c < cats; c++)
            {
                double rr = r * (top <= 0 ? 0 : Math.Max(0, sd.Values[c]) / top);
                pts[c] = Pt(cx + rr * Math.Cos(Angle(c)), cy + rr * Math.Sin(Angle(c)));
            }
            list.Add(new DrawPolygonPrimitive
            {
                Points = new EquatableArray<Point>(pts),
                Closed = true,
                Pen = new PenStyle(sd.Color, Unit.FromPoint(1.5)),
                Fill = new BrushStyle(sd.Color with { A = 50 }),
                Bounds = Rect(px, py, pw, ph),
                SourceElementId = chart.Id,
            });
        }
    }

    // ─── Pie ───────────────────────────────────────────────────────────────────────

    private static void RenderPie(
        ChartElement chart, ChartData data,
        double px, double py, double pw, double ph,
        List<LayoutPrimitive> list)
    {
        var sd = data.Series[0]; // pie summarises the first series
        double total = 0;
        foreach (var v in sd.Values) total += Math.Abs(v);
        if (total <= 0)
        {
            return;
        }

        double size = Math.Min(pw, ph);
        double cx = px + pw / 2;
        double cy = py + ph / 2;
        double r = size / 2 * 0.92;

        double startDeg = -90; // start at 12 o'clock
        for (int c = 0; c < data.Categories.Count; c++)
        {
            double frac = Math.Abs(sd.Values[c]) / total;
            if (frac <= 0)
            {
                continue;
            }
            double sweep = frac * 360.0;
            list.Add(Wedge(cx, cy, r, startDeg, sweep, Palette[c % Palette.Length], chart.Id));
            startDeg += sweep;
        }
    }

    private static DrawPolygonPrimitive Wedge(
        double cx, double cy, double r, double startDeg, double sweepDeg, Color color, string? id)
    {
        // Approximate the arc with ~2°-spaced segments — visually smooth at report scale and
        // keeps the primitive a plain point list (no arc primitive needed downstream).
        int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweepDeg) / 2.0));
        var pts = new List<Point>(steps + 2) { Pt(cx, cy) };
        for (int i = 0; i <= steps; i++)
        {
            double a = (startDeg + sweepDeg * i / steps) * Math.PI / 180.0;
            pts.Add(Pt(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
        }
        return new DrawPolygonPrimitive
        {
            Points = new EquatableArray<Point>(pts.ToArray()),
            Closed = true,
            Fill = new BrushStyle(color),
            Pen = new PenStyle(Color.White, Unit.FromPoint(0.75)),
            Bounds = Rect(cx - r, cy - r, 2 * r, 2 * r),
            SourceElementId = id,
        };
    }

    // ─── Legend ──────────────────────────────────────────────────────────────────────

    private static void RenderLegend(
        ChartElement chart, ChartData data,
        double x, double y, double w, double h,
        List<LayoutPrimitive> list)
    {
        // Pie legends list categories; bar/line legends list series.
        var entries = new List<(string Label, Color Color)>();
        if (chart.Kind == ChartKind.Pie)
        {
            for (int c = 0; c < data.Categories.Count; c++)
            {
                entries.Add((data.Categories[c], Palette[c % Palette.Length]));
            }
        }
        else
        {
            foreach (var sd in data.Series)
            {
                entries.Add((sd.Name, sd.Color));
            }
        }
        if (entries.Count == 0)
        {
            return;
        }

        double box = Math.Min(3.5, h * 0.6);
        double itemW = w / entries.Count;
        double yMid = y + (h - box) / 2;
        for (int i = 0; i < entries.Count; i++)
        {
            double ix = x + i * itemW;
            list.Add(new DrawRectanglePrimitive
            {
                Bounds = Rect(ix, yMid, box, box),
                Fill = new BrushStyle(entries[i].Color),
                Pen = null,
                SourceElementId = chart.Id,
            });
            list.Add(Text(entries[i].Label, ix + box + 1.5, y, itemW - box - 1.5, h,
                6, false, HorizontalAlignment.Left, chart.Id));
        }
    }

    // ─── Expression evaluation ────────────────────────────────────────────────────────

    private static string EvalString(ExpressionEvaluator ev, string expr, IReportExpressionContext ctx)
    {
        var v = SafeEval(ev, expr, ctx);
        return v is null ? string.Empty : Convert.ToString(v, ctx.Culture) ?? string.Empty;
    }

    private static double EvalDouble(ExpressionEvaluator ev, string expr, IReportExpressionContext ctx)
        => ToDouble(SafeEval(ev, expr, ctx));

    private static object? SafeEval(ExpressionEvaluator ev, string expr, IReportExpressionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return null;
        }
        try
        {
            return ev.Evaluate(expr, ctx);
        }
        catch (ExpressionParseException)
        {
            // Mirror BandRenderer: an unparseable expression yields no data point rather than
            // crashing the whole report. Genuine runtime errors still propagate.
            return null;
        }
    }

    private static double ToDouble(object? v) => v switch
    {
        null => 0,
        double d => d,
        float f => f,
        decimal m => (double)m,
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        bool bo => bo ? 1 : 0,
        string str => ParseNumber(str),
        IConvertible c => SafeConvertible(c),
        _ => 0,
    };

    private static double ParseNumber(string str)
    {
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
        {
            return p;
        }
        return double.TryParse(str, NumberStyles.Any, PtBr, out p) ? p : 0;
    }

    private static double SafeConvertible(IConvertible c)
    {
        try
        {
            return c.ToDouble(CultureInfo.InvariantCulture);
        }
        catch (FormatException) { return 0; }
        catch (InvalidCastException) { return 0; }
        catch (OverflowException) { return 0; }
    }

    // ─── Geometry / text helpers ──────────────────────────────────────────────────────

    private static double NiceCeil(double v)
    {
        if (v <= 0) return 1;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(v)));
        double n = v / mag;
        double nice = n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10;
        return nice * mag;
    }

    private static string FormatValue(double v)
        => Math.Abs(v) >= 1000 ? v.ToString("#,0", PtBr) : v.ToString("0.##", CultureInfo.InvariantCulture);

    private static DrawTextPrimitive Text(
        string text, double xMm, double yMm, double wMm, double hMm,
        double sizePt, bool bold, HorizontalAlignment align, string? id)
    {
        var font = new Font("Arial", sizePt, bold ? FontStyle.Bold : FontStyle.Regular);
        var style = new TextStyle(font, LabelColor, align, VerticalAlignment.Middle, WordWrap: false);
        return new DrawTextPrimitive
        {
            Text = text ?? string.Empty,
            Bounds = Rect(xMm, yMm, wMm, hMm),
            Style = style,
            SourceElementId = id,
        };
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

    private static Rectangle Rect(double xMm, double yMm, double wMm, double hMm)
        => new(Unit.FromMm(xMm), Unit.FromMm(yMm), Unit.FromMm(Math.Max(0, wMm)), Unit.FromMm(Math.Max(0, hMm)));

    private static Point Pt(double xMm, double yMm) => new(Unit.FromMm(xMm), Unit.FromMm(yMm));
}
