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
