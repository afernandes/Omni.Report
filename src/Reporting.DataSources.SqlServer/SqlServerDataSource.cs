using Microsoft.Data.SqlClient;
using Reporting.DataSources.AdoNet;

namespace Reporting.DataSources.SqlServer;

/// <summary>
/// Microsoft SQL Server-backed <see cref="IReportDataSource"/>. Thin wrapper over
/// <see cref="AdoNetDataSource"/> that supplies a <see cref="SqlConnection"/> factory.
/// </summary>
/// <remarks>
/// <para>Connection string examples:</para>
/// <list type="bullet">
/// <item>Trusted: <c>Server=.\SQLEXPRESS;Database=erp;Integrated Security=true;TrustServerCertificate=true</c></item>
/// <item>SQL auth: <c>Server=tcp:server.db.windows.net,1433;Database=erp;User Id=app;Password=secret;Encrypt=true</c></item>
/// </list>
/// <para><b>Parameters</b> use the <c>@name</c> prefix.</para>
/// </remarks>
public sealed class SqlServerDataSource : IReportDataSource
{
    private readonly AdoNetDataSource _inner;

    public SqlServerDataSource(string name, string connectionString, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        _inner = new AdoNetDataSource(
            name,
            () => new SqlConnection(connectionString),
            new AdoNetQuery(sql, parameters));
    }

    public string Name => _inner.Name;
    public IReportRecordSchema Schema => _inner.Schema;
    public IAsyncEnumerable<IReportRecord> ReadAsync(CancellationToken cancellationToken = default)
        => _inner.ReadAsync(cancellationToken);
}
