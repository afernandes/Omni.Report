using Reporting.Common;
using Reporting.Metadata;
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
    [property: PropertyGrid(Order = 1, Label = "Nome")] string Name,
    [property: PropertyGrid(Order = 2, Label = "Categoria")] string CategoryExpression,
    [property: PropertyGrid(Order = 3, Label = "Valor")] string ValueExpression,
    [property: PropertyGrid(Order = 4, Label = "Cor")] Color? Color = null,
    [property: PropertyGrid(Order = 5, Label = "Tamanho (bubble)")] string? SizeExpression = null,
    [property: PropertyGrid(Order = 6, Label = "Alta (stock)")] string? HighExpression = null,
    [property: PropertyGrid(Order = 7, Label = "Baixa (stock)")] string? LowExpression = null);

public sealed record ChartElement : ReportElement
{
    [PropertyGrid(Category = "Gráfico", Order = 1, Label = "Tipo")]
    public ChartKind Kind { get; init; } = ChartKind.Bar;
    [PropertyGrid(Category = "Gráfico", Order = 2, Label = "Título")]
    public string? Title { get; init; }
    [PropertyGrid(Category = "Gráfico", Order = 3, Label = "Legenda")]
    public bool ShowLegend { get; init; } = true;
    [PropertyGrid(Category = "Gráfico", Order = 4, Label = "Séries", Editor = "list")]
    public EquatableArray<ChartSeries> Series { get; init; } = EquatableArray<ChartSeries>.Empty;
}
