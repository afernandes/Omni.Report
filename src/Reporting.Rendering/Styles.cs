using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering;

/// <summary>Text rendering style resolved from a domain <see cref="Style"/> + <see cref="Font"/>.</summary>
/// <param name="Font">Family, size and style attributes.</param>
/// <param name="ForeColor">Colour the glyphs are painted in.</param>
/// <param name="HorizontalAlignment">Placement across the box.</param>
/// <param name="VerticalAlignment">Placement down the box.</param>
/// <param name="WordWrap">Whether long text wraps to further lines instead of overflowing on one.</param>
/// <param name="Padding">Space between the box edges and the text.</param>
public sealed record TextStyle(
    Font Font,
    Color ForeColor,
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    bool WordWrap = true,
    Thickness Padding = default)
{
    /// <summary>Black text in the default font.</summary>
    public static readonly TextStyle Default = new(Font.Default, Color.Black);

    /// <summary>A copy using a different font.</summary>
    public TextStyle WithFont(Font font) => this with { Font = font };

    /// <summary>A copy using a different foreground colour.</summary>
    public TextStyle WithColor(Color color) => this with { ForeColor = color };
}
