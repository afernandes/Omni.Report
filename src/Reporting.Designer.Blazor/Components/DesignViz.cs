using System.Globalization;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;

namespace Reporting.Designer.Blazor.Components;

/// <summary>
/// Representative <b>design-time</b> SVG previews for the data-viz elements (Chart / Gauge / DataBar / Sparkline /
/// Indicator). Like SSRS / DevExpress / Telerik, the design surface shows a stylised SAMPLE (fixed dummy series),
/// not live data — real data only appears in Preview (F5). The preview honours the element's real kind and colours
/// so it reads as "a bar chart" / "a radial gauge", replacing the old dashed placeholder box.
/// </summary>
internal static class DesignViz
{
    private const string Accent = "#C2410C";
    private static readonly string[] Palette = { "#C2410C", "#0EA5E9", "#16A34A", "#A855F7", "#EAB308" };
    private static readonly double[] Sample = { 0.45, 0.7, 0.32, 0.85, 0.55, 0.62 }; // fixed dummy series (0..1)

    /// <summary>Returns the design-time preview markup (SVG for data-viz, an HTML table for Tablix), or null when
    /// the kind has no viz preview.</summary>
    public static string? Markup(ElementViewModel e) => e.Kind switch
    {
        DesignerElementKind.Chart => Chart(e),
        DesignerElementKind.Gauge => Gauge(e),
        DesignerElementKind.DataBar => DataBar(e),
        DesignerElementKind.Sparkline => Sparkline(e),
        DesignerElementKind.Indicator => Indicator(),
        DesignerElementKind.Tablix => Tablix(e),
        _ => null,
    };

    // ── Tablix: structural grid (HTML table). Matrix → corner/row-group/col-group/body; else flat columns. ──
    private static string Tablix(ElementViewModel e)
    {
        const string t = "border-collapse:collapse;width:100%;height:100%;font-size:9px;background:#fff;table-layout:fixed;font-family:sans-serif;";
        const string th = "border:1px solid #d4d4d4;background:#f5f4f0;padding:1px 3px;text-align:left;overflow:hidden;white-space:nowrap;color:#555;font-weight:600;";
        const string td = "border:1px solid #ececec;padding:1px 3px;overflow:hidden;white-space:nowrap;color:#0a7a55;font-family:monospace;";
        const string faded = "border:1px solid #f0f0f0;padding:1px 3px;color:#c4c4c4;text-align:center;";

        if (e.TablixIsMatrix)
        {
            string Cell(string s, string st) => $"<td style=\"{st}\">{Esc(s)}</td>";
            return $"<table style=\"{t}\">" +
                $"<tr>{Cell(e.TablixCorner, th)}{Cell(Strip(e.TablixColumnGroup), th)}{Cell("…", faded)}</tr>" +
                $"<tr>{Cell(Strip(e.TablixRowGroup), th)}{Cell(e.TablixCellExpr, td)}{Cell("…", faded)}</tr>" +
                $"<tr>{Cell("…", faded)}{Cell("…", faded)}{Cell("…", faded)}</tr></table>";
        }

        var cols = e.TablixColumns;
        if (cols.Count == 0)
        {
            return $"<table style=\"{t}\"><tr><th style=\"{th}\">Coluna</th></tr><tr><td style=\"{td}\">Fields.Valor</td></tr></table>";
        }
        var head = string.Concat(cols.Select(c => $"<th style=\"{th}\">{Esc(string.IsNullOrWhiteSpace(c.Header) ? c.Expression : c.Header)}</th>"));
        var detail = string.Concat(cols.Select(c => $"<td style=\"{td}\">{Esc(c.Expression)}</td>"));
        var dots = string.Concat(cols.Select(_ => $"<td style=\"{faded}\">…</td>"));
        return $"<table style=\"{t}\"><thead><tr>{head}</tr></thead><tbody><tr>{detail}</tr><tr>{dots}</tr></tbody></table>";
    }

