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
/// Vector renderers for the single-value KPI elements — <see cref="GaugeElement"/> and
/// <see cref="DataBarElement"/>. Unlike charts they don't iterate rows: their value/min/max
/// expressions are evaluated once against the band's live context (so a gauge in a group/report
/// footer naturally shows that scope's aggregate, e.g. <c>Sum(Fields.Total)</c>).
/// </summary>
internal static class KpiRenderer
{
    private static readonly Color TrackColor = Color.FromHex("#E5E7EB"); // gray-200
    private static readonly Color LabelColor = Color.FromHex("#374151"); // gray-700
    private static readonly Color NeedleColor = Color.FromHex("#111827"); // gray-900
    private static readonly Color DefaultBar = Color.FromHex("#C2410C"); // orange-700

    // ─── DataBar ───────────────────────────────────────────────────────────────────

    public static IEnumerable<LayoutPrimitive> RenderDataBar(
        DataBarElement el, Rectangle bounds, ExpressionEvaluator ev, IReportExpressionContext ctx)
    {
        var list = new List<LayoutPrimitive>(2);
        double w = bounds.Width.ToMm(), h = bounds.Height.ToMm();
        if (w <= 0.5 || h <= 0.5)
        {
            return list;
        }

        double v = Eval(ev, el.ValueExpression, ctx);
        double min = Eval(ev, el.MinimumExpression, ctx);
        double max = Eval(ev, el.MaximumExpression, ctx);
        double frac = max > min ? Math.Clamp((v - min) / (max - min), 0, 1) : 0;

        // Track (full extent) then the proportional fill on top.
        list.Add(new DrawRectanglePrimitive
        {
            Bounds = bounds,
            Fill = new BrushStyle(TrackColor),
            Pen = null,
            SourceElementId = el.Id,
        });
        if (frac > 0)
        {
            list.Add(new DrawRectanglePrimitive
            {
                Bounds = new Rectangle(bounds.X, bounds.Y, Unit.FromMm(w * frac), bounds.Height),
                Fill = new BrushStyle(ParseColor(el.FillColor, DefaultBar)),
                Pen = null,
                SourceElementId = el.Id,
            });
        }
        return list;
    }

    // ─── Gauge ───────────────────────────────────────────────────────────────────

    public static IEnumerable<LayoutPrimitive> RenderGauge(
        GaugeElement el, Rectangle bounds, ExpressionEvaluator ev, IReportExpressionContext ctx)
    {
        var list = new List<LayoutPrimitive>(16);
        double w = bounds.Width.ToMm(), h = bounds.Height.ToMm();
        if (w <= 2 || h <= 2)
        {
            return list;
        }

        double min = Eval(ev, el.MinimumExpression, ctx);
        double max = Eval(ev, el.MaximumExpression, ctx);
        double val = Eval(ev, el.ValueExpression, ctx);
        if (max <= min) max = min + 1;
        val = Math.Clamp(val, min, max);

        if (el.Kind == GaugeKind.Linear)
        {
            RenderLinearGauge(el, bounds, min, max, val, list);
        }
        else
        {
            RenderRadialGauge(el, bounds, min, max, val, list);
        }
        return list;
    }

    private static void RenderRadialGauge(
        GaugeElement el, Rectangle b, double min, double max, double val, List<LayoutPrimitive> list)
    {
        double x = b.X.ToMm(), y = b.Y.ToMm(), w = b.Width.ToMm(), h = b.Height.ToMm();
        double labelH = Math.Min(6, h * 0.22);
        double gaugeH = h - labelH;
        double r = Math.Min(w / 2, gaugeH) * 0.95;
        double rInner = r * 0.6;
        double cx = x + w / 2;
        double cy = y + r; // flat (diameter) side at the bottom; arc rises to y

        double Angle(double v) => 180 + (v - min) / (max - min) * 180; // 180°=left … 360°=right (upper arc)

        // Background band across the full sweep.
        list.Add(Ring(cx, cy, rInner, r, 180, 180, TrackColor, el.Id));

        // Coloured range bands (red/amber/green zones, etc.).
        foreach (var range in el.Ranges)
        {
            double rs = Math.Clamp(EvalLiteral(range.StartExpression), min, max);
            double re = Math.Clamp(EvalLiteral(range.EndExpression), min, max);
            if (re <= rs)
            {
                continue;
            }
            double a0 = Angle(rs);
            double sweep = Angle(re) - a0;
            list.Add(Ring(cx, cy, rInner, r, a0, sweep, ParseColor(range.ColorHex, DefaultBar), el.Id));
        }

        // Needle from the hub to the value angle.
        double ang = Angle(val) * Math.PI / 180.0;
        list.Add(Line(cx, cy, cx + r * 0.96 * Math.Cos(ang), cy + r * 0.96 * Math.Sin(ang),
            new PenStyle(NeedleColor, Unit.FromPoint(1.5)), el.Id));

        // Value label below the dial.
        list.Add(Text(FormatValue(val, el.Style.Format), x, y + gaugeH, w, labelH, 8, bold: true, HorizontalAlignment.Center, el.Id));
    }

