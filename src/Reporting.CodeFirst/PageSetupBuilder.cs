using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.CodeFirst;

/// <summary>Fluent builder for <see cref="PageSetup"/>.</summary>
public sealed class PageSetupBuilder
{
    private PaperSize _paper = PaperSize.A4;
    private Orientation _orientation = Orientation.Portrait;
    private Thickness _margins = Thickness.Uniform(Unit.FromMm(15));
    private int _columns = 1;
    private Unit _columnSpacing = Unit.Zero;

    /// <summary>Sets the page's paper size and returns the builder for chaining.</summary>
    public PageSetupBuilder Paper(PaperSize paper) { _paper = paper; return this; }
    /// <summary>Sets the paper size to A4 (210 x 297 mm).</summary>
    public PageSetupBuilder A4() => Paper(PaperSize.A4);
    /// <summary>Sets the paper size to A5 (148 x 210 mm).</summary>
    public PageSetupBuilder A5() => Paper(PaperSize.A5);
    /// <summary>Sets the paper size to US Letter (8.5 x 11 in).</summary>
    public PageSetupBuilder Letter() => Paper(PaperSize.Letter);
    /// <summary>Sets the paper size to US Legal (8.5 x 14 in).</summary>
    public PageSetupBuilder Legal() => Paper(PaperSize.Legal);
    /// <summary>Sets the paper size to 58 mm thermal receipt roll.</summary>
    public PageSetupBuilder Thermal58() => Paper(PaperSize.Thermal58);
    /// <summary>Sets the paper size to 80 mm thermal receipt roll.</summary>
    public PageSetupBuilder Thermal80() => Paper(PaperSize.Thermal80);
    /// <summary>Sets a custom paper size with the given name and dimensions in millimeters.</summary>
    public PageSetupBuilder CustomPaper(string name, double widthMm, double heightMm)
        => Paper(new PaperSize(name, Unit.FromMm(widthMm), Unit.FromMm(heightMm)));

    /// <summary>Sets the page orientation to portrait.</summary>
    public PageSetupBuilder Portrait() { _orientation = Orientation.Portrait; return this; }
    /// <summary>Sets the page orientation to landscape.</summary>
    public PageSetupBuilder Landscape() { _orientation = Orientation.Landscape; return this; }

    /// <summary>Sets uniform margins in millimeters.</summary>
    public PageSetupBuilder Margins(double mm)
        => Margins(mm, mm, mm, mm);

    /// <summary>Sets margins in millimeters (top, right, bottom, left).</summary>
    public PageSetupBuilder Margins(double top, double right, double bottom, double left)
    {
        _margins = new Thickness(Unit.FromMm(left), Unit.FromMm(top), Unit.FromMm(right), Unit.FromMm(bottom));
        return this;
    }

    /// <summary>Lays the page out in multiple newspaper-style columns, with the given spacing in millimeters between them. A count below 1 is clamped to a single column.</summary>
    /// <param name="columns">Number of columns; values less than 1 are treated as 1.</param>
    /// <param name="spacingMm">Gap between adjacent columns, in millimeters.</param>
    public PageSetupBuilder Columns(int columns, double spacingMm = 5)
    {
        _columns = Math.Max(1, columns);
        _columnSpacing = Unit.FromMm(spacingMm);
        return this;
    }

    internal PageSetup Build() => new(_paper, _orientation, _margins, _columns, _columnSpacing);
}
