using MySqlConnector;
using Reporting.DataSources.AdoNet;

namespace Reporting.DataSources.MySql;

/// <summary>
/// MySQL / MariaDB-backed <see cref="IReportDataSource"/> via MySqlConnector. Thin wrapper
/// over <see cref="AdoNetDataSource"/>.
/// </summary>
/// <remarks>
/// <para>Connection string: <c>Server=localhost;Database=erp;User=app;Password=secret;SslMode=Required</c>.
/// MySqlConnector is the async-first MIT alternative to Oracle's MySql.Data.</para>
/// <para><b>Parameters</b> use the <c>@name</c> prefix.</para>
/// </remarks>
public sealed class MySqlDataSource : IReportDataSource
{
    private readonly AdoNetDataSource _inner;

    public MySqlDataSource(string name, string connectionString, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        _inner = new AdoNetDataSource(
            name,
            () => new MySqlConnection(connectionString),
            new AdoNetQuery(sql, parameters));
    }

    public string Name => _inner.Name;
    public IReportRecordSchema Schema => _inner.Schema;
    public IAsyncEnumerable<IReportRecord> ReadAsync(CancellationToken cancellationToken = default)
        => _inner.ReadAsync(cancellationToken);
}
