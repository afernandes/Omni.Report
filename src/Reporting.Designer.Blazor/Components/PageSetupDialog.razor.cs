using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Icons;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Printing;
using System.Globalization;
namespace Reporting.Designer.Blazor.Components;

public partial class PageSetupDialog
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public PageSetup InitialPageSetup { get; set; } = PageSetup.A4Portrait;
    [Parameter] public EventCallback<PageSetup> OnApplied { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }

    private static readonly PaperSize[] Presets = new[]
    {
        PaperSize.A4, PaperSize.A5, PaperSize.Letter, PaperSize.Legal,
        PaperSize.Thermal58, PaperSize.Thermal80,
    };

    private string _paperName = "A4";
    private double _widthMm = 210;
    private double _heightMm = 297;
    private Orientation _orientation = Orientation.Portrait;
    private double _marginTop, _marginRight, _marginBottom, _marginLeft;
    private int _columns = 1;
    private double _columnSpacing;

    protected override void OnParametersSet()
    {
        var ps = InitialPageSetup;
        var preset = Array.Find(Presets, p =>
            Math.Abs(p.Width.ToMm() - ps.Paper.Width.ToMm()) < 0.5 &&
            Math.Abs(p.Height.ToMm() - ps.Paper.Height.ToMm()) < 0.5);
        _paperName = preset?.Name ?? "Custom";
        _widthMm = ps.Paper.Width.ToMm();
        _heightMm = ps.Paper.Height.ToMm();
        _orientation = ps.Orientation;
        _marginTop = ps.Margins.Top.ToMm();
        _marginRight = ps.Margins.Right.ToMm();
        _marginBottom = ps.Margins.Bottom.ToMm();
        _marginLeft = ps.Margins.Left.ToMm();
        _columns = ps.Columns;
        _columnSpacing = ps.ColumnSpacing.ToMm();
    }

    private void OnPaperChanged(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        _paperName = name;
        var preset = Array.Find(Presets, p => p.Name == name);
        if (preset is not null)
        {
            _widthMm = preset.Width.ToMm();
            _heightMm = preset.Height.ToMm();
        }
    }

    private static string FormatSize(PaperSize p)
    {
        var w = p.Width.ToMm().ToString("F0", CultureInfo.InvariantCulture);
        if (p.Height == Reporting.Geometry.Unit.Zero)
        {
            return $"{w} × cont. mm";
        }
        var h = p.Height.ToMm().ToString("F0", CultureInfo.InvariantCulture);
        return $"{w} × {h} mm";
    }

    private static double ParseMm(string? raw, double fallback)
        => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private async Task OnApply()
    {
        var paper = new PaperSize(_paperName == "Custom" ? "Custom" : _paperName,
            Reporting.Geometry.Unit.FromMm(_widthMm),
            Reporting.Geometry.Unit.FromMm(_heightMm));
        var margins = new Reporting.Geometry.Thickness(
            Reporting.Geometry.Unit.FromMm(_marginLeft),
            Reporting.Geometry.Unit.FromMm(_marginTop),
            Reporting.Geometry.Unit.FromMm(_marginRight),
            Reporting.Geometry.Unit.FromMm(_marginBottom));
        var ps = new PageSetup(paper, _orientation, margins, _columns,
            Reporting.Geometry.Unit.FromMm(_columnSpacing));
        await OnApplied.InvokeAsync(ps);
    }

    private Task OnCancel() => OnClosed.InvokeAsync();
}
