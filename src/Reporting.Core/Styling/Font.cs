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

/// <summary>Logical font descriptor — resolved to a platform font by the renderer.</summary>
/// <param name="Family">Family name, e.g. <c>"Arial"</c>. Resolution is the backend's job: a family absent
/// from the host is substituted, which is why glyph metrics can differ between machines.</param>
/// <param name="Size">Size in typographic points (72 per inch).</param>
/// <param name="Style">Combined style attributes.</param>
public sealed record Font(string Family, double Size, FontStyle Style = FontStyle.Regular)
{
    /// <summary>Arial 10pt regular — what an element inherits when no font is declared anywhere.</summary>
    public static readonly Font Default = new("Arial", 10);

    /// <summary>A copy with a different size.</summary>
    public Font WithSize(double size) => this with { Size = size };

    /// <summary>A copy whose style <em>replaces</em> the current one.</summary>
    public Font WithStyle(FontStyle style) => this with { Style = style };

    /// <summary>A copy whose style is the current one OR-ed with <paramref name="style"/> — use this to add
    /// bold without dropping italic.</summary>
    public Font AddStyle(FontStyle style) => this with { Style = Style | style };
}
