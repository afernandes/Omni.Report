namespace Reporting.Styling;

/// <summary>Font style attributes (bold, italic, underline, strikeout) that can be combined as flags.</summary>
[Flags]
public enum FontStyle
{
    /// <summary>No attributes — upright, normal weight.</summary>
    Regular = 0,

    /// <summary>Heavier weight.</summary>
    Bold = 1 << 0,

    /// <summary>Slanted.</summary>
    Italic = 1 << 1,

    /// <summary>A line under the text.</summary>
    Underline = 1 << 2,

    /// <summary>A line through the text.</summary>
    Strikeout = 1 << 3,
}
