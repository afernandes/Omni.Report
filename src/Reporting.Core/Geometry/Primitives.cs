namespace Reporting.Geometry;

/// <summary>A 2D point in device-independent units.</summary>
/// <param name="X">Horizontal position, increasing to the right.</param>
/// <param name="Y">Vertical position, increasing <em>downwards</em> — page coordinates, not math ones.</param>
public readonly record struct Point(Unit X, Unit Y)
{
    /// <summary>The top-left corner, <c>(0, 0)</c>.</summary>
    public static readonly Point Origin = new(Unit.Zero, Unit.Zero);

    /// <summary>Offsets a point by a size — moves right by the width and down by the height.</summary>
    public static Point operator +(Point p, Size s) => new(p.X + s.Width, p.Y + s.Height);

    /// <summary>Offsets a point back by a size — moves left and up.</summary>
    public static Point operator -(Point p, Size s) => new(p.X - s.Width, p.Y - s.Height);

    /// <summary>The displacement from <paramref name="b"/> to <paramref name="a"/>. Components may be
    /// negative when <paramref name="a"/> is above or to the left of <paramref name="b"/>.</summary>
    public static Size operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
}

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

/// <summary>An axis-aligned rectangle in device-independent units.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge (Y grows downwards).</param>
/// <param name="Width">Horizontal extent from <paramref name="X"/>.</param>
/// <param name="Height">Vertical extent from <paramref name="Y"/>.</param>
public readonly record struct Rectangle(Unit X, Unit Y, Unit Width, Unit Height)
{
    /// <summary>A rectangle at the origin with no extent.</summary>
    public static readonly Rectangle Empty = new(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero);

    /// <summary>The top-left corner.</summary>
    public Point Location => new(X, Y);

    /// <summary>The extent, without the position.</summary>
    public Size Size => new(Width, Height);

    /// <summary>The right edge — <c>X + Width</c>.</summary>
    public Unit Right => X + Width;

    /// <summary>The bottom edge — <c>Y + Height</c>.</summary>
    public Unit Bottom => Y + Height;

    /// <summary>Builds a rectangle from a corner and an extent.</summary>
    public static Rectangle FromLocationSize(Point location, Size size)
        => new(location.X, location.Y, size.Width, size.Height);

    /// <summary>True when <paramref name="p"/> lies inside the rectangle. Edges count as inside.</summary>
    public bool Contains(Point p)
        => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    /// <summary>True when the two rectangles overlap. Touching edges count as intersecting.</summary>
    public bool IntersectsWith(Rectangle other)
        => !(other.X > Right || other.Right < X || other.Y > Bottom || other.Bottom < Y);
}

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
