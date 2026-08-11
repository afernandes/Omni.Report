using Reporting.Common;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>The plot type of a <see cref="ChartElement"/> — bar, line, pie, area, scatter, radar, bubble, or stock.</summary>
public enum ChartKind
{
    /// <summary>Vertical bars, one per category and series.</summary>
    Bar,

    /// <summary>A polyline per series.</summary>
    Line,

    /// <summary>A single-series circle divided into proportional slices.</summary>
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
/// <param name="Name">Series name, shown in the legend.</param>
/// <param name="CategoryExpression">Expression producing the category (X value) of each point.</param>
/// <param name="ValueExpression">Expression producing the value (Y value) of each point.</param>
/// <param name="Color">Series colour. Null lets the renderer pick from its palette.</param>
/// <param name="SizeExpression">Bubble radius. Ignored by other chart kinds.</param>
/// <param name="HighExpression">Top of the stock range bar. Ignored by other chart kinds.</param>
/// <param name="LowExpression">Bottom of the stock range bar. Ignored by other chart kinds.</param>
public sealed record ChartSeries(
    [property: PropertyGrid(Order = 1, Label = "Nome")] string Name,
    [property: PropertyGrid(Order = 2, Label = "Categoria")] string CategoryExpression,
    [property: PropertyGrid(Order = 3, Label = "Valor")] string ValueExpression,
    [property: PropertyGrid(Order = 4, Label = "Cor")] Color? Color = null,
    [property: PropertyGrid(Order = 5, Label = "Tamanho (bubble)")] string? SizeExpression = null,
    [property: PropertyGrid(Order = 6, Label = "Alta (stock)")] string? HighExpression = null,
    [property: PropertyGrid(Order = 7, Label = "Baixa (stock)")] string? LowExpression = null);

/// <summary>RDL <c>Chart</c> — a data visualisation of one or more <see cref="ChartSeries"/> rendered by
/// <c>ChartRenderer</c>, with a configurable <see cref="Kind"/>, optional title, and legend.</summary>
public sealed record ChartElement : ReportElement
{
    /// <summary>The plot type. Determines which of the series expressions are honoured.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 1, Label = "Tipo")]
    public ChartKind Kind { get; init; } = ChartKind.Bar;

    /// <summary>Optional caption drawn above the plot area. Null draws no title.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 2, Label = "Título")]
    public string? Title { get; init; }

    /// <summary>Whether to draw the series legend.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 3, Label = "Legenda")]
    public bool ShowLegend { get; init; } = true;

    /// <summary>The series plotted. An empty collection renders an empty plot area.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 4, Label = "Séries", Editor = "list")]
    public EquatableArray<ChartSeries> Series { get; init; } = EquatableArray<ChartSeries>.Empty;
}
