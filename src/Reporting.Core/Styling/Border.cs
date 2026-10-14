using Reporting.Geometry;

namespace Reporting.Styling;

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
