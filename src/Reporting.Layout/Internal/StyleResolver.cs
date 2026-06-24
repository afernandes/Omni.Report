using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

/// <summary>
/// Resolves an element's effective <see cref="Style"/> by overlaying each matching conditional format,
/// in declaration order. Shared by every renderer so conditional formatting behaves identically across
/// band elements, Tablix cells, and any future value-displaying component — instead of each renderer
/// re-implementing (and diverging on) the merge semantics.
/// </summary>
internal static class StyleResolver
{
    /// <summary>The element's effective <see cref="Style"/>: its named-style base (<see cref="Style.BasedOn"/>,
    /// looked up in <paramref name="namedStyles"/>) overlaid by the element's inline style, then by every matching
    /// conditional format in declaration order. <paramref name="namedStyles"/> should be the FLATTENED table from
    /// <see cref="FlattenNamedStyles"/> (BasedOn chains already resolved); null = no named styles.</summary>
    public static Style Resolve(ReportElement element, ExpressionEvaluator evaluator, IReportExpressionContext ctx,
        IReadOnlyDictionary<string, Style>? namedStyles = null)
    {
        var style = WithNamedBase(element.Style, namedStyles);
        foreach (var cf in element.ConditionalFormats)
        {
            if (evaluator.Evaluate<bool>(cf.Condition, ctx))
            {
                style = Merge(style, cf.Style);
            }
        }
        return style;
    }

    /// <summary>A style with its named base (<see cref="Style.BasedOn"/>) applied — but WITHOUT conditional formats.
    /// For contexts that have no per-row data context, e.g. matrix aggregate cells, where evaluating a CF would be
    /// against the wrong scope. <paramref name="namedStyles"/> is the flattened table (see <see cref="FlattenNamedStyles"/>).</summary>
    public static Style WithNamedBase(Style style, IReadOnlyDictionary<string, Style>? namedStyles)
        => style.BasedOn is { } name && namedStyles is not null && namedStyles.TryGetValue(name, out var baseStyle)
            ? MergeNamedBase(baseStyle, style) // named base ← inline overlay (layout fields inheritable)
            : style;

    /// <summary>Pre-resolves every named style's <see cref="Style.BasedOn"/> chain ONCE (cycle-guarded) so the
    /// per-element/per-row lookup in <see cref="Resolve"/> is a single O(1) merge. The named base is independent of
    /// row data, so this runs once per paginate.</summary>
    public static Dictionary<string, Style> FlattenNamedStyles(IReadOnlyDictionary<string, Style> table)
    {
        var flat = new Dictionary<string, Style>(table.Count, StringComparer.Ordinal);
        foreach (var name in table.Keys)
        {
            flat[name] = ResolveChain(name, table, new HashSet<string>(StringComparer.Ordinal));
        }
        return flat;
    }

    private static Style ResolveChain(string name, IReadOnlyDictionary<string, Style> table, HashSet<string> visiting)
    {
        if (!table.TryGetValue(name, out var style))
        {
            return Style.Default;
        }
        // No base, or we're already resolving this name (cycle) → use it as-is, breaking the chain.
        if (style.BasedOn is not { } baseName || !visiting.Add(name))
        {
            return style;
        }
        var baseStyle = ResolveChain(baseName, table, visiting);
        visiting.Remove(name);
        return MergeNamedBase(baseStyle, style);
    }

    /// <summary>Merge for NAMED-STYLE inheritance: like <see cref="Merge"/> for the nullable members, but the
    /// non-nullable layout fields (alignment / word-wrap) are INHERITED from the base unless the overlay diverges
    /// from the type default. Without this, a "centered heading" named style would never pass its alignment on,
    /// because an element that doesn't re-state alignment carries the default (Left/Top/wrap), which the plain
    /// <see cref="Merge"/> takes unconditionally. The rare cost is that explicitly re-stating the default over a
    /// non-default base inherits the base instead — acceptable for reuse, and conditional formats keep plain Merge.</summary>
    private static Style MergeNamedBase(Style baseStyle, Style overlay)
    {
        var merged = Merge(baseStyle, overlay);
        return merged with
        {
            HorizontalAlignment = overlay.HorizontalAlignment != HorizontalAlignment.Left
                ? overlay.HorizontalAlignment : baseStyle.HorizontalAlignment,
            VerticalAlignment = overlay.VerticalAlignment != VerticalAlignment.Top
                ? overlay.VerticalAlignment : baseStyle.VerticalAlignment,
            WordWrap = overlay.WordWrap ? baseStyle.WordWrap : false, // false = explicit no-wrap; true = default → inherit
        };
    }

    /// <summary>
    /// Overlay <paramref name="overlay"/> onto <paramref name="baseStyle"/>: nullable members win only when
    /// set; layout members (alignment / word wrap) always take the overlay's value.
    /// </summary>
    public static Style Merge(Style baseStyle, Style overlay)
    {
        // The background fill is a coherent unit (start + end + direction). An overlay that sets a gradient
        // replaces the whole trio; an overlay that sets only a solid BackColor wins as a CLEAN solid (clearing any
        // base gradient, so e.g. a "negative → red" conditional format paints solid red, not red→oldEnd); otherwise
        // the base fill is kept intact.
        Color? backColor;
        Color? backColorEnd;
        BackgroundGradientType gradient;
        if (overlay.BackgroundGradient != BackgroundGradientType.None)
        {
            backColor = overlay.BackColor ?? baseStyle.BackColor;
            backColorEnd = overlay.BackColorEnd;
            gradient = overlay.BackgroundGradient;
        }
        else if (overlay.BackColor is not null)
        {
            backColor = overlay.BackColor;
            backColorEnd = null;
            gradient = BackgroundGradientType.None;
        }
        else
        {
            backColor = baseStyle.BackColor;
            backColorEnd = baseStyle.BackColorEnd;
            gradient = baseStyle.BackgroundGradient;
        }

        return baseStyle with
        {
            Font = overlay.Font ?? baseStyle.Font,
            ForeColor = overlay.ForeColor ?? baseStyle.ForeColor,
            BackColor = backColor,
            BackColorEnd = backColorEnd,
            BackgroundGradient = gradient,
            Border = overlay.Border ?? baseStyle.Border,
            Padding = overlay.Padding ?? baseStyle.Padding,
            HorizontalAlignment = overlay.HorizontalAlignment,
            VerticalAlignment = overlay.VerticalAlignment,
            WordWrap = overlay.WordWrap,
            Format = overlay.Format ?? baseStyle.Format,
        };
    }

    /// <summary>The background fill for a resolved style as a <see cref="BrushStyle"/>, or null when there is no
    /// fill at all. Centralises the band/Tablix logic: <c>BackColor</c> is the (gradient) start; when it is unset
    /// but an end colour exists (e.g. an RDL with a gradient type but no <c>BackgroundColor</c>) the end colour
    /// stands in as a solid so nothing is silently dropped.</summary>
    public static BrushStyle? BackgroundBrush(Style style)
    {
        var start = style.BackColor ?? style.BackColorEnd;
        if (start is null)
        {
            return null;
        }
        return new BrushStyle(start.Value, style.BackColorEnd, style.BackgroundGradient);
    }
}
