using System.Runtime.CompilerServices;
using Reporting.DataSources;
namespace Reporting.Designer.Blazor.Tests;

internal sealed class GatedPreviewSource(IReportDataSource source) : IReportDataSource
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string Name => source.Name;
    public IReportRecordSchema Schema => source.Schema;
    public async IAsyncEnumerable<IReportRecord> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Started.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
        await foreach (var row in source.ReadAsync(cancellationToken)) yield return row;
    }
}
