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

/// <summary>The four edges of an element's border (left, top, right, bottom), each independently styled.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
public sealed record Border(BorderSide Left, BorderSide Top, BorderSide Right, BorderSide Bottom)
{
    /// <summary>All four edges invisible.</summary>
    public static readonly Border None = new(BorderSide.None, BorderSide.None, BorderSide.None, BorderSide.None);

    /// <summary>The same edge repeated on all four sides.</summary>
    public static Border Uniform(BorderSide side) => new(side, side, side, side);

    /// <summary>The same style, thickness and colour on all four sides.</summary>
    public static Border Uniform(BorderLineStyle style, Unit thickness, Color color)
        => Uniform(new BorderSide(style, thickness, color));
}