    private static void RenderLinearGauge(
        GaugeElement el, Rectangle b, double min, double max, double val, List<LayoutPrimitive> list)
    {
        double x = b.X.ToMm(), y = b.Y.ToMm(), w = b.Width.ToMm(), h = b.Height.ToMm();
        double labelH = Math.Min(5, h * 0.4);
        double trackH = Math.Max(1, (h - labelH) * 0.6);
        double ty = y + (h - labelH - trackH) / 2;

        double XOf(double v) => x + (v - min) / (max - min) * w;

        // Track.
        list.Add(new DrawRectanglePrimitive
        {
            Bounds = Rect(x, ty, w, trackH),
            Fill = new BrushStyle(TrackColor),
            Pen = null,
            SourceElementId = el.Id,
        });

        // Coloured range bands behind the measure.
        foreach (var range in el.Ranges)
        {
            double rs = Math.Clamp(EvalLiteral(range.StartExpression), min, max);
            double re = Math.Clamp(EvalLiteral(range.EndExpression), min, max);
            if (re <= rs)
            {
                continue;
            }
            list.Add(new DrawRectanglePrimitive
            {
                Bounds = Rect(XOf(rs), ty, XOf(re) - XOf(rs), trackH),
                Fill = new BrushStyle(ParseColor(range.ColorHex, DefaultBar)),
                Pen = null,
                SourceElementId = el.Id,
            });
        }

        // Measure bar (the value) — a thinner dark bar (bullet-chart style).
        double measureH = trackH * 0.45;
        list.Add(new DrawRectanglePrimitive
        {
            Bounds = Rect(x, ty + (trackH - measureH) / 2, XOf(val) - x, measureH),
            Fill = new BrushStyle(NeedleColor),
            Pen = null,
            SourceElementId = el.Id,
        });

        list.Add(Text(FormatValue(val, el.Style.Format), x, y + h - labelH, w, labelH, 7, bold: false, HorizontalAlignment.Center, el.Id));
    }

