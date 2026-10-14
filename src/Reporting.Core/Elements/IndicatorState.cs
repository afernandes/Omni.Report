using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>One state of an <see cref="IndicatorElement"/> — the value range (start/end expressions) that selects a given icon.</summary>
/// <param name="StartExpression">Expression for the low bound of the range that selects this state.</param>
/// <param name="EndExpression">Expression for the high bound.</param>
/// <param name="IconName">Icon drawn while the value falls in the range.</param>
public sealed record IndicatorState(
    [property: PropertyGrid(Order = 1, Label = "Início")] string StartExpression,
    [property: PropertyGrid(Order = 2, Label = "Fim")] string EndExpression,
    [property: PropertyGrid(Order = 3, Label = "Ícone")] string IconName);
