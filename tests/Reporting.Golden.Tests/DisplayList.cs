using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Golden.Tests;

/// <summary>
/// Renders a <see cref="RenderedReport"/> as compact, line-oriented text — the "display list" the
/// paginator produced.
/// </summary>
/// <remarks>
/// <para>This is the golden format on purpose. A PNG golden would be unreviewable (a diff shows
/// "binary files differ") and platform-fragile (glyph rasterisation and hinting differ between
/// Windows and Linux). The display list is the actual layout contract: one line per drawing
/// instruction, with geometry and style. A reviewer reads the diff and sees <em>what moved</em>.</para>
///
/// <para><b>Determinism.</b> Every coordinate comes from <see cref="Unit"/>, which is an integer
/// count of mils, and the default <c>AverageWidthTextMeasurer</c> is pure arithmetic — no font
/// file, no shaping engine, no culture. So the same report yields byte-identical text on every OS.
/// Formatting is pinned to <see cref="CultureInfo.InvariantCulture"/> for the same reason.</para>
///
/// <para><b>Only non-default values are printed.</b> A golden crowded with <c>wrap=True</c> on every
/// line hides the one field that changed, so defaults stay implicit.</para>
/// </remarks>
internal static partial class DisplayList
{
    public static string Format(RenderedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        sb.Append("report ").Append(report.Name).Append(" — ").Append(report.PageCount)
          .Append(report.PageCount == 1 ? " page" : " pages").AppendLine();

        var ids = new IdNormalizer();
        foreach (var page in report.Pages)
        {
            sb.AppendLine();
            AppendPageHeader(sb, page);
            foreach (var p in page.Primitives)
            {
                sb.Append("  ").AppendLine(FormatPrimitive(p, ids));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Rewrites element ids to first-seen ordinals (<c>e1</c>, <c>e2</c>, …).
    /// </summary>
    /// <remarks>
    /// <see cref="Reporting.Elements.ReportElement.Id"/> defaults to <c>Guid.NewGuid()</c>, so the raw
    /// value differs on every run and would make every golden fail. The ordinal keeps what actually
    /// carries meaning — that a primitive <em>has</em> a source, and that two primitives share one (a
    /// filled rectangle and its border come from the same element) — while staying stable.
    /// Applied to any 32-hex-digit run so ids embedded in link/bookmark targets normalise too.
    /// </remarks>
    private sealed partial class IdNormalizer
    {
        private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

        public string Apply(string value) => GuidLike().Replace(value, m =>
        {
            if (!_map.TryGetValue(m.Value, out var stable))
            {
                stable = "e" + (_map.Count + 1).ToString(CultureInfo.InvariantCulture);
                _map[m.Value] = stable;
            }
            return stable;
        });

        [GeneratedRegex("[0-9a-f]{32}")]
        private static partial Regex GuidLike();
    }

    private static void AppendPageHeader(StringBuilder sb, RenderedPage page)
    {
        var s = page.PageSetup;
        sb.Append(Inv($"page {page.PageNumber}  {s.Paper.Name} {s.Orientation} {s.PageWidth}×{s.PageHeight}"));
        sb.Append(Inv($"  margins {s.Margins.Left}/{s.Margins.Top}/{s.Margins.Right}/{s.Margins.Bottom}"));
        if (s.Columns > 1)
        {
            sb.Append(Inv($"  columns {s.Columns} gap {s.ColumnSpacing}"));
        }
        sb.Append(Inv($"  [{page.Primitives.Count}]")).AppendLine();
    }

    private static string FormatPrimitive(LayoutPrimitive p, IdNormalizer ids)
    {
        var sb = new StringBuilder();
        sb.Append(Kind(p).PadRight(7)).Append(' ').Append(Rect(p.Bounds));

        switch (p)
        {
            case DrawTextPrimitive t:
                sb.Append("  \"").Append(Escape(t.Text)).Append('"').Append(TextStyleOf(t.Style));
                break;
            case DrawLinePrimitive l:
                sb.Append(Inv($"  ({l.From.X},{l.From.Y})→({l.To.X},{l.To.Y})")).Append(PenOf(l.Pen));
                break;
            case DrawRectanglePrimitive r:
                sb.Append(FillOf(r.Fill)).Append(PenOf(r.Pen));
                break;
            case DrawEllipsePrimitive e:
                sb.Append(FillOf(e.Fill)).Append(PenOf(e.Pen));
                break;
            case DrawImagePrimitive i:
                // The bytes themselves are not part of the layout contract — length + sizing are.
                sb.Append(Inv($"  {i.Data.Count} bytes sizing={i.Sizing}"));
                break;
            case DrawPolygonPrimitive g:
                sb.Append(Inv($"  {g.Points.Count} pts{(g.Closed ? " closed" : " open")}"))
                  .Append(FillOf(g.Fill)).Append(PenOf(g.Pen));
                break;
        }

        AppendCommon(sb, p, ids);
        return sb.ToString();
    }

    /// <summary>Fields shared by every primitive, printed only when set — these are the ones that
    /// silently regress (a lost clip, a dropped hyperlink) without changing any geometry.</summary>
    private static void AppendCommon(StringBuilder sb, LayoutPrimitive p, IdNormalizer ids)
    {
        if (p.SourceElementId is { Length: > 0 } id)
        {
            sb.Append("  src=").Append(ids.Apply(id));
        }
        if (p.LinkTarget is { Length: > 0 } link)
        {
            sb.Append("  link=").Append(ids.Apply(link));
        }
        if (p.BookmarkId is { Length: > 0 } bm)
        {
            sb.Append("  bookmark=").Append(ids.Apply(bm));
        }
        if (p.DocMapLabel is { Length: > 0 } dm)
        {
            sb.Append("  docmap=\"").Append(Escape(dm)).Append('"');
        }
        if (p.ClipBounds is { } clip)
        {
            sb.Append("  clip=").Append(Rect(clip));
            if (p.ClipCornerRadius != Unit.Zero)
            {
                sb.Append(Inv($" r={p.ClipCornerRadius}"));
            }
        }
        if (p.IsVisual)
        {
            sb.Append("  visual");
        }
    }

    private static string Kind(LayoutPrimitive p) => p switch
    {
        DrawTextPrimitive => "text",
        DrawLinePrimitive => "line",
        DrawRectanglePrimitive => "rect",
        DrawEllipsePrimitive => "ellipse",
        DrawImagePrimitive => "image",
        DrawPolygonPrimitive => "poly",
        _ => p.GetType().Name,
    };

    private static string Rect(Rectangle r) => Inv($"[{r.X},{r.Y} {r.Width}×{r.Height}]");

    private static string TextStyleOf(TextStyle s)
    {
        var sb = new StringBuilder();
        sb.Append(Inv($"  {s.Font.Family}/{s.Font.Size:0.##}"));
        if (s.Font.Style != FontStyle.Regular)
        {
            sb.Append('/').Append(s.Font.Style);
        }
        if (s.ForeColor != Color.Black)
        {
            sb.Append(" fore=").Append(s.ForeColor.ToHex());
        }
        if (s.HorizontalAlignment != HorizontalAlignment.Left || s.VerticalAlignment != VerticalAlignment.Top)
        {
            sb.Append(Inv($" align={s.HorizontalAlignment}/{s.VerticalAlignment}"));
        }
        if (!s.WordWrap)
        {
            sb.Append(" nowrap");
        }
        if (s.Padding != default)
        {
            sb.Append(Inv($" pad={s.Padding.Left}/{s.Padding.Top}/{s.Padding.Right}/{s.Padding.Bottom}"));
        }
        return sb.ToString();
    }

    private static string PenOf(PenStyle? pen)
    {
        if (pen is null)
        {
            return string.Empty;
        }
        var style = pen.Style == BorderLineStyle.Solid ? string.Empty : Inv($"/{pen.Style}");
        return Inv($"  pen={pen.Color.ToHex()}/{pen.Thickness.ToPoints():0.##}pt{style}");
    }

    private static string FillOf(BrushStyle? fill)
    {
        if (fill is null)
        {
            return string.Empty;
        }
        // A gradient that silently degrades to its start colour is exactly the class of regression
        // this suite exists to catch, so the direction and end colour are always printed.
        return fill.HasGradient
            ? Inv($"  fill={fill.Color.ToHex()}→{fill.GradientEndColor!.Value.ToHex()}/{fill.Gradient}")
            : Inv($"  fill={fill.Color.ToHex()}");
    }

    private static string Escape(string s) => s.Replace("\n", "\\n", StringComparison.Ordinal)
                                               .Replace("\r", string.Empty, StringComparison.Ordinal);

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
