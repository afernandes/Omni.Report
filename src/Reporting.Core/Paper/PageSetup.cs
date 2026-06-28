using Reporting.Geometry;

namespace Reporting.Paper;

/// <summary>Page orientation — whether the paper is taller than wide (portrait) or wider than tall (landscape).</summary>
public enum Orientation
{
    Portrait,
    Landscape,
}

/// <summary>Physical paper size in device-independent units (mils).</summary>
public sealed record PaperSize(string Name, Unit Width, Unit Height)
{
    public static readonly PaperSize A4 = new("A4", Unit.FromMm(210), Unit.FromMm(297));
    public static readonly PaperSize A5 = new("A5", Unit.FromMm(148), Unit.FromMm(210));
    public static readonly PaperSize Letter = new("Letter", Unit.FromInch(8.5), Unit.FromInch(11));
    public static readonly PaperSize Legal = new("Legal", Unit.FromInch(8.5), Unit.FromInch(14));

    /// <summary>Brazilian thermal receipt roll 58mm (height is "infinite" — treated as 0 = no page break).</summary>
    public static readonly PaperSize Thermal58 = new("Thermal58", Unit.FromMm(58), Unit.Zero);

    /// <summary>Brazilian thermal receipt roll 80mm.</summary>
    public static readonly PaperSize Thermal80 = new("Thermal80", Unit.FromMm(80), Unit.Zero);

    public PaperSize Rotated() => new(Name, Height, Width);
}

/// <summary>Page layout for a report: paper size, orientation, margins, and multi-column flow.</summary>
public sealed record PageSetup(
    PaperSize Paper,
    Orientation Orientation = Orientation.Portrait,
    Thickness Margins = default,
    int Columns = 1,
    Unit ColumnSpacing = default)
{
    public static readonly PageSetup A4Portrait = new(
        PaperSize.A4,
        Orientation.Portrait,
        Thickness.Uniform(Unit.FromMm(20)));

    /// <summary>Effective page width after orientation but before margins.</summary>
    public Unit PageWidth => Orientation == Orientation.Portrait ? Paper.Width : Paper.Height;

    /// <summary>Effective page height after orientation but before margins.</summary>
    public Unit PageHeight => Orientation == Orientation.Portrait ? Paper.Height : Paper.Width;

    public Unit ContentWidth => PageWidth - Margins.Horizontal;
    public Unit ContentHeight => PageHeight - Margins.Vertical;

    public bool IsContinuous => Paper.Height == Unit.Zero;
}
