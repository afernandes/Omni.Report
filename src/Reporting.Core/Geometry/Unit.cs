using System.Globalization;

namespace Reporting.Geometry;

/// <summary>
/// Device-independent length expressed as an integer number of mils (1/1000 inch).
/// </summary>
/// <remarks>
/// Chosen over millimeters or floating-point so that band-stacking math is exact and
/// snap-to-grid is trivial. 1 inch = 1000 mils; 1 mm = 39.370... mils (rounded to nearest).
/// PDF/typography points (72 dpi) and GDI HiMetric coordinate naturally with integers.
/// </remarks>
/// <param name="Mils">The length in mils (1/1000 inch). Integer by design — see the type remarks.</param>
public readonly record struct Unit(int Mils) : IComparable<Unit>
{
    /// <summary>A zero-length unit. The additive identity, and the usual "unset" value.</summary>
    public static readonly Unit Zero = new(0);

    /// <summary>Creates a unit from millimeters, rounding to the nearest mil.</summary>
    public static Unit FromMm(double mm) => new((int)Math.Round(mm * 1000.0 / 25.4));

    /// <summary>Creates a unit from centimeters, rounding to the nearest mil.</summary>
    public static Unit FromCm(double cm) => FromMm(cm * 10.0);

    /// <summary>Creates a unit from inches. Exact: 1 inch = 1000 mils.</summary>
    public static Unit FromInch(double inch) => new((int)Math.Round(inch * 1000.0));

    /// <summary>Creates a unit from typographic points (72 per inch), rounding to the nearest mil.</summary>
    public static Unit FromPoint(double pt) => new((int)Math.Round(pt * 1000.0 / 72.0));

    /// <summary>Creates a unit from pixels at <paramref name="dpi"/> (96 = the CSS reference density).</summary>
    public static Unit FromPixels(double px, double dpi = 96.0) => new((int)Math.Round(px * 1000.0 / dpi));

    /// <summary>The length in millimeters.</summary>
    public double ToMm() => Mils * 25.4 / 1000.0;

    /// <summary>The length in centimeters.</summary>
    public double ToCm() => ToMm() / 10.0;

    /// <summary>The length in inches.</summary>
    public double ToInches() => Mils / 1000.0;

    /// <summary>The length in typographic points (72 per inch) — the unit PDF and font sizes use.</summary>
    public double ToPoints() => Mils * 72.0 / 1000.0;

    /// <summary>The length in pixels at <paramref name="dpi"/> (96 = the CSS reference density).</summary>
    public double ToPixels(double dpi = 96.0) => Mils * dpi / 1000.0;

    /// <summary>Orders by length. Enables sorting and the comparison operators.</summary>
    public int CompareTo(Unit other) => Mils.CompareTo(other.Mils);

    /// <summary>Adds two lengths. Exact — no floating-point drift when stacking bands.</summary>
    public static Unit operator +(Unit a, Unit b) => new(a.Mils + b.Mils);

    /// <summary>Subtracts <paramref name="b"/> from <paramref name="a"/>. May go negative.</summary>
    public static Unit operator -(Unit a, Unit b) => new(a.Mils - b.Mils);

    /// <summary>Scales a length by an integer factor. Exact.</summary>
    public static Unit operator *(Unit a, int factor) => new(a.Mils * factor);

    /// <summary>Scales a length by a fractional factor, rounding to the nearest mil.</summary>
    public static Unit operator *(Unit a, double factor) => new((int)Math.Round(a.Mils * factor));

    /// <summary>Divides a length by an integer. Truncates toward zero, like integer division.</summary>
    public static Unit operator /(Unit a, int divisor) => new(a.Mils / divisor);

    /// <summary>Negates a length — useful for offsets that move up or left.</summary>
    public static Unit operator -(Unit a) => new(-a.Mils);

    /// <summary>True when <paramref name="a"/> is shorter than <paramref name="b"/>.</summary>
    public static bool operator <(Unit a, Unit b) => a.Mils < b.Mils;

    /// <summary>True when <paramref name="a"/> is longer than <paramref name="b"/>.</summary>
    public static bool operator >(Unit a, Unit b) => a.Mils > b.Mils;

    /// <summary>True when <paramref name="a"/> is no longer than <paramref name="b"/>.</summary>
    public static bool operator <=(Unit a, Unit b) => a.Mils <= b.Mils;

    /// <summary>True when <paramref name="a"/> is at least as long as <paramref name="b"/>.</summary>
    public static bool operator >=(Unit a, Unit b) => a.Mils >= b.Mils;

    /// <summary>Millimeters with two decimals, e.g. <c>"29.70mm"</c>. Invariant culture, so the text is
    /// stable across machines — diagnostics and golden files depend on that.</summary>
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{ToMm():F2}mm");
}
