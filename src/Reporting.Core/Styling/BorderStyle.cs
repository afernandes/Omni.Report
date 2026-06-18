using Reporting.Geometry;

namespace Reporting.Styling;

public sealed record BorderSide(BorderLineStyle Style, Unit Thickness, Color Color)
{
    public static readonly BorderSide None = new(BorderLineStyle.None, Unit.Zero, Color.Transparent);

    public bool IsVisible => Style != BorderLineStyle.None && Thickness > Unit.Zero;
}

public sealed record Border(BorderSide Left, BorderSide Top, BorderSide Right, BorderSide Bottom)
{
    public static readonly Border None = new(BorderSide.None, BorderSide.None, BorderSide.None, BorderSide.None);

    public static Border Uniform(BorderSide side) => new(side, side, side, side);
    public static Border Uniform(BorderLineStyle style, Unit thickness, Color color)
        => Uniform(new BorderSide(style, thickness, color));
}
