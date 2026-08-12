namespace Reporting.Layout;

/// <summary>Lays out a report definition + data into a <see cref="RenderedReport"/>.</summary>
public interface IReportPaginator
{
    /// <summary>Runs the report: reads the data, evaluates expressions, stacks bands and breaks pages.</summary>
    /// <param name="request">Definition, data sources and parameters to render.</param>
    /// <param name="ct">Cancels the run. Honoured per data row, not only between phases.</param>
    /// <returns>The paginated output, ready for a renderer or an exporter.</returns>
    Task<RenderedReport> PaginateAsync(PaginationRequest request, CancellationToken ct = default);
}
