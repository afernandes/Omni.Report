using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>DataBar</c> — horizontal bar that fills proportionally to <see cref="ValueExpression"/>.
/// Typically appears inside a Tablix cell to highlight a metric. Rendered by <c>KpiRenderer</c>.
/// </summary>
public sealed record DataBarElement : ReportElement
{
    /// <summary>Expression for the value that sets the fill length.</summary>
    [PropertyGrid(Category = "Barra de dados", Order = 1, Label = "Valor", Placeholder = "Fields.Total")]
    public string ValueExpression { get; init; } = "0";
    /// <summary>Expression for the value that maps to an empty bar.</summary>
    [PropertyGrid(Category = "Barra de dados", Order = 2, Label = "Mínimo")]
    public string MinimumExpression { get; init; } = "0";
    /// <summary>Expression for the value that maps to a full bar.</summary>
    [PropertyGrid(Category = "Barra de dados", Order = 3, Label = "Máximo")]
    public string MaximumExpression { get; init; } = "100";
    /// <summary>Fill colour expression (hex literal or expression returning hex).</summary>
    [PropertyGrid(Category = "Barra de dados", Order = 4, Label = "Preenchimento", Editor = "color-hex")]
    public string FillColor { get; init; } = "#C2410C";
}
