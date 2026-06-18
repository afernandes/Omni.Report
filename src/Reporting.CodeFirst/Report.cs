using Reporting.DataSources;
using Reporting.Layout;

namespace Reporting.CodeFirst;

/// <summary>The output of <see cref="ReportBuilderRoot.Build"/>: a self-contained report ready
/// to paginate. Wraps the <see cref="Definition"/> AST and a <see cref="DataSources"/> registry
/// containing every data source registered via the fluent <c>DataSource</c> calls.</summary>
public sealed class Report
{
    public Report(ReportDefinition definition, DataSourceRegistry dataSources)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(dataSources);
        Definition = definition;
        DataSources = dataSources;
    }

    public ReportDefinition Definition { get; }
    public DataSourceRegistry DataSources { get; }

    /// <summary>Paginates the report with the given parameters (optional).</summary>
    public Task<RenderedReport> PaginateAsync(
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var paginator = new ReportPaginator();
        var request = new PaginationRequest
        {
            Definition = Definition,
            DataSources = DataSources,
            Parameters = parameters ?? new Dictionary<string, object?>(),
        };
        return paginator.PaginateAsync(request, cancellationToken);
    }
}
