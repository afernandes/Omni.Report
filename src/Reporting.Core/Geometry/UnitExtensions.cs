using System.Globalization;

namespace Reporting.Geometry;

/// <summary>Fluent helpers for declaring units in code-first reports.</summary>
public static class UnitExtensions
{
    /// <summary>Millimeters, e.g. <c>15.Mm()</c>.</summary>
    public static Unit Mm(this int value) => Unit.FromMm(value);

    /// <summary>Millimeters, e.g. <c>15.5.Mm()</c>.</summary>
    public static Unit Mm(this double value) => Unit.FromMm(value);

    /// <summary>Centimeters, e.g. <c>2.Cm()</c>.</summary>
    public static Unit Cm(this int value) => Unit.FromCm(value);

    /// <summary>Centimeters, e.g. <c>2.5.Cm()</c>.</summary>
    public static Unit Cm(this double value) => Unit.FromCm(value);

    /// <summary>Inches, e.g. <c>1.Inch()</c>.</summary>
    public static Unit Inch(this int value) => Unit.FromInch(value);

    /// <summary>Inches, e.g. <c>1.5.Inch()</c>.</summary>
    public static Unit Inch(this double value) => Unit.FromInch(value);

    /// <summary>Typographic points, e.g. <c>12.Pt()</c>.</summary>
    public static Unit Pt(this int value) => Unit.FromPoint(value);

    /// <summary>Typographic points, e.g. <c>10.5.Pt()</c>.</summary>
    public static Unit Pt(this double value) => Unit.FromPoint(value);
}
