using Reporting.Geometry;

namespace Reporting.Rendering;

/// <summary>
/// Deterministic, font-free text measurer based on average glyph width × character count.
/// Used by the layout engine when no real font shaping is available (tests, headless paging).
/// Heights track the font size; widths approximate Latin text decently for layout estimation.
/// </summary>
public sealed class AverageWidthTextMeasurer : ITextMeasurer
{
    /// <summary>Glyphs per em — typical for Latin text in a proportional font.</summary>
    public double AverageWidthRatio { get; init; } = 0.55;

    /// <summary>Line-height multiplier applied to <c>style.Font.Size</c>.</summary>
    public double LineHeightRatio { get; init; } = 1.25;

    public Size Measure(string text, TextStyle style, Unit? maxWidth = null)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (string.IsNullOrEmpty(text))
        {
            return new Size(Unit.Zero, Unit.FromPoint(style.Font.Size * LineHeightRatio));
        }
        var unitWidth = Unit.FromPoint(style.Font.Size * AverageWidthRatio);
        var lineHeight = Unit.FromPoint(style.Font.Size * LineHeightRatio);

        if (maxWidth is null || !style.WordWrap)
        {
            // Single line: width = chars * unitWidth.
            return new Size(unitWidth * text.Length, lineHeight);
        }

        var availableMils = maxWidth.Value.Mils;
        if (availableMils <= 0)
        {
            return new Size(Unit.Zero, lineHeight);
        }
        int charsPerLine = Math.Max(1, availableMils / Math.Max(1, unitWidth.Mils));
        int lines = (text.Length + charsPerLine - 1) / charsPerLine;
        if (lines < 1)
        {
            lines = 1;
        }
        return new Size(maxWidth.Value, lineHeight * lines);
    }
}
