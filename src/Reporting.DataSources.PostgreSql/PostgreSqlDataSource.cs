using Npgsql;
using Reporting.DataSources.AdoNet;

namespace Reporting.DataSources.PostgreSql;

/// <summary>
/// PostgreSQL-backed <see cref="IReportDataSource"/>. Thin wrapper over
/// <see cref="AdoNetDataSource"/> that supplies an <see cref="NpgsqlConnection"/> factory.
/// </summary>
/// <remarks>
/// <para>Connection string follows the Npgsql format:
/// <c>"Host=localhost;Username=postgres;Password=secret;Database=mydb"</c>.
/// Pooling is enabled by default — each <see cref="ReadAsync"/> call acquires a connection
/// from the pool and returns it on disposal.</para>
///
/// <para><b>Parameters</b> use the <c>@name</c> prefix in SQL (Npgsql accepts <c>@</c>,
/// <c>:</c>, and <c>$</c>). Pass via the parameter dictionary.</para>
///
/// <para>For high-throughput scenarios (many reports against the same database), prefer
/// the overload that accepts an <see cref="NpgsqlDataSource"/> — it manages its own pool
/// and is the modern Npgsql idiom (since 7.0).</para>
/// </remarks>
public sealed class PostgreSqlDataSource : IReportDataSource
{
    private readonly AdoNetDataSource _inner;

    /// <summary>Connection-string flavor: opens a fresh <see cref="NpgsqlConnection"/> per read.</summary>
    public PostgreSqlDataSource(string name, string connectionString, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        _inner = new AdoNetDataSource(
            name,
            () => new NpgsqlConnection(connectionString),
            new AdoNetQuery(sql, parameters));
    }

    /// <summary>Pool-aware flavor: takes an <see cref="Npgsql.NpgsqlDataSource"/> (Npgsql 7+ idiom)
    /// and asks it for a connection on each read.</summary>
    public PostgreSqlDataSource(string name, NpgsqlDataSource dataSource, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        _inner = new AdoNetDataSource(
            name,
            () => dataSource.CreateConnection(),
            new AdoNetQuery(sql, parameters));
    }

    public string Name => _inner.Name;
    public IReportRecordSchema Schema => _inner.Schema;
    public IAsyncEnumerable<IReportRecord> ReadAsync(CancellationToken cancellationToken = default)
        => _inner.ReadAsync(cancellationToken);
}
