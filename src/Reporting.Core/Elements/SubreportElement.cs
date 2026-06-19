using Reporting.Common;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>Embeds another <c>ReportDefinition</c> at this element's bounds.</summary>
public sealed record SubreportElement : ReportElement
{
    /// <summary>Reference to a child report — resolved by ID against a registry,
    /// or inline by setting <see cref="InlineDefinition"/> instead.</summary>
    [PropertyGrid(Category = "Sub-relatório", Order = 1, Label = "Report ID", Placeholder = "nome no registro de reports")]
    public string? ReportId { get; init; }

    /// <summary>Inline report definition (mutually exclusive with <see cref="ReportId"/>).</summary>
    public ReportDefinition? InlineDefinition { get; init; }

    /// <summary>Parameter expressions passed to the child report (key = child parameter name).</summary>
    [PropertyGrid(Category = "Sub-relatório", Order = 3, Label = "Parâmetros", Editor = "dict")]
    public EquatableDictionary<string, string> ParameterBindings { get; init; } = EquatableDictionary<string, string>.Empty;

    /// <summary>Optional data source binding — expression in the parent context
    /// yielding the IEnumerable passed to the child's data source.</summary>
    [PropertyGrid(Category = "Sub-relatório", Order = 2, Label = "Dados", Placeholder = "Fields.Itens (expressão no pai)")]
    public string? DataExpression { get; init; }
}