    private static string Strip(string s) => string.IsNullOrWhiteSpace(s) ? "Grupo" : s;

    private static string Frame(string inner, string aspect = "none") =>
        $"<svg viewBox=\"0 0 100 60\" preserveAspectRatio=\"x{aspect}\" width=\"100%\" height=\"100%\" " +
        $"xmlns=\"http://www.w3.org/2000/svg\" style=\"display:block;\">{inner}</svg>";

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // ── Chart: bar / line / area / pie, by ChartKind ──────────────────────────────
    private static string Chart(ElementViewModel e)
    {
        const double top = 10, bottom = 54, left = 6, right = 94, w = right - left, h = bottom - top;
        string body = e.ChartKind switch
        {
            ChartKind.Pie => Pie(),
            ChartKind.Line or ChartKind.Scatter => Line(left, top, w, h, area: false),
            ChartKind.Area => Line(left, top, w, h, area: true),
            _ => Bars(left, top, w, h),
        };
        var title = string.IsNullOrWhiteSpace(e.ChartTitle) ? "" :
            $"<text x=\"50\" y=\"7\" text-anchor=\"middle\" font-size=\"6\" fill=\"#666\" font-family=\"sans-serif\">{Esc(e.ChartTitle)}</text>";
        return Frame($"<rect x=\"0\" y=\"0\" width=\"100\" height=\"60\" fill=\"#fff\"/>{title}" +
                     $"<line x1=\"{F(left)}\" y1=\"{F(bottom)}\" x2=\"{F(right)}\" y2=\"{F(bottom)}\" stroke=\"#ddd\" stroke-width=\"0.7\"/>{body}");
    }

