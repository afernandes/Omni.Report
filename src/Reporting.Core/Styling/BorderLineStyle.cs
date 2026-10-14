namespace Reporting.Styling;

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
