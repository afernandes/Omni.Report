using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Sparkline</c> — miniature line / bar chart embedded in a cell. Trades full
/// chart fidelity for compactness. Rendered by <c>KpiRenderer</c>.
/// </summary>
public sealed record SparklineElement : ReportElement
{
    /// <summary>Line, column or filled-area form.</summary>
    [PropertyGrid(Category = "Mini-gráfico", Order = 1, Label = "Tipo")]
    public SparklineKind Kind { get; init; } = SparklineKind.Line;
    /// <summary>Data source providing the trend series. Each row contributes one point.</summary>
    [PropertyGrid(Category = "Mini-gráfico", Order = 4, Label = "Fonte", Placeholder = "(fonte primária)")]
    public string? DataSetName { get; init; }
    /// <summary>Expression evaluated per row to produce the value of each point.</summary>
    [PropertyGrid(Category = "Mini-gráfico", Order = 2, Label = "Valor", Placeholder = "Fields.Total")]
    public string ValueExpression { get; init; } = "Fields.Value";
    /// <summary>Optional expression labelling each point along the axis. Null leaves points unlabelled.</summary>
    [PropertyGrid(Category = "Mini-gráfico", Order = 3, Label = "Categoria")]
    public string? CategoryExpression { get; init; }
}
