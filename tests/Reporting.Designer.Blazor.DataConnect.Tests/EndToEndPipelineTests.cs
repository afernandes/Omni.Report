using FluentAssertions;
using Microsoft.Data.Sqlite;
using Reporting.Designer.Blazor.DataConnect;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Layout;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Designer.Blazor.DataConnect.Tests;

/// <summary>
/// End-to-end "designer → save → load → paginate against real DB" pipeline tests.
/// Validates that a report designed in the designer with a SQLite-backed data source
/// round-trips through <c>.repx</c> and renders correctly when paginated.
/// </summary>
public sealed class EndToEndPipelineTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly string _connectionString;

    public EndToEndPipelineTests()
    {
        var dbName = $"e2e-{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE pedidos (
                id INTEGER PRIMARY KEY, cliente TEXT, produto TEXT,
                quantidade REAL, preco REAL, data TEXT);
            INSERT INTO pedidos (cliente, produto, quantidade, preco, data) VALUES
                ('Ana',   'Caneta',    10, 2.50, '2026-05-03'),
                ('Ana',   'Caderno',    1, 27.40,'2026-05-04'),
                ('Beto',  'Borracha',   8, 1.20, '2026-05-09'),
                ('Carla', 'Mochila',    1,189.00,'2026-05-10');
        """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _keepAlive.Dispose();

    [Fact]
    public void DataSource_round_trips_through_repx()
    {
        // ── 1. Build designer state with a SQLite data source ────────────────
        var state = new DesignerState();
        state.DataSources.Clear();
        var vm = new DesignerDataSource("Vendas", new[]
        {
            new DesignerField("cliente",    DesignerFieldType.Text),
            new DesignerField("produto",    DesignerFieldType.Text),
            new DesignerField("quantidade", DesignerFieldType.Number),
            new DesignerField("preco",      DesignerFieldType.Money),
        })
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT cliente, produto, quantidade, preco FROM pedidos WHERE data >= $de",
            CommandTimeoutSeconds = 45,
        };
        vm.SqlParameters.Add(new DesignerSqlParameter("$de", "DataInicio", "2026-05-01"));
        state.DataSources.Add(vm);

        state.Relations.Clear();
        // No relations for this test (single source) — but the empty array should round-trip.

        // ── 2. Save to .repx ──────────────────────────────────────────────────
        var bytes = state.Save();
        bytes.Should().NotBeNullOrEmpty();

        // ── 3. Load into a fresh state ────────────────────────────────────────
        var reloaded = new DesignerState();
        reloaded.Load(bytes);

        // ── 4. Verify the round-trip preserved everything ─────────────────────
        reloaded.DataSources.Should().HaveCount(1);
        var rds = reloaded.DataSources[0];
        rds.Name.Should().Be("Vendas");
        rds.Kind.Should().Be(DataConnectionKind.Sqlite);
        rds.ConnectionString.Should().Be(_connectionString);
        rds.Sql.Should().Be("SELECT cliente, produto, quantidade, preco FROM pedidos WHERE data >= $de");
        rds.CommandTimeoutSeconds.Should().Be(45);
        rds.SqlParameters.Should().HaveCount(1);
        rds.SqlParameters[0].SqlName.Should().Be("$de");
        rds.SqlParameters[0].ReportParameter.Should().Be("DataInicio");
        rds.SqlParameters[0].Literal.Should().Be("2026-05-01");
    }

    [Fact]
    public void Relations_round_trip_through_repx()
    {
        var state = new DesignerState();
        state.DataSources.Clear();
        state.DataSources.Add(new DesignerDataSource("Clientes", new[]
        {
            new DesignerField("id", DesignerFieldType.Number),
            new DesignerField("nome", DesignerFieldType.Text),
        })
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT 1 AS id, 'X' AS nome",
        });
        state.DataSources.Add(new DesignerDataSource("Pedidos", new[]
        {
            new DesignerField("cliente_id", DesignerFieldType.Number),
            new DesignerField("total", DesignerFieldType.Money),
        })
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT 1 AS cliente_id, 9.9 AS total",
        });
        state.Relations.Clear();
        state.Relations.Add(new DesignerRelation(
            "PedidosDoCliente", "Clientes", "id", "Pedidos", "cliente_id"));

        var bytes = state.Save();

        var reloaded = new DesignerState();
        reloaded.Load(bytes);

        reloaded.Relations.Should().HaveCount(1);
        var r = reloaded.Relations[0];
        r.Name.Should().Be("PedidosDoCliente");
        r.ParentSource.Should().Be("Clientes");
        r.ParentField.Should().Be("id");
        r.ChildSource.Should().Be("Pedidos");
        r.ChildField.Should().Be("cliente_id");
    }

    [Fact]
    public async Task BuildRegistry_with_sql_parameters_renders_filtered_rows()
    {
        // Build a single data source whose query uses a SQL parameter resolved
        // from a runtime report parameter dictionary.
        var vm = new DesignerDataSource("Vendas", System.Array.Empty<DesignerField>())
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT cliente, produto, preco FROM pedidos WHERE cliente = $cli",
        };
        vm.SqlParameters.Add(new DesignerSqlParameter("$cli", "ClienteAtual"));

        var runtimeParams = new Dictionary<string, object?> { ["ClienteAtual"] = "Ana" };
        var registry = DesignerDataSourceFactory.BuildRegistry(
            new[] { vm }, System.Array.Empty<DesignerRelation>(), runtimeParams);

        registry.Names.Should().Contain("Vendas");
        var src = registry.Get("Vendas");

        var clientes = new List<string>();
        await foreach (var r in src.ReadAsync())
        {
            clientes.Add((string)r["cliente"]!);
        }
        clientes.Should().HaveCount(2).And.OnlyContain(c => c == "Ana");
    }
}
