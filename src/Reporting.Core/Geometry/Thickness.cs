namespace Reporting.Geometry;

/// <summary>Per-side spacing — used for margins, padding, borders.</summary>
/// <param name="Left">Spacing on the left side.</param>
/// <param name="Top">Spacing on the top side.</param>
/// <param name="Right">Spacing on the right side.</param>
/// <param name="Bottom">Spacing on the bottom side.</param>
public readonly record struct Thickness(Unit Left, Unit Top, Unit Right, Unit Bottom)
{
    /// <summary>No spacing on any side.</summary>
    public static readonly Thickness Zero = new(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero);

    /// <summary>The same spacing on all four sides.</summary>
    public static Thickness Uniform(Unit value) => new(value, value, value, value);

    /// <summary>One spacing left/right and another top/bottom — the common page-margin shape.</summary>
    public static Thickness Symmetric(Unit horizontal, Unit vertical) => new(horizontal, vertical, horizontal, vertical);

    /// <summary>Total horizontal spacing — <c>Left + Right</c>. Subtract from a width to get the content box.</summary>
    public Unit Horizontal => Left + Right;

    /// <summary>Total vertical spacing — <c>Top + Bottom</c>. Subtract from a height to get the content box.</summary>
    public Unit Vertical => Top + Bottom;
}
