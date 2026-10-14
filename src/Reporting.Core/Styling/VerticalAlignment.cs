namespace Reporting.Styling;

/// <summary>Vertical placement of content within its box (top, middle, or bottom).</summary>
public enum VerticalAlignment
{
    /// <summary>Against the top edge of the content box. The default.</summary>
    Top,

    /// <summary>Centred between the top and bottom edges.</summary>
    Middle,

    /// <summary>Against the bottom edge — keeps baselines aligned across a row of boxes of unequal height.</summary>
    Bottom,
}
