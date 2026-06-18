namespace Reporting.Geometry;

/// <summary>A 2D point in device-independent units.</summary>
public readonly record struct Point(Unit X, Unit Y)
{
    public static readonly Point Origin = new(Unit.Zero, Unit.Zero);

    public static Point operator +(Point p, Size s) => new(p.X + s.Width, p.Y + s.Height);
    public static Point operator -(Point p, Size s) => new(p.X - s.Width, p.Y - s.Height);
    public static Size operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
}

/// <summary>A 2D size in device-independent units.</summary>
public readonly record struct Size(Unit Width, Unit Height)
{
    public static readonly Size Empty = new(Unit.Zero, Unit.Zero);

    public bool IsEmpty => Width == Unit.Zero && Height == Unit.Zero;

    public static Size operator +(Size a, Size b) => new(a.Width + b.Width, a.Height + b.Height);
    public static Size operator -(Size a, Size b) => new(a.Width - b.Width, a.Height - b.Height);
}

/// <summary>An axis-aligned rectangle in device-independent units.</summary>
public readonly record struct Rectangle(Unit X, Unit Y, Unit Width, Unit Height)
{
    public static readonly Rectangle Empty = new(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero);

    public Point Location => new(X, Y);
    public Size Size => new(Width, Height);
    public Unit Right => X + Width;
    public Unit Bottom => Y + Height;

    public static Rectangle FromLocationSize(Point location, Size size)
        => new(location.X, location.Y, size.Width, size.Height);

    public bool Contains(Point p)
        => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public bool IntersectsWith(Rectangle other)
        => !(other.X > Right || other.Right < X || other.Y > Bottom || other.Bottom < Y);
}

/// <summary>Per-side spacing — used for margins, padding, borders.</summary>
public readonly record struct Thickness(Unit Left, Unit Top, Unit Right, Unit Bottom)
{
    public static readonly Thickness Zero = new(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero);

    public static Thickness Uniform(Unit value) => new(value, value, value, value);
    public static Thickness Symmetric(Unit horizontal, Unit vertical) => new(horizontal, vertical, horizontal, vertical);

    public Unit Horizontal => Left + Right;
    public Unit Vertical => Top + Bottom;
}
