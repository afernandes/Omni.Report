using Reporting.Common;
using Reporting.Styling;

namespace Reporting.Elements;

public enum ChartKind
{
    Bar,
    Line,
    Pie,
    /// <summary>Line chart with the area below each series filled (translucent).</summary>
    Area,
    /// <summary>Point/marker plot — an ellipse per (category, value), no connecting line.</summary>
    Scatter,
    /// <summary>Polar plot — categories on radial axes, value as radius; each series a closed web.</summary>
    Radar,
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
