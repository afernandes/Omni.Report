using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    /// <summary>Emits completed pages with one buffered page of backpressure. Provider rows are
    /// materialized before the first page; reports requiring totals also complete a counting pass first.
    /// Breaking enumeration cancels and joins the worker, releasing pending pages and provider resources.
    /// Requires a threaded host. Browser runtimes are rejected because blocking the producer on
    /// backpressure can prevent a single-threaded consumer from making progress.
    /// Use <see cref="PrepareAsync"/> for random access and definition diagnostics.</summary>
    public async IAsyncEnumerable<RenderedPage> StreamPagesAsync(PaginationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException("Progressive pagination requires a threaded host; use PrepareAsync or PaginateAsync in browser runtimes.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channel = Channel.CreateBounded<RenderedPage>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        // Only this worker blocks on a full channel; the consuming UI/event loop stays asynchronous.
        var worker = Task.Run(async () =>
        {
            try
            {
                var run = new ReportPaginator(_compiler)
                {
                    _pageSink = page => channel.Writer.WriteAsync(page, lifetime.Token).AsTask().GetAwaiter().GetResult()
                };
                await run.ExecuteAsync(request, lifetime.Token).ConfigureAwait(false);
                channel.Writer.TryComplete();
            }
            catch (Exception exception) { channel.Writer.TryComplete(exception); }
        }, CancellationToken.None);
        try
        {
            await foreach (var page in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return page;
        }
        finally
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            await worker.ConfigureAwait(false);
        }
    }
}
