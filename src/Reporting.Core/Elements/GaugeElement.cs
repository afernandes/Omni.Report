using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Gauge</c> — radial / linear gauge showing a single value against a range.
/// Captures Min/Max/Value expressions + gauge kind. Rendered by <c>KpiRenderer</c> (radial arc / linear track).
/// </summary>
public sealed record GaugeElement : ReportElement
{
    /// <summary>Radial dial or linear track.</summary>
    [PropertyGrid(Category = "Medidor", Order = 1, Label = "Tipo")]
    public GaugeKind Kind { get; init; } = GaugeKind.Radial;
    /// <summary>Expression for the low end of the scale.</summary>
    [PropertyGrid(Category = "Medidor", Order = 3, Label = "Mínimo")]
    public string MinimumExpression { get; init; } = "0";
    /// <summary>Expression for the high end of the scale.</summary>
    [PropertyGrid(Category = "Medidor", Order = 4, Label = "Máximo")]
    public string MaximumExpression { get; init; } = "100";
    /// <summary>Expression for the value the needle points at. Clamped to the min/max range.</summary>
    [PropertyGrid(Category = "Medidor", Order = 2, Label = "Valor", Placeholder = "Fields.Velocidade")]
    public string ValueExpression { get; init; } = "0";
    /// <summary>Optional banded ranges (red/yellow/green zones). Each entry is
    /// (start expr, end expr, color hex).</summary>
    [PropertyGrid(Category = "Medidor", Order = 5, Label = "Faixas", Editor = "list")]
    public EquatableArray<GaugeRange> Ranges { get; init; } = EquatableArray<GaugeRange>.Empty;
}