    /// <summary>Builds a filled annular sector (ring segment) approximated by line segments.</summary>
    private static DrawPolygonPrimitive Ring(
        double cx, double cy, double rIn, double rOut, double startDeg, double sweepDeg, Color color, string? id)
    {
        int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweepDeg) / 4.0));
        var pts = new List<Point>(2 * steps + 2);
        for (int i = 0; i <= steps; i++)
        {
            double a = (startDeg + sweepDeg * i / steps) * Math.PI / 180.0;
            pts.Add(Pt(cx + rOut * Math.Cos(a), cy + rOut * Math.Sin(a)));
        }
        for (int i = steps; i >= 0; i--)
        {
            double a = (startDeg + sweepDeg * i / steps) * Math.PI / 180.0;
            pts.Add(Pt(cx + rIn * Math.Cos(a), cy + rIn * Math.Sin(a)));
        }
        return new DrawPolygonPrimitive
        {
            Points = new EquatableArray<Point>(pts.ToArray()),
            Closed = true,
            Fill = new BrushStyle(color),
            Pen = null,
            Bounds = Rect(cx - rOut, cy - rOut, 2 * rOut, 2 * rOut),
            SourceElementId = id,
        };
    }

    // ─── Sparkline ───────────────────────────────────────────────────────────────

    private static readonly Color SparkColor = Color.FromHex("#2563EB"); // blue-600

    public static IEnumerable<LayoutPrimitive> RenderSparkline(
        SparklineElement el, Rectangle bounds,
        IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> rows,
        ExpressionEvaluator ev, IReportExpressionContext baseCtx)
    {
        var list = new List<LayoutPrimitive>();
        double x = bounds.X.ToMm(), y = bounds.Y.ToMm(), w = bounds.Width.ToMm(), h = bounds.Height.ToMm();
        if (w <= 1 || h <= 1 || rows.Count == 0)
        {
            return list;
        }

        var values = new List<double>(rows.Count);
        double min = double.MaxValue, max = double.MinValue;
        foreach (var row in rows)
        {
            double v = Eval(ev, el.ValueExpression, new RowScopedContext(baseCtx, row));
            values.Add(v);
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        if (max <= min) max = min + 1;

        double pad = h * 0.12;
        double plotH = h - 2 * pad;
        double baseY = y + pad + plotH;
        double YOf(double v) => y + pad + (1 - (v - min) / (max - min)) * plotH;
        int n = values.Count;

        if (el.Kind == SparklineKind.Column)
        {
            double slot = w / n;
            double barW = Math.Max(0.3, slot * 0.7);
            for (int i = 0; i < n; i++)
            {
                double bx = x + i * slot + (slot - barW) / 2;
                double vy = YOf(values[i]);
                list.Add(new DrawRectanglePrimitive
                {
                    Bounds = Rect(bx, vy, barW, Math.Max(0.1, baseY - vy)),
                    Fill = new BrushStyle(SparkColor),
                    Pen = null,
                    SourceElementId = el.Id,
                });
            }
            return list;
        }

        var xs = new double[n];
        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = n == 1 ? x + w / 2 : x + (double)i / (n - 1) * w;
            pts[i] = Pt(xs[i], YOf(values[i]));
        }

        if (el.Kind == SparklineKind.Area)
        {
            var poly = new List<Point>(n + 2);
            poly.AddRange(pts);
            poly.Add(Pt(xs[n - 1], baseY));
            poly.Add(Pt(xs[0], baseY));
            list.Add(new DrawPolygonPrimitive
            {
                Points = new EquatableArray<Point>(poly.ToArray()),
                Closed = true,
                Fill = new BrushStyle(SparkColor with { A = (byte)64 }),
                Pen = new PenStyle(SparkColor, Unit.FromPoint(1)),
                Bounds = bounds,
                SourceElementId = el.Id,
            });
        }
        else
        {
            list.Add(new DrawPolygonPrimitive
            {
                Points = new EquatableArray<Point>(pts),
                Closed = false,
                Pen = new PenStyle(SparkColor, Unit.FromPoint(1.2)),
                Fill = null,
                Bounds = bounds,
                SourceElementId = el.Id,
            });
        }
        return list;
    }

    // ─── Indicator ───────────────────────────────────────────────────────────────

    public static IEnumerable<LayoutPrimitive> RenderIndicator(
        IndicatorElement el, Rectangle bounds, ExpressionEvaluator ev, IReportExpressionContext ctx)
    {
        var list = new List<LayoutPrimitive>(4);
        double x = bounds.X.ToMm(), y = bounds.Y.ToMm(), w = bounds.Width.ToMm(), h = bounds.Height.ToMm();
        if (w <= 1 || h <= 1)
        {
            return list;
        }

        double val = Eval(ev, el.ValueExpression, ctx);
        int stateIdx = MatchState(el.States, val);
        int count = Math.Max(1, el.States.Count);
        double pos = count > 1 ? (double)stateIdx / (count - 1) : 0.5;
        var color = SemanticColor(pos);

        switch (el.Kind)
        {
            case IndicatorKind.RatingBar:
                double slot = w / count;
                double barW = slot * 0.7;
                double barH = h * 0.8;
                double by = y + (h - barH) / 2;
                for (int i = 0; i < count; i++)
                {
                    double bx = x + i * slot + (slot - barW) / 2;
                    var c = i <= stateIdx ? color : TrackColor;
                    list.Add(new DrawRectanglePrimitive
                    {
                        Bounds = Rect(bx, by, barW, barH),
                        Fill = new BrushStyle(c),
                        Pen = null,
                        SourceElementId = el.Id,
                    });
                }
                break;

            case IndicatorKind.Shape:
            case IndicatorKind.Symbol:
                double s = Math.Min(w, h) * 0.8;
                list.Add(new DrawEllipsePrimitive
                {
                    Bounds = Rect(x + (w - s) / 2, y + (h - s) / 2, s, s),
                    Fill = new BrushStyle(color),
                    Pen = null,
                    SourceElementId = el.Id,
                });
                break;

            default: // DirectionalArrow — up / flat / down by state position
                list.Add(Arrow(x, y, w, h, pos, color, el.Id));
                break;
        }
        return list;
    }

    private static int MatchState(EquatableArray<IndicatorState> states, double val)
    {
        for (int i = 0; i < states.Count; i++)
        {
            double s = EvalLiteral(states[i].StartExpression);
            double e = EvalLiteral(states[i].EndExpression);
            if (val >= s && val <= e)
            {
                return i;
            }
        }
        if (states.Count == 0)
        {
            return 0;
        }
        // Below every band → first state; above every band → last state.
        return val < EvalLiteral(states[0].StartExpression) ? 0 : states.Count - 1;
    }

    private static Color SemanticColor(double pos)
        => pos < 0.34 ? Color.FromHex("#DC2626")  // red-600
         : pos < 0.67 ? Color.FromHex("#D97706")  // amber-600
         : Color.FromHex("#16A34A");              // green-600

    private static LayoutPrimitive Arrow(double x, double y, double w, double h, double pos, Color color, string? id)
    {
        double side = Math.Min(w, h) * 0.8;
        double cx = x + w / 2, cy = y + h / 2, half = side / 2;
        if (pos >= 0.67)
        {
            return FilledPolygon([Pt(cx, cy - half), Pt(cx + half, cy + half), Pt(cx - half, cy + half)], color, id);
        }
        if (pos < 0.34)
        {
            return FilledPolygon([Pt(cx, cy + half), Pt(cx + half, cy - half), Pt(cx - half, cy - half)], color, id);
        }
        // Flat (neutral) — a horizontal dash.
        return new DrawRectanglePrimitive
        {
            Bounds = Rect(cx - half, cy - half * 0.25, side, half * 0.5),
            Fill = new BrushStyle(color),
            Pen = null,
            SourceElementId = id,
        };
    }

    private static DrawPolygonPrimitive FilledPolygon(Point[] pts, Color color, string? id)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in pts)
        {
            minX = Math.Min(minX, p.X.ToMm());
            maxX = Math.Max(maxX, p.X.ToMm());
            minY = Math.Min(minY, p.Y.ToMm());
            maxY = Math.Max(maxY, p.Y.ToMm());
        }
        return new DrawPolygonPrimitive
        {
            Points = new EquatableArray<Point>(pts),
            Closed = true,
            Fill = new BrushStyle(color),
            Pen = null,
            Bounds = Rect(minX, minY, maxX - minX, maxY - minY),
            SourceElementId = id,
        };
    }

    // ─── Expression / formatting helpers ──────────────────────────────────────────

    private static double Eval(ExpressionEvaluator ev, string expr, IReportExpressionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return 0;
        }
        try
        {
            return ToDouble(ev.Evaluate(expr, ctx));
        }
        catch (ExpressionParseException)
        {
            return 0;
        }
    }

    /// <summary>Range bounds are usually literals (<c>"0"</c>, <c>"500"</c>); parse directly,
    /// avoiding a context dependency.</summary>
    private static double EvalLiteral(string expr)
        => double.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

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
        string str => double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0,
        IConvertible c => SafeConvertible(c),
        _ => 0,
    };

    private static double SafeConvertible(IConvertible c)
    {
        try { return c.ToDouble(CultureInfo.InvariantCulture); }
        catch (FormatException) { return 0; }
        catch (InvalidCastException) { return 0; }
        catch (OverflowException) { return 0; }
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }
        try { return Color.FromHex(hex); }
        catch (FormatException) { return fallback; }
    }

    // The element's Format property (when set) drives the value label — so a Gauge/DataBar value can be
    // currency, percent, etc., consistent with a textbox. Falls back to a sensible heuristic when unset.
    private static string FormatValue(double v, string? format = null)
        => !string.IsNullOrEmpty(format)
            ? ValueFormatter.Format(v, format, CultureInfo.GetCultureInfo("pt-BR"))
            : Math.Abs(v) >= 1000
                ? v.ToString("#,0", CultureInfo.GetCultureInfo("pt-BR"))
                : v.ToString("0.##", CultureInfo.InvariantCulture);

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
