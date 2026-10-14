namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    /// <summary>Prepares pages directly into bounded temporary storage without retaining a final
    /// <see cref="RenderedReport"/>. Provider rows are still materialized for sorting, aggregates and
    /// subreports. Completion means the whole document is ready; this is not a streaming data reader.
    /// Layout runs on a worker so an interactive host can process cancellation while it runs.</summary>
    public async Task<PreparedReport> PrepareAsync(PaginationRequest request,
        PreparedReportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var document = new PreparedReport(request.Definition.Name, options ?? new());
        try
        {
            var run = new ReportPaginator(_compiler) { _pageSink = document.Append };
            var result = await Task.Run(() => run.ExecuteAsync(request, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            document.Diagnostics = result.Diagnostics;
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}