    private static string Bars(double left, double top, double w, double h)
    {
        var n = Sample.Length;
        var bw = w / n * 0.6;
        var gap = w / n;
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < n; i++)
        {
            var bh = Sample[i] * h;
            var x = left + i * gap + (gap - bw) / 2;
            var y = top + h - bh;
            sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(bw)}\" height=\"{F(bh)}\" fill=\"{Palette[i % Palette.Length]}\" rx=\"0.6\"/>");
        }
        return sb.ToString();
    }

    private static string Line(double left, double top, double w, double h, bool area)
    {
        var n = Sample.Length;
        var pts = new List<string>();
        for (var i = 0; i < n; i++)
        {
            var x = left + w * i / (n - 1);
            var y = top + h - Sample[i] * h;
            pts.Add($"{F(x)},{F(y)}");
        }
        var poly = string.Join(" ", pts);
        var fill = area ? $"<polygon points=\"{F(left)},{F(top + h)} {poly} {F(left + w)},{F(top + h)}\" fill=\"{Accent}\" fill-opacity=\"0.18\"/>" : "";
        var dots = string.Concat(pts.Select(p => { var c = p.Split(','); return $"<circle cx=\"{c[0]}\" cy=\"{c[1]}\" r=\"1.2\" fill=\"{Accent}\"/>"; }));
        return $"{fill}<polyline points=\"{poly}\" fill=\"none\" stroke=\"{Accent}\" stroke-width=\"1.6\"/>{dots}";
    }

    private static string Pie()
    {
        double cx = 50, cy = 32, r = 22, a = -90;
        var slices = new[] { 0.35, 0.25, 0.22, 0.18 };
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < slices.Length; i++)
        {
            var a2 = a + slices[i] * 360;
            var (x1, y1) = Polar(cx, cy, r, a);
            var (x2, y2) = Polar(cx, cy, r, a2);
            var large = slices[i] > 0.5 ? 1 : 0;
            sb.Append($"<path d=\"M{F(cx)},{F(cy)} L{F(x1)},{F(y1)} A{F(r)},{F(r)} 0 {large} 1 {F(x2)},{F(y2)} Z\" fill=\"{Palette[i % Palette.Length]}\"/>");
            a = a2;
        }
        return sb.ToString();
    }

    // ── Gauge: radial arc + needle, or linear track ───────────────────────────────
    private static string Gauge(ElementViewModel e)
    {
        const double frac = 0.62; // sample value position
        if (e.GaugeType == GaugeKind.Linear)
        {
            return Frame("<rect x=\"0\" y=\"0\" width=\"100\" height=\"60\" fill=\"#fff\"/>" +
                         "<rect x=\"10\" y=\"26\" width=\"80\" height=\"8\" rx=\"4\" fill=\"#eee\"/>" +
                         $"<rect x=\"10\" y=\"26\" width=\"{F(80 * frac)}\" height=\"8\" rx=\"4\" fill=\"#16A34A\"/>");
        }
        double cx = 50, cy = 46, r = 34;
        var (sx, sy) = Polar(cx, cy, r, 180);
        var (ex, ey) = Polar(cx, cy, r, 0);
        var (nx, ny) = Polar(cx, cy, r - 4, 180 + 180 * frac);
        var ranges = "<path d=\"M" + F(sx) + "," + F(sy) + $" A{F(r)},{F(r)} 0 0 1 {F(ex)},{F(ey)}\" fill=\"none\" stroke=\"#e5e5e5\" stroke-width=\"6\"/>";
        var (mx, my) = Polar(cx, cy, r, 180 + 180 * frac);
        var arc = $"<path d=\"M{F(sx)},{F(sy)} A{F(r)},{F(r)} 0 0 1 {F(mx)},{F(my)}\" fill=\"none\" stroke=\"#16A34A\" stroke-width=\"6\"/>";
        return Frame("<rect x=\"0\" y=\"0\" width=\"100\" height=\"60\" fill=\"#fff\"/>" + ranges + arc +
                     $"<line x1=\"{F(cx)}\" y1=\"{F(cy)}\" x2=\"{F(nx)}\" y2=\"{F(ny)}\" stroke=\"#333\" stroke-width=\"1.6\"/>" +
                     $"<circle cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"2.4\" fill=\"#333\"/>");
    }

    private static string DataBar(ElementViewModel e)
    {
        var fill = SafeColor(e.DataBarFill, Accent);
        return Frame("<rect x=\"0\" y=\"0\" width=\"100\" height=\"60\" fill=\"#fff\"/>" +
                     "<rect x=\"6\" y=\"24\" width=\"88\" height=\"12\" rx=\"2\" fill=\"#eee\"/>" +
                     $"<rect x=\"6\" y=\"24\" width=\"{F(88 * 0.66)}\" height=\"12\" rx=\"2\" fill=\"{fill}\"/>");
    }

    private static string Sparkline(ElementViewModel e)
    {
        const double left = 4, top = 8, w = 92, h = 44;
        if (e.SparkKind == SparklineKind.Column)
        {
            return Frame("<rect x=\"0\" y=\"0\" width=\"100\" height=\"60\" fill=\"#fff\"/>" + Bars(left, top, w, h));
        }
        return Frame("<rect x=\"0\" y=\"0\" width=\"100\" height=\"60\" fill=\"#fff\"/>" +
                     Line(left, top, w, h, area: e.SparkKind == SparklineKind.Area));
    }

    private static string Indicator() =>
        Frame("<rect x=\"0\" y=\"0\" width=\"100\" height=\"60\" fill=\"#fff\"/>" +
              "<circle cx=\"50\" cy=\"30\" r=\"16\" fill=\"#16A34A\" fill-opacity=\"0.15\"/>" +
              "<path d=\"M50,20 L60,36 L40,36 Z\" fill=\"#16A34A\"/>", "MidYMid meet");

    private static (double, double) Polar(double cx, double cy, double r, double deg)
    {
        var a = deg * Math.PI / 180;
        return (cx + r * Math.Cos(a), cy + r * Math.Sin(a));
    }

    private static string SafeColor(string? c, string fallback)
        => !string.IsNullOrWhiteSpace(c) && c.StartsWith('#') && (c.Length == 7 || c.Length == 4) ? c : fallback;

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
