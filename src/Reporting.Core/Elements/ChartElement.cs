using Reporting.Common;
using Reporting.Styling;

namespace Reporting.Elements;

public enum ChartKind
{
    Bar,
    Line,
    Pie,
}

public sealed record ChartSeries(
    string Name,
    string CategoryExpression,
    string ValueExpression,
    Color? Color = null);

public sealed record ChartElement : ReportElement
{
    public ChartKind Kind { get; init; } = ChartKind.Bar;
    public string? Title { get; init; }
    public bool ShowLegend { get; init; } = true;
    public EquatableArray<ChartSeries> Series { get; init; } = EquatableArray<ChartSeries>.Empty;
}
