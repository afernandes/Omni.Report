namespace Reporting.Styling;

/// <summary>Horizontal placement of content within its box (left, center, right, or justified).</summary>
public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
    Justify,
}

/// <summary>Vertical placement of content within its box (top, middle, or bottom).</summary>
public enum VerticalAlignment
{
    Top,
    Middle,
    Bottom,
}

/// <summary>Line style for a border edge (none, solid, dashed, dotted, dash-dot, or double).</summary>
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
