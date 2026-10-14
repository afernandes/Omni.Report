using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>A coloured band of a <see cref="GaugeElement"/> — a value range (start/end expressions) painted in a given colour.</summary>
/// <param name="StartExpression">Expression for where the band begins on the scale.</param>
/// <param name="EndExpression">Expression for where the band ends.</param>
/// <param name="ColorHex">Fill colour of the band, as <c>#RRGGBB</c>.</param>
public sealed record GaugeRange(
    [property: PropertyGrid(Order = 1, Label = "Início")] string StartExpression,
    [property: PropertyGrid(Order = 2, Label = "Fim")] string EndExpression,
    [property: PropertyGrid(Order = 3, Label = "Cor", Editor = "color-hex")] string ColorHex);
