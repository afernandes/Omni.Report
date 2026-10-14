using Reporting.Common;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

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
