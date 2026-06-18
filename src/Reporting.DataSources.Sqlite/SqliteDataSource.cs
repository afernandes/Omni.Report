using Microsoft.Data.Sqlite;
using Reporting.DataSources.AdoNet;

namespace Reporting.DataSources.Sqlite;

/// <summary>
/// SQLite-backed <see cref="IReportDataSource"/>. Thin wrapper over
/// <see cref="AdoNetDataSource"/> that supplies a <see cref="SqliteConnection"/> factory.
/// </summary>
/// <remarks>
/// <para>Two main usage modes:</para>
/// <list type="bullet">
/// <item><b>File / shared-cache mode</b> — pass a connection string like
/// <c>"Data Source=mydb.sqlite"</c> or <c>"Data Source=file::memory:?cache=shared"</c>.
/// Each <see cref="ReadAsync"/> opens a fresh connection (recommended).</item>
/// <item><b>Single-connection mode</b> — pass an already-opened <see cref="SqliteConnection"/>
/// directly (use the <see cref="AdoNetDataSource"/> constructor that takes a connection).
/// Mandatory for an isolated <c>:memory:</c> database, because each new connection to
/// <c>:memory:</c> sees a brand-new empty database.</item>
/// </list>
///
/// <para><b>Parameters</b> use the <c>$name</c> or <c>@name</c> prefix in the SQL — SQLite
/// accepts both. Pass them via the parameter dictionary in <see cref="AdoNetQuery"/>.</para>
/// </remarks>
public sealed class SqliteDataSource : IReportDataSource
{
    private readonly AdoNetDataSource _inner;

    /// <summary>Creates a SQLite data source that opens a fresh connection per read.
    /// Best for file-backed or shared-cache databases.</summary>
    public SqliteDataSource(string name, string connectionString, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        _inner = new AdoNetDataSource(
            name,
            () => new SqliteConnection(connectionString),
            new AdoNetQuery(sql, parameters));
    }

    /// <summary>Creates a SQLite data source bound to an already-opened connection — required
    /// for in-memory databases where each connection sees its own database.</summary>
    public SqliteDataSource(string name, SqliteConnection openConnection, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(openConnection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        _inner = new AdoNetDataSource(name, openConnection, new AdoNetQuery(sql, parameters));
    }

    public string Name => _inner.Name;
    public IReportRecordSchema Schema => _inner.Schema;
    public IAsyncEnumerable<IReportRecord> ReadAsync(CancellationToken cancellationToken = default)
        => _inner.ReadAsync(cancellationToken);
}
