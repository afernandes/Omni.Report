namespace Reporting.Geometry;

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
