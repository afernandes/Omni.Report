namespace Reporting.Styling;

/// <summary>Font style attributes (bold, italic, underline, strikeout) that can be combined as flags.</summary>
[Flags]
public enum FontStyle
{
    Regular = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Strikeout = 1 << 3,
}

/// <summary>Logical font descriptor — resolved to a platform font by the renderer.</summary>
public sealed record Font(string Family, double Size, FontStyle Style = FontStyle.Regular)
{
    public static readonly Font Default = new("Arial", 10);

    public Font WithSize(double size) => this with { Size = size };
    public Font WithStyle(FontStyle style) => this with { Style = style };
    public Font AddStyle(FontStyle style) => this with { Style = Style | style };
}
