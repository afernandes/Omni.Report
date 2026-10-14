using Reporting.Geometry;

namespace Reporting.Paper;

/// <summary>Physical paper size in device-independent units (mils).</summary>
/// <param name="Name">Identifier of the size, e.g. <c>"A4"</c>. Round-trips through serialization.</param>
/// <param name="Width">Width in portrait orientation.</param>
/// <param name="Height">Height in portrait orientation. Zero means an endless roll — see
/// <see cref="PageSetup.IsContinuous"/>.</param>
public sealed record PaperSize(string Name, Unit Width, Unit Height)
{
    /// <summary>ISO A4 — 210 x 297 mm.</summary>
    public static readonly PaperSize A4 = new("A4", Unit.FromMm(210), Unit.FromMm(297));

    /// <summary>ISO A5 — 148 x 210 mm.</summary>
    public static readonly PaperSize A5 = new("A5", Unit.FromMm(148), Unit.FromMm(210));

    /// <summary>US Letter — 8.5 x 11 in.</summary>
    public static readonly PaperSize Letter = new("Letter", Unit.FromInch(8.5), Unit.FromInch(11));

    /// <summary>US Legal — 8.5 x 14 in.</summary>
    public static readonly PaperSize Legal = new("Legal", Unit.FromInch(8.5), Unit.FromInch(14));

    /// <summary>Brazilian thermal receipt roll 58mm (height is "infinite" — treated as 0 = no page break).</summary>
    public static readonly PaperSize Thermal58 = new("Thermal58", Unit.FromMm(58), Unit.Zero);

    /// <summary>Brazilian thermal receipt roll 80mm.</summary>
    public static readonly PaperSize Thermal80 = new("Thermal80", Unit.FromMm(80), Unit.Zero);

    /// <summary>The same paper with width and height swapped. Used to apply landscape orientation.</summary>
    public PaperSize Rotated() => new(Name, Height, Width);
}
