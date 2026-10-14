using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Indicator</c> — KPI icon (up arrow / star / signal bars) that swaps
/// visualisation based on which "state" range the value falls into. Rendered by <c>KpiRenderer</c>.
/// </summary>
public sealed record IndicatorElement : ReportElement
{
    /// <summary>Which icon family the states pick from.</summary>
    [PropertyGrid(Category = "Indicador", Order = 1, Label = "Tipo")]
    public IndicatorKind Kind { get; init; } = IndicatorKind.DirectionalArrow;
    /// <summary>Expression whose result selects the matching state.</summary>
    [PropertyGrid(Category = "Indicador", Order = 2, Label = "Valor", Placeholder = "Fields.Meta")]
    public string ValueExpression { get; init; } = "0";
    /// <summary>State boundaries — each (start, end, iconName).</summary>
    [PropertyGrid(Category = "Indicador", Order = 3, Label = "Estados", Editor = "list")]
    public EquatableArray<IndicatorState> States { get; init; } = EquatableArray<IndicatorState>.Empty;
}
