using Reporting.Elements;
using Reporting.Expressions;
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
        => baseStyle with
        {
            Font = overlay.Font ?? baseStyle.Font,
            ForeColor = overlay.ForeColor ?? baseStyle.ForeColor,
            BackColor = overlay.BackColor ?? baseStyle.BackColor,
            Border = overlay.Border ?? baseStyle.Border,
            Padding = overlay.Padding ?? baseStyle.Padding,
            HorizontalAlignment = overlay.HorizontalAlignment,
            VerticalAlignment = overlay.VerticalAlignment,
            WordWrap = overlay.WordWrap,
            Format = overlay.Format ?? baseStyle.Format,
        };
}
