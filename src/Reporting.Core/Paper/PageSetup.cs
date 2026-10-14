using Reporting.Geometry;

namespace Reporting.Paper;

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
