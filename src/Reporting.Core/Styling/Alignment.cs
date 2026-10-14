namespace Reporting.Styling;

/// <summary>Horizontal placement of content within its box (left, center, right, or justified).</summary>
public enum HorizontalAlignment
{
    /// <summary>Against the left edge of the content box. The default.</summary>
    Left,

    /// <summary>Centred between the left and right edges.</summary>
    Center,

    /// <summary>Against the right edge — the usual choice for numeric columns.</summary>
    Right,

    /// <summary>Stretched to both edges by widening the spaces between words. Only affects lines that
    /// wrap; the last line of a paragraph stays left-aligned.</summary>
    Justify,
}
