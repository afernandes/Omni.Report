using System.Runtime.CompilerServices;
using Reporting.DataSources;
namespace Reporting.Layout.Tests;

internal sealed class AuditCountingSource(IReportDataSource source, bool failOnRead = false) : IReportDataSource
{
    public int Reads { get; private set; }
    public string Name => source.Name;
    public IReportRecordSchema Schema => source.Schema;
    public async IAsyncEnumerable<IReportRecord> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Reads++;
        if (failOnRead) throw new InvalidOperationException("Fonte não utilizada foi aberta.");
        await Task.Yield();
        await foreach (var row in source.ReadAsync(cancellationToken)) yield return row;
    }
}
