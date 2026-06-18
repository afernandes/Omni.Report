namespace Reporting.DataSources;

/// <summary>Streaming source of report records — produces records asynchronously to
/// support large datasets and database-backed providers.</summary>
public interface IReportDataSource
{
    /// <summary>Stable name of the source (matches <c>DataSourceDefinition.Name</c>).</summary>
    string Name { get; }

    /// <summary>Schema described by this source (may be inferred from the first record).</summary>
    IReportRecordSchema Schema { get; }

    /// <summary>Iterates the records of the source. Implementations should defer work.</summary>
    IAsyncEnumerable<IReportRecord> ReadAsync(CancellationToken cancellationToken = default);
}
