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
public readonly record struct Unit(int Mils) : IComparable<Unit>
{
    public static readonly Unit Zero = new(0);

    public static Unit FromMm(double mm) => new((int)Math.Round(mm * 1000.0 / 25.4));
    public static Unit FromCm(double cm) => FromMm(cm * 10.0);
    public static Unit FromInch(double inch) => new((int)Math.Round(inch * 1000.0));
    public static Unit FromPoint(double pt) => new((int)Math.Round(pt * 1000.0 / 72.0));
    public static Unit FromPixels(double px, double dpi = 96.0) => new((int)Math.Round(px * 1000.0 / dpi));

    public double ToMm() => Mils * 25.4 / 1000.0;
    public double ToCm() => ToMm() / 10.0;
    public double ToInches() => Mils / 1000.0;
    public double ToPoints() => Mils * 72.0 / 1000.0;
    public double ToPixels(double dpi = 96.0) => Mils * dpi / 1000.0;

    public int CompareTo(Unit other) => Mils.CompareTo(other.Mils);

    public static Unit operator +(Unit a, Unit b) => new(a.Mils + b.Mils);
    public static Unit operator -(Unit a, Unit b) => new(a.Mils - b.Mils);
    public static Unit operator *(Unit a, int factor) => new(a.Mils * factor);
    public static Unit operator *(Unit a, double factor) => new((int)Math.Round(a.Mils * factor));
    public static Unit operator /(Unit a, int divisor) => new(a.Mils / divisor);
    public static Unit operator -(Unit a) => new(-a.Mils);

    public static bool operator <(Unit a, Unit b) => a.Mils < b.Mils;
    public static bool operator >(Unit a, Unit b) => a.Mils > b.Mils;
    public static bool operator <=(Unit a, Unit b) => a.Mils <= b.Mils;
    public static bool operator >=(Unit a, Unit b) => a.Mils >= b.Mils;

    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{ToMm():F2}mm");
}

/// <summary>Fluent helpers for declaring units in code-first reports.</summary>
public static class UnitExtensions
{
    public static Unit Mm(this int value) => Unit.FromMm(value);
    public static Unit Mm(this double value) => Unit.FromMm(value);
    public static Unit Cm(this int value) => Unit.FromCm(value);
    public static Unit Cm(this double value) => Unit.FromCm(value);
    public static Unit Inch(this int value) => Unit.FromInch(value);
    public static Unit Inch(this double value) => Unit.FromInch(value);
    public static Unit Pt(this int value) => Unit.FromPoint(value);
    public static Unit Pt(this double value) => Unit.FromPoint(value);
}
