namespace Reporting.Geometry;

/// <summary>A 2D size in device-independent units.</summary>
/// <param name="Width">Horizontal extent.</param>
/// <param name="Height">Vertical extent.</param>
public readonly record struct Size(Unit Width, Unit Height)
{
    /// <summary>A size of zero by zero.</summary>
    public static readonly Size Empty = new(Unit.Zero, Unit.Zero);

    /// <summary>True when <em>both</em> extents are zero. A zero-height line with a real width is not
    /// empty — it still draws.</summary>
    public bool IsEmpty => Width == Unit.Zero && Height == Unit.Zero;

    /// <summary>Adds two sizes component-wise.</summary>
    public static Size operator +(Size a, Size b) => new(a.Width + b.Width, a.Height + b.Height);

    /// <summary>Subtracts two sizes component-wise. Components may go negative.</summary>
    public static Size operator -(Size a, Size b) => new(a.Width - b.Width, a.Height - b.Height);
}
