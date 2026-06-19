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
    /// <summary>Scatter with a third dimension — each marker is sized by <see cref="ChartSeries.SizeExpression"/>.</summary>
    Bubble,
    /// <summary>High-low(-close) range chart — a vertical bar from <see cref="ChartSeries.LowExpression"/>
    /// to <see cref="ChartSeries.HighExpression"/> per category, with a close tick at the value.</summary>
    Stock,
}

/// <summary>One chart series. <c>SizeExpression</c> drives bubble radii; <c>HighExpression</c>/
/// <c>LowExpression</c> drive the stock range bar — all optional and ignored by other kinds.</summary>
public sealed record ChartSeries(
    string Name,
    string CategoryExpression,
    string ValueExpression,
    Color? Color = null,
    string? SizeExpression = null,
    string? HighExpression = null,
    string? LowExpression = null);

public sealed record ChartElement : ReportElement
{
    public ChartKind Kind { get; init; } = ChartKind.Bar;
    public string? Title { get; init; }
    public bool ShowLegend { get; init; } = true;
    public EquatableArray<ChartSeries> Series { get; init; } = EquatableArray<ChartSeries>.Empty;
}
