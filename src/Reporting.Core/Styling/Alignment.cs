namespace Reporting.Styling;

/// <summary>Horizontal placement of content within its box (left, center, right, or justified).</summary>
public enum HorizontalAlignment
{
    /// <summary>Against the left edge of the content box. The default.</summary>
    Left,

    /// <summary>Centred between the left and right edges.</summary>
    Center,

    /// <summary>Against the right edge — the usual choice for numeric columns.</summary>
    Right,

    /// <summary>Stretched to both edges by widening the spaces between words. Only affects lines that
    /// wrap; the last line of a paragraph stays left-aligned.</summary>
    Justify,
}

/// <summary>Vertical placement of content within its box (top, middle, or bottom).</summary>
public enum VerticalAlignment
{
    /// <summary>Against the top edge of the content box. The default.</summary>
    Top,

    /// <summary>Centred between the top and bottom edges.</summary>
    Middle,

    /// <summary>Against the bottom edge — keeps baselines aligned across a row of boxes of unequal height.</summary>
    Bottom,
}

/// <summary>Line style for a border edge (none, solid, dashed, dotted, dash-dot, or double).</summary>
public enum BorderLineStyle
{
    /// <summary>No line is drawn. The side is invisible regardless of its colour or thickness.</summary>
    None,

    /// <summary>A continuous line. The default when a border is declared.</summary>
    Solid,

    /// <summary>A line of dashes.</summary>
    Dashed,

    /// <summary>A line of dots.</summary>
    Dotted,

    /// <summary>Alternating dashes and dots.</summary>
    DashDot,

    /// <summary>Two parallel solid lines with a gap between them.</summary>
    Double,
}

/// <summary>Background gradient direction, mirroring RDL <c>BackgroundGradientType</c>. <see cref="None"/>
/// (the default) means a solid fill; any other value blends <c>Style.BackColor</c> (start) to
/// <c>Style.BackColorEnd</c> (end) along the given direction. <see cref="Center"/> is a radial blend.</summary>
public enum BackgroundGradientType
{
    /// <summary>No gradient — the fill is the solid <c>Style.BackColor</c>. The default.</summary>
    None,

    /// <summary>Horizontal blend, start colour on the left.</summary>
    LeftRight,

    /// <summary>Vertical blend, start colour at the top.</summary>
    TopBottom,

    /// <summary>Radial blend, start colour at the centre fading outwards.</summary>
    Center,

    /// <summary>Diagonal blend from the top-left corner.</summary>
    DiagonalLeft,

    /// <summary>Diagonal blend from the top-right corner.</summary>
    DiagonalRight,
}
