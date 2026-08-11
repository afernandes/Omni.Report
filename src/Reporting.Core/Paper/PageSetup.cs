using Reporting.Geometry;

namespace Reporting.Paper;

/// <summary>Page orientation — whether the paper is taller than wide (portrait) or wider than tall (landscape).</summary>
public enum Orientation
{
    /// <summary>Taller than wide — the paper is used in its declared orientation.</summary>
    Portrait,

    /// <summary>Wider than tall — width and height are swapped.</summary>
    Landscape,
}

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

/// <summary>Page layout for a report: paper size, orientation, margins, and multi-column flow.</summary>
/// <param name="Paper">The physical sheet.</param>
/// <param name="Orientation">Whether the sheet is rotated.</param>
/// <param name="Margins">Space reserved on each side; content is laid out inside them.</param>
/// <param name="Columns">Number of newspaper-style columns the content flows through. 1 = single column.</param>
/// <param name="ColumnSpacing">Gutter between adjacent columns. Ignored when <paramref name="Columns"/> is 1.</param>
public sealed record PageSetup(
    PaperSize Paper,
    Orientation Orientation = Orientation.Portrait,
    Thickness Margins = default,
    int Columns = 1,
    Unit ColumnSpacing = default)
{
    /// <summary>A4 portrait with uniform 20 mm margins — a sane default for a new report.</summary>
    public static readonly PageSetup A4Portrait = new(
        PaperSize.A4,
        Orientation.Portrait,
        Thickness.Uniform(Unit.FromMm(20)));

    /// <summary>Effective page width after orientation but before margins.</summary>
    public Unit PageWidth => Orientation == Orientation.Portrait ? Paper.Width : Paper.Height;

    /// <summary>Effective page height after orientation but before margins.</summary>
    public Unit PageHeight => Orientation == Orientation.Portrait ? Paper.Height : Paper.Width;

    /// <summary>Usable width — page width minus the left and right margins.</summary>
    public Unit ContentWidth => PageWidth - Margins.Horizontal;

    /// <summary>Usable height — page height minus the top and bottom margins.</summary>
    public Unit ContentHeight => PageHeight - Margins.Vertical;

    /// <summary>True for an endless roll (a thermal receipt), signalled by a paper height of zero. The
    /// paginator never breaks such a page; the sheet grows to fit the content.</summary>
    public bool IsContinuous => Paper.Height == Unit.Zero;
}
