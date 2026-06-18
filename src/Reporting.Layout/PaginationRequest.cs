using Reporting.DataSources;
using Reporting.Rendering;

namespace Reporting.Layout;

/// <summary>Inputs required to paginate a <c>ReportDefinition</c>.</summary>
public sealed class PaginationRequest
{
    public required ReportDefinition Definition { get; init; }

    /// <summary>Named data sources keyed by <c>DataSourceDefinition.Name</c>. The first entry
    /// (or the one whose name matches the report's primary data source) drives the detail band.</summary>
    public required DataSourceRegistry DataSources { get; init; }

    /// <summary>Parameter values supplied by the caller. Missing parameters fall back to
    /// <c>ReportParameter.DefaultValue</c>.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();

    /// <summary>Text measurer used for CanGrow / wrap calculation. Defaults to
    /// <see cref="AverageWidthTextMeasurer"/>.</summary>
    public ITextMeasurer? Measurer { get; init; }

    /// <summary>If set, drives the detail band. When null, the first registered data source is used.</summary>
    public string? PrimaryDataSource { get; init; }

    /// <summary>Optional resolver for RDL <c>Code.MethodName(...)</c> calls (the report's Code
    /// block). <c>null</c> by default — the core never executes C#. Supply one from the opt-in
    /// <c>Reporting.Expressions.Roslyn</c> package (<c>RoslynCode.CreateResolver(...)</c>) to
    /// enable custom code. Carries the security implications of running embedded C#.</summary>
    public Func<string, object?[], object?>? CodeFunctionResolver { get; init; }
}
