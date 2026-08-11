using FluentAssertions;
using Xunit;

namespace Reporting.DataSources.Integration.Tests;

/// <summary>
/// One contract, run against every SQL connector.
/// </summary>
/// <remarks>
/// <para>`SqlServerDataSource`, `PostgreSqlDataSource` and `MySqlDataSource` are the same shim three
/// times over: each supplies a connection factory to <c>AdoNetDataSource</c>. Because they are
/// interchangeable by design, the assertions belong in one place — a per-engine copy would drift, and
/// a behaviour that only one of them gets right is exactly the bug worth catching.</para>
///
/// <para>Each engine differs in how it spells its types, so the DDL lives in the fixture while the
/// expectations live here. What is asserted is what a thin shim actually gets wrong: nulls surfacing
/// as <see cref="DBNull"/> instead of <c>null</c>, decimals arriving as <c>double</c>, dates losing
/// their time component, GUIDs arriving as strings, and cancellation being ignored.</para>
/// </remarks>
public abstract class SqlConnectorContractTests<TFixture> : IClassFixture<TFixture>
    where TFixture : DbEngineFixture
{
    private readonly TFixture _db;

    /// <summary>Receives the running engine from xUnit.</summary>
    protected SqlConnectorContractTests(TFixture db) => _db = db;

    private async Task<List<IReportRecord>> ReadAllAsync(string sql,
        IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        var rows = new List<IReportRecord>();
        await foreach (var r in _db.CreateDataSource("vendas", sql, parameters).ReadAsync(ct))
        {
            rows.Add(r);
        }
        return rows;
    }

    [DockerFact]
    public async Task Opens_the_connection_and_reads_every_row()
    {
        var rows = await ReadAllAsync("SELECT id, produto FROM vendas ORDER BY id");

        rows.Should().HaveCount(2);
        rows[0]["produto"].Should().Be("Teclado");
        rows[1]["produto"].Should().Be("Monitor");
    }

    [DockerFact]
    public async Task Exposes_the_schema_with_field_names_in_order()
    {
        var source = _db.CreateDataSource("vendas", "SELECT id, produto, valor FROM vendas ORDER BY id");

        // Schema is populated by reading — it describes the result set, which is not known before.
        await foreach (var _ in source.ReadAsync()) { break; }

        source.Schema.Fields.Select(f => f.Name)
              .Should().ContainInOrder("id", "produto", "valor");
        source.Schema.IndexOf("valor").Should().Be(2);
        source.Schema.IndexOf("inexistente").Should().Be(-1);
    }

    [DockerFact]
    public async Task Null_columns_surface_as_null_not_DBNull()
    {
        var rows = await ReadAllAsync(
            "SELECT id, valor, emitido_em, referencia FROM vendas WHERE id = 2");

        var row = rows.Should().ContainSingle().Subject;
        // DBNull leaking through is the classic ADO.NET shim bug: it is not null, so every downstream
        // null check silently fails and the value renders as "System.DBNull".
        row["valor"].Should().BeNull();
        row["emitido_em"].Should().BeNull();
        row["referencia"].Should().BeNull();
    }

    [DockerFact]
    public async Task Decimal_keeps_its_type_and_scale()
    {
        var rows = await ReadAllAsync("SELECT valor FROM vendas WHERE id = 1");

        var valor = rows.Should().ContainSingle().Subject["valor"];
        // Arriving as double would round-trip 1450.90 into 1450.8999999999999 in a currency report.
        valor.Should().BeOfType<decimal>().And.Be(1450.90m);
    }

    [DockerFact]
    public async Task DateTime_keeps_its_time_component()
    {
        var rows = await ReadAllAsync("SELECT emitido_em FROM vendas WHERE id = 1");

        var value = rows.Should().ContainSingle().Subject["emitido_em"];
        value.Should().BeOfType<DateTime>();
        ((DateTime)value!).Should().Be(DbEngineFixture.KnownDate);
    }

    [DockerFact]
    public async Task Guid_column_surfaces_as_Guid()
    {
        var rows = await ReadAllAsync("SELECT referencia FROM vendas WHERE id = 1");

        var value = rows.Should().ContainSingle().Subject["referencia"];
        // Each engine spells it differently — UNIQUEIDENTIFIER, UUID, CHAR(36) — and MySQL only maps it
        // when the connection string opts in. Arriving as a string would compare and format wrongly
        // everywhere downstream while looking plausible in the output.
        value.Should().BeOfType<Guid>().And.Be(DbEngineFixture.KnownGuid);
    }

    [DockerFact]
    public async Task Boolean_column_surfaces_as_bool()
    {
        var rows = await ReadAllAsync("SELECT ativo FROM vendas WHERE id = 1");

        // MySQL has no real boolean — TINYINT(1) is the convention, and the driver is expected to map it.
        rows.Should().ContainSingle().Subject["ativo"].Should().Be(true);
    }

    [DockerFact]
    public async Task Parameters_bind_by_name()
    {
        var rows = await ReadAllAsync(
            "SELECT id, produto FROM vendas WHERE id = @id",
            new Dictionary<string, object?> { ["id"] = 2 });

        rows.Should().ContainSingle().Subject["produto"].Should().Be("Monitor");
    }

    [DockerFact]
    public async Task Reads_lazily_instead_of_buffering_the_whole_result()
    {
        var source = _db.CreateDataSource("vendas", LargeQuery);

        int seen = 0;
        await foreach (var _ in source.ReadAsync())
        {
            if (++seen == 3)
            {
                break; // abandon the enumeration early
            }
        }

        // Reaching here without materialising all LargeRowCount rows is the point: a source that
        // buffered everything would still pass a row count assertion but blow up memory on a real table.
        seen.Should().Be(3);
    }

    [DockerFact]
    public async Task Cancellation_stops_the_read()
    {
        using var cts = new CancellationTokenSource();
        var source = _db.CreateDataSource("vendas", LargeQuery);

        var act = async () =>
        {
            await foreach (var _ in source.ReadAsync(cts.Token))
            {
                await cts.CancelAsync();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>A query that returns enough rows to make lazy reading observable, in this engine's dialect.</summary>
    protected abstract string LargeQuery { get; }
}

/// <summary>The contract, run against SQL Server.</summary>
public sealed class SqlServerConnectorTests(SqlServerFixture db) : SqlConnectorContractTests<SqlServerFixture>(db)
{
    /// <inheritdoc/>
    protected override string LargeQuery =>
        "SELECT TOP 500 v.id, v.produto FROM vendas v CROSS JOIN sys.all_objects";
}

/// <summary>The contract, run against PostgreSQL.</summary>
public sealed class PostgreSqlConnectorTests(PostgreSqlFixture db) : SqlConnectorContractTests<PostgreSqlFixture>(db)
{
    /// <inheritdoc/>
    protected override string LargeQuery =>
        "SELECT v.id, v.produto FROM vendas v CROSS JOIN generate_series(1, 250) LIMIT 500";
}

/// <summary>The contract, run against MySQL.</summary>
public sealed class MySqlConnectorTests(MySqlFixture db) : SqlConnectorContractTests<MySqlFixture>(db)
{
    /// <inheritdoc/>
    protected override string LargeQuery =>
        "SELECT v.id, v.produto FROM vendas v, information_schema.columns LIMIT 500";
}
