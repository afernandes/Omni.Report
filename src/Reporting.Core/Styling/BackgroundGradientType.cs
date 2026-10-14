namespace Reporting.Styling;

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
