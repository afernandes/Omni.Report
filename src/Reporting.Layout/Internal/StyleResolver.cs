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
    /// <summary>The element's <see cref="Style"/> with every conditional format whose condition is true applied.</summary>
    public static Style Resolve(ReportElement element, ExpressionEvaluator evaluator, IReportExpressionContext ctx)
    {
        var style = element.Style;
        foreach (var cf in element.ConditionalFormats)
        {
            if (evaluator.Evaluate<bool>(cf.Condition, ctx))
            {
                style = Merge(style, cf.Style);
            }
        }
        return style;
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
