namespace Reporting.Layout;

/// <summary>Lays out a report definition + data into a <see cref="RenderedReport"/>.</summary>
public interface IReportPaginator
{
    Task<RenderedReport> PaginateAsync(PaginationRequest request, CancellationToken ct = default);
}
