namespace Reporting.Styling;

public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
    Justify,
}

public enum VerticalAlignment
{
    Top,
    Middle,
    Bottom,
}

public enum BorderLineStyle
{
    None,
    Solid,
    Dashed,
    Dotted,
    DashDot,
    Double,
}

/// <summary>Background gradient direction, mirroring RDL <c>BackgroundGradientType</c>. <see cref="None"/>
/// (the default) means a solid fill; any other value blends <c>Style.BackColor</c> (start) to
/// <c>Style.BackColorEnd</c> (end) along the given direction. <see cref="Center"/> is a radial blend.</summary>
public enum BackgroundGradientType
{
    None,
    LeftRight,
    TopBottom,
    Center,
    DiagonalLeft,
    DiagonalRight,
}
