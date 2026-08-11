using System.Data.Common;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Reporting.DataSources.Integration.Tests;

/// <summary>
/// Boots one real database engine in a container and seeds a fixed table, so the connector under test
/// talks to the actual server rather than a stand-in.
/// </summary>
/// <remarks>
/// The table is deliberately awkward: a null in every nullable column, a decimal with real scale, a
/// date with a time component, and a GUID in whatever type the engine calls one. Those are the values
/// a thin ADO.NET shim gets wrong, and they are invisible to a test that only counts rows.
/// </remarks>
public abstract class DbEngineFixture : IAsyncLifetime
{
    /// <summary>Connection string for the running container. Only valid after <see cref="InitializeAsync"/>.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>The GUID stored in the seeded row, for the type-mapping assertions.</summary>
    public static readonly Guid KnownGuid = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    /// <summary>The timestamp stored in the seeded row — with a time component on purpose.</summary>
    public static readonly DateTime KnownDate = new(2026, 3, 14, 15, 9, 26, DateTimeKind.Unspecified);

    /// <summary>Builds the connector under test over this engine.</summary>
    public abstract IReportDataSource CreateDataSource(string name, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null);

    /// <summary>Statements that create and seed the fixture table on this engine.</summary>
    protected abstract IReadOnlyList<string> SeedStatements { get; }

    /// <summary>Opens a raw connection, used only to run the seed statements.</summary>
    protected abstract DbConnection OpenRaw();

    /// <summary>Starts the container and seeds the table.</summary>
    public abstract Task InitializeAsync();

    /// <summary>Stops the container.</summary>
    public abstract Task DisposeAsync();

    /// <summary>Records the connection string once the container is up.</summary>
    protected void SetConnectionString(string value) => ConnectionString = value;

    /// <summary>Runs <see cref="SeedStatements"/> against the freshly started engine.</summary>
    protected async Task SeedAsync()
    {
        await using var cn = OpenRaw();
        await cn.OpenAsync();
        foreach (var sql in SeedStatements)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>SQL Server 2022 in a container.</summary>
public sealed class SqlServerFixture : DbEngineFixture
{
    // Imagem pinada: o construtor sem parâmetro está obsoleto no Testcontainers 4.13, e uma tag fixa
    // deixa o CI reproduzível — "latest" faria a suíte mudar de comportamento sem nenhum commit.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04").Build();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await _container.StartAsync();
        SetConnectionString(_container.GetConnectionString());
        await SeedAsync();
    }

    /// <inheritdoc/>
    public override Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <inheritdoc/>
    protected override DbConnection OpenRaw() => new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);

    /// <inheritdoc/>
    public override IReportDataSource CreateDataSource(string name, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
        => new SqlServer.SqlServerDataSource(name, ConnectionString, sql, parameters);

    /// <inheritdoc/>
    protected override IReadOnlyList<string> SeedStatements =>
    [
        """
        CREATE TABLE vendas (
            id          INT              NOT NULL PRIMARY KEY,
            produto     NVARCHAR(50)     NOT NULL,
            valor       DECIMAL(12,2)    NULL,
            emitido_em  DATETIME2        NULL,
            referencia  UNIQUEIDENTIFIER NULL,
            ativo       BIT              NULL
        )
        """,
        $"""
        INSERT INTO vendas (id, produto, valor, emitido_em, referencia, ativo) VALUES
            (1, N'Teclado', 1450.90, '2026-03-14T15:09:26', '{KnownGuid}', 1),
            (2, N'Monitor', NULL,    NULL,                  NULL,         NULL)
        """,
    ];
}

/// <summary>PostgreSQL in a container.</summary>
public sealed class PostgreSqlFixture : DbEngineFixture
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17.2-alpine").Build();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await _container.StartAsync();
        SetConnectionString(_container.GetConnectionString());
        await SeedAsync();
    }

    /// <inheritdoc/>
    public override Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <inheritdoc/>
    protected override DbConnection OpenRaw() => new Npgsql.NpgsqlConnection(ConnectionString);

    /// <inheritdoc/>
    public override IReportDataSource CreateDataSource(string name, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
        => new PostgreSql.PostgreSqlDataSource(name, ConnectionString, sql, parameters);

    /// <inheritdoc/>
    protected override IReadOnlyList<string> SeedStatements =>
    [
        """
        CREATE TABLE vendas (
            id          INTEGER        NOT NULL PRIMARY KEY,
            produto     VARCHAR(50)    NOT NULL,
            valor       NUMERIC(12,2)  NULL,
            emitido_em  TIMESTAMP      NULL,
            referencia  UUID           NULL,
            ativo       BOOLEAN        NULL
        )
        """,
        $"""
        INSERT INTO vendas (id, produto, valor, emitido_em, referencia, ativo) VALUES
            (1, 'Teclado', 1450.90, TIMESTAMP '2026-03-14 15:09:26', '{KnownGuid}', TRUE),
            (2, 'Monitor', NULL,    NULL,                            NULL,          NULL)
        """,
    ];
}

/// <summary>MySQL in a container.</summary>
public sealed class MySqlFixture : DbEngineFixture
{
    private readonly MySqlContainer _container =
        new MySqlBuilder("mysql:8.4.3").Build();

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await _container.StartAsync();
        // AllowUserVariables/UseAffectedRows stay at their defaults; GuidFormat=Char36 is what makes
        // MySqlConnector surface CHAR(36) as a Guid, which is how MySQL stores one.
        SetConnectionString(_container.GetConnectionString() + ";GuidFormat=Char36");
        await SeedAsync();
    }

    /// <inheritdoc/>
    public override Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <inheritdoc/>
    protected override DbConnection OpenRaw() => new MySqlConnector.MySqlConnection(ConnectionString);

    /// <inheritdoc/>
    public override IReportDataSource CreateDataSource(string name, string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
        => new MySql.MySqlDataSource(name, ConnectionString, sql, parameters);

    /// <inheritdoc/>
    protected override IReadOnlyList<string> SeedStatements =>
    [
        """
        CREATE TABLE vendas (
            id          INT            NOT NULL PRIMARY KEY,
            produto     VARCHAR(50)    NOT NULL,
            valor       DECIMAL(12,2)  NULL,
            emitido_em  DATETIME       NULL,
            referencia  CHAR(36)       NULL,
            ativo       TINYINT(1)     NULL
        )
        """,
        $"""
        INSERT INTO vendas (id, produto, valor, emitido_em, referencia, ativo) VALUES
            (1, 'Teclado', 1450.90, '2026-03-14 15:09:26', '{KnownGuid}', 1),
            (2, 'Monitor', NULL,    NULL,                  NULL,          NULL)
        """,
    ];
}
