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

    public PageSetupBuilder Paper(PaperSize paper) { _paper = paper; return this; }
    public PageSetupBuilder A4() => Paper(PaperSize.A4);
    public PageSetupBuilder A5() => Paper(PaperSize.A5);
    public PageSetupBuilder Letter() => Paper(PaperSize.Letter);
    public PageSetupBuilder Legal() => Paper(PaperSize.Legal);
    public PageSetupBuilder Thermal58() => Paper(PaperSize.Thermal58);
    public PageSetupBuilder Thermal80() => Paper(PaperSize.Thermal80);
    public PageSetupBuilder CustomPaper(string name, double widthMm, double heightMm)
        => Paper(new PaperSize(name, Unit.FromMm(widthMm), Unit.FromMm(heightMm)));

    public PageSetupBuilder Portrait() { _orientation = Orientation.Portrait; return this; }
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

    public PageSetupBuilder Columns(int columns, double spacingMm = 5)
    {
        _columns = Math.Max(1, columns);
        _columnSpacing = Unit.FromMm(spacingMm);
        return this;
    }

    internal PageSetup Build() => new(_paper, _orientation, _margins, _columns, _columnSpacing);
}
