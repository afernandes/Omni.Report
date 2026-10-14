using Reporting.Geometry;

namespace Reporting.Styling;

/// <summary>One edge of a border: its line style, thickness, and color.</summary>
/// <param name="Style">Line style. <see cref="BorderLineStyle.None"/> hides the edge.</param>
/// <param name="Thickness">Stroke width. Zero hides the edge even when a style is set.</param>
/// <param name="Color">Stroke colour.</param>
public sealed record BorderSide(BorderLineStyle Style, Unit Thickness, Color Color)
{
    /// <summary>An invisible edge — no style, no thickness, transparent.</summary>
    public static readonly BorderSide None = new(BorderLineStyle.None, Unit.Zero, Color.Transparent);

    /// <summary>True when the edge would actually draw: it needs both a style and a non-zero thickness.</summary>
    public bool IsVisible => Style != BorderLineStyle.None && Thickness > Unit.Zero;
}
