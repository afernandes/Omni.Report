using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
namespace Reporting.DataSources.AdoNet.Tests;

internal sealed class CloseFailureConnection : DbConnection
{
    private readonly SqliteConnection _inner = new("Data Source=:memory:");
    public bool WasDisposed { get; private set; }
    [AllowNull] public override string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
    public override string Database => _inner.Database;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override ConnectionState State => _inner.State;
    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
    public override void Open() => _inner.Open();
    public override void Close() => _inner.Close();
    public override Task CloseAsync() => Task.FromException(new IOException("Falha simulada no fechamento"));
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);
    protected override DbCommand CreateDbCommand() => _inner.CreateCommand();
    public override async ValueTask DisposeAsync()
    {
        WasDisposed = true;
        await _inner.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
