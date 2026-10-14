using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Chart with its series list.</summary>
internal sealed class ChartFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Chart];
    internal override bool Owns(ReportElement element) => element is ChartElement;

    internal override ReportElement Build(ElementViewModel vm) => new ChartElement
    {
        Bounds = vm.Bounds,
        Kind = vm.ChartKind,
        Title = string.IsNullOrWhiteSpace(vm.ChartTitle) ? null : vm.ChartTitle,
        ShowLegend = vm.ShowLegend,
        Series = vm.ChartSeries.Count == 0
            ? EquatableArray<ChartSeries>.Empty
            : new EquatableArray<ChartSeries>(vm.ChartSeries.Select(s => s.ToSeries())),
    };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var chart = (ChartElement)element;
        vm.ChartKind = chart.Kind;
        vm.ChartTitle = chart.Title ?? string.Empty;
        vm.ShowLegend = chart.ShowLegend;
        foreach (var series in chart.Series)
        {
            vm.ChartSeries.Add(ChartSeriesRule.From(series));
        }
    }
}
