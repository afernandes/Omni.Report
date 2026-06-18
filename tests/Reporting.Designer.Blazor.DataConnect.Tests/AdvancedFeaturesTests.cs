using FluentAssertions;
using Microsoft.Data.Sqlite;
using Reporting.DataSources;
using Reporting.Designer.Blazor.DataConnect;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.DataConnect.Tests;

/// <summary>
/// Tests for the second-wave features: secret resolution, schema explorer, stored-procedure
/// signature discovery, master-detail relations, transaction-aware borrowed connection.
/// </summary>
public sealed class AdvancedFeaturesTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly string _connectionString;

    public AdvancedFeaturesTests()
    {
        var dbName = $"adv-{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE clientes (id INTEGER PRIMARY KEY, nome TEXT NOT NULL);
            CREATE TABLE pedidos (id INTEGER PRIMARY KEY, cliente_id INTEGER, total REAL);
            CREATE VIEW v_resumo AS SELECT c.nome, SUM(p.total) AS soma
              FROM clientes c JOIN pedidos p ON p.cliente_id = c.id GROUP BY c.nome;
            INSERT INTO clientes (id, nome) VALUES (1,'Ana'),(2,'Bruno'),(3,'Carla');
            INSERT INTO pedidos  (cliente_id, total) VALUES
              (1, 100), (1, 50), (2, 80), (3, 30), (3, 70);
        """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _keepAlive.Dispose();

    // ── Secret resolution ─────────────────────────────────────────────────────

    [Fact]
    public void SecretTemplate_ExpandFromEnvironment_replaces_known_and_keeps_unknown()
    {
        Environment.SetEnvironmentVariable("OMNI_TEST_TOKEN", "s3cr3t");
        try
        {
            var template = "Token={secret:OMNI_TEST_TOKEN};Other={secret:OMNI_TEST_MISSING}";
            var result = SecretTemplate.ExpandFromEnvironment(template);
            result.Should().Be("Token=s3cr3t;Other={secret:OMNI_TEST_MISSING}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OMNI_TEST_TOKEN", null);
        }
    }

    [Fact]
    public async Task SecretTemplate_ExpandAsync_collects_missing_names()
    {
        var resolver = new InMemorySecretResolver(("DB_HOST", "prod.db"));
        var missing = new List<string>();
        var template = "Host={secret:DB_HOST};Password={secret:DB_PASSWORD}";

        var result = await SecretTemplate.ExpandAsync(template, resolver, missing);

        result.Should().Be("Host=prod.db;Password={secret:DB_PASSWORD}");
        missing.Should().ContainSingle().Which.Should().Be("DB_PASSWORD");
    }

    [Fact]
    public void SecretTemplate_ContainsPlaceholder_detects_correctly()
    {
        SecretTemplate.ContainsPlaceholder("Host=local").Should().BeFalse();
        SecretTemplate.ContainsPlaceholder("Host={secret:DB}").Should().BeTrue();
        SecretTemplate.ContainsPlaceholder(null).Should().BeFalse();
        SecretTemplate.ContainsPlaceholder("").Should().BeFalse();
    }

    // ── Schema explorer ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListSchemaAsync_returns_tables_views_and_columns()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.ListSchemaAsync(DataConnectionKind.Sqlite, _connectionString);

        result.Error.Should().BeNull();
        result.Tables.Should().HaveCount(3); // clientes, pedidos, v_resumo

        var clientes = result.Tables.Single(t => t.Name == "clientes");
        clientes.IsView.Should().BeFalse();
        clientes.Columns.Should().HaveCount(2);
        clientes.Columns.Single(c => c.Name == "id").IsPrimaryKey.Should().BeTrue();

        var view = result.Tables.Single(t => t.Name == "v_resumo");
        view.IsView.Should().BeTrue();
        view.Columns.Should().Contain(c => c.Name == "nome");
        view.Columns.Should().Contain(c => c.Name == "soma");
    }

    [Fact]
    public async Task ListSchemaAsync_inmemory_returns_empty()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.ListSchemaAsync(DataConnectionKind.InMemory, string.Empty);
        result.Tables.Should().BeEmpty();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── Stored procedure discovery (Sqlite returns "not supported" gracefully) ─

    [Fact]
    public async Task DiscoverProcedureSignatureAsync_sqlite_reports_not_supported()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.DiscoverProcedureSignatureAsync(
            DataConnectionKind.Sqlite, _connectionString, "any_name");

        result.Parameters.Should().BeEmpty();
        result.Error.Should().Contain("não suporta");
    }

    // ── Master-detail data source ─────────────────────────────────────────────

    [Fact]
    public async Task MasterDetailDataSource_filters_children_by_parent_value()
    {
        // Build the children source directly via AdoNet.
        var child = DesignerDataSourceFactory.Build(new DesignerDataSource("Pedidos", []) {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT id, cliente_id, total FROM pedidos",
        });
        child.Should().NotBeNull();

        var detail = new MasterDetailDataSource("PedidosDeCliente", child!, "cliente_id");
        detail.WithParentValue(1);
        var rows1 = new List<int>();
        await foreach (var r in detail.ReadAsync())
        {
            rows1.Add(Convert.ToInt32(r["cliente_id"]));
        }
        rows1.Should().HaveCount(2).And.OnlyContain(v => v == 1);

        detail.WithParentValue(3);
        var rows3 = new List<int>();
        await foreach (var r in detail.ReadAsync())
        {
            rows3.Add(Convert.ToInt32(r["cliente_id"]));
        }
        rows3.Should().HaveCount(2).And.OnlyContain(v => v == 3);
    }

    [Fact]
    public void DesignerDataSourceFactory_BuildRegistry_wires_master_detail()
    {
        var clientes = new DesignerDataSource("Clientes", []) {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT id, nome FROM clientes",
        };
        var pedidos = new DesignerDataSource("Pedidos", []) {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT id, cliente_id, total FROM pedidos",
        };
        var rel = new DesignerRelation("PedidosDeCliente", "Clientes", "id", "Pedidos", "cliente_id");

        var registry = DesignerDataSourceFactory.BuildRegistry(
            new[] { clientes, pedidos }, new[] { rel });

        registry.Names.Should().Contain("Clientes")
                       .And.Contain("Pedidos")
                       .And.Contain("PedidosDeCliente");

        var view = registry.Get("PedidosDeCliente");
        view.Should().BeOfType<MasterDetailDataSource>();
        ((MasterDetailDataSource)view).ChildField.Should().Be("cliente_id");
    }

    // ── Transaction-aware borrowed connection ─────────────────────────────────

    [Fact]
    public async Task BuildWithConnection_reuses_open_connection_inside_transaction()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Mutate inside the transaction — the source must see the uncommitted row.
        await using (var ins = conn.CreateCommand())
        {
            ins.Transaction = (SqliteTransaction)tx;
            ins.CommandText = "INSERT INTO clientes (id, nome) VALUES (999, 'TX-Only')";
            await ins.ExecuteNonQueryAsync();
        }

        var vm = new DesignerDataSource("Clientes", []) {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT id, nome FROM clientes WHERE id = 999",
        };
        var source = DesignerDataSourceFactory.BuildWithConnection(vm, conn);
        source.Should().NotBeNull();

        // NOTE: AdoNetDataSource doesn't propagate the transaction to its command — for that
        // the caller should rely on the borrowed connection's currently-active transaction,
        // which SQLite associates implicitly. Here we just verify the source uses the same
        // connection (otherwise a new SqliteConnection wouldn't see the uncommitted INSERT).
        var rows = new List<string>();
        await foreach (var r in source!.ReadAsync())
        {
            rows.Add((string)r["nome"]!);
        }
        rows.Should().ContainSingle("TX-Only");

        await tx.RollbackAsync();
    }

    // ── Helper: in-memory secret store ─────────────────────────────────────────

    private sealed class InMemorySecretResolver : ISecretResolver
    {
        private readonly Dictionary<string, string> _values;
        public InMemorySecretResolver(params (string Name, string Value)[] entries)
            => _values = entries.ToDictionary(t => t.Name, t => t.Value, StringComparer.Ordinal);

        public Task<string?> ResolveAsync(string secretName, CancellationToken ct = default)
            => Task.FromResult(_values.TryGetValue(secretName, out var v) ? v : null);
    }
}
