using FluentAssertions;
using Microsoft.Data.Sqlite;
using Reporting.Designer.Blazor.DataConnect;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.DataConnect.Tests;

/// <summary>End-to-end tests for the designer data-connect service. SQLite (file mode,
/// shared-cache) is the test backbone — no external service, works on any OS / CI.</summary>
public class DesignerDataConnectTests : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAlive;

    public DesignerDataConnectTests()
    {
        // Shared-cache in-memory DB — multiple connections see the same data, but the
        // database is freed when no connection is open. Keep one alive for the test fixture.
        _connectionString = $"Data Source=file:test_{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
        using var seed = _keepAlive.CreateCommand();
        seed.CommandText = @"
            CREATE TABLE customers (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                total REAL NOT NULL
            );
            INSERT INTO customers VALUES
                (1, 'Ana',   125.50),
                (2, 'Beto',   45.00),
                (3, 'Carla', 350.00);";
        seed.ExecuteNonQuery();
    }

    public void Dispose() => _keepAlive.Dispose();

    [Fact]
    public async Task TestConnection_succeeds_against_live_sqlite()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.TestConnectionAsync(DataConnectionKind.Sqlite, _connectionString);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Conectado");
        result.Elapsed.Should().BePositive();
    }

    [Fact]
    public async Task TestConnection_reports_failure_for_invalid_connection_string()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.TestConnectionAsync(
            DataConnectionKind.Sqlite,
            "Data Source=/non/existent/path/db.sqlite;Mode=ReadOnly");

        // SQLite refuses to open a missing read-only file → failure surfaces in Message,
        // never as an exception.
        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DiscoverSchema_returns_columns_with_clr_types()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.DiscoverSchemaAsync(
            DataConnectionKind.Sqlite,
            _connectionString,
            "SELECT id, name, total FROM customers");

        result.Error.Should().BeNull();
        result.Fields.Should().HaveCount(3);
        result.Fields[0].Name.Should().Be("id");
        result.Fields[1].Name.Should().Be("name");
        result.Fields[2].Name.Should().Be("total");
        // Sqlite reports id as long, total as double.
        result.Fields[0].ClrType.Should().Be(typeof(long));
        result.Fields[2].ClrType.Should().Be(typeof(double));
    }

    [Fact]
    public async Task Preview_returns_rows_and_fields()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.PreviewAsync(
            DataConnectionKind.Sqlite,
            _connectionString,
            "SELECT name, total FROM customers ORDER BY id",
            maxRows: 50);

        result.Error.Should().BeNull();
        result.Fields.Select(f => f.Name).Should().Equal("name", "total");
        result.Rows.Should().HaveCount(3);
        result.Rows[0]["name"].Should().Be("Ana");
        result.Rows[0]["total"].Should().Be(125.50);
    }

    [Fact]
    public async Task Preview_caps_at_max_rows()
    {
        var svc = new DesignerDataConnect();
        // 3 rows in the table, ask for 2 → should return 2.
        var result = await svc.PreviewAsync(
            DataConnectionKind.Sqlite,
            _connectionString,
            "SELECT id FROM customers ORDER BY id",
            maxRows: 2);

        result.Error.Should().BeNull();
        result.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Preview_binds_parameters_safely()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.PreviewAsync(
            DataConnectionKind.Sqlite,
            _connectionString,
            "SELECT name FROM customers WHERE total > $threshold ORDER BY total",
            parameters: new Dictionary<string, object?> { ["$threshold"] = 100.0 });

        result.Error.Should().BeNull();
        result.Rows.Select(r => r["name"]).Should().Equal("Ana", "Carla");
    }

    [Fact]
    public async Task InMemory_kind_short_circuits_without_a_connection()
    {
        var svc = new DesignerDataConnect();
        var test = await svc.TestConnectionAsync(DataConnectionKind.InMemory, "");
        test.Success.Should().BeTrue();

        var schema = await svc.DiscoverSchemaAsync(DataConnectionKind.InMemory, "", "SELECT 1");
        schema.Fields.Should().BeEmpty();
        schema.Error.Should().Contain("InMemory");

        var prev = await svc.PreviewAsync(DataConnectionKind.InMemory, "", "SELECT 1");
        prev.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Errors_in_sql_surface_as_Error_message_not_exceptions()
    {
        var svc = new DesignerDataConnect();
        var result = await svc.DiscoverSchemaAsync(
            DataConnectionKind.Sqlite,
            _connectionString,
            "SELECT * FROM no_such_table");

        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("no_such_table", because: "the actual SQLite error mentions the missing table");
        result.Fields.Should().BeEmpty();
    }

    // ─── DesignerDataSourceFactory ──────────────────────────────────────────────

    [Fact]
    public async Task Factory_builds_runtime_data_source_from_view_model()
    {
        var vm = new DesignerDataSource("Customers", Array.Empty<DesignerField>())
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT id, name, total FROM customers ORDER BY id",
        };

        var ds = DesignerDataSourceFactory.Build(vm);
        ds.Should().NotBeNull();

        var rows = new List<string>();
        await foreach (var row in ds!.ReadAsync())
        {
            rows.Add((string)row["name"]!);
        }
        rows.Should().Equal("Ana", "Beto", "Carla");
    }

    [Fact]
    public async Task Factory_resolves_report_parameter_bindings_at_runtime()
    {
        var vm = new DesignerDataSource("Customers", Array.Empty<DesignerField>())
        {
            Kind = DataConnectionKind.Sqlite,
            ConnectionString = _connectionString,
            Sql = "SELECT name FROM customers WHERE total > $threshold ORDER BY total",
        };
        vm.SqlParameters.Add(new DesignerSqlParameter("$threshold", reportParameter: "MinTotal"));

        // Pass the resolved runtime value through the report-parameter dictionary.
        var ds = DesignerDataSourceFactory.Build(vm,
            new Dictionary<string, object?> { ["MinTotal"] = 100.0 });

        ds.Should().NotBeNull();
        var names = new List<string>();
        await foreach (var row in ds!.ReadAsync())
        {
            names.Add((string)row["name"]!);
        }
        names.Should().Equal("Ana", "Carla");
    }

    [Fact]
    public void Factory_returns_null_for_in_memory_kind()
    {
        var vm = new DesignerDataSource("InMem", Array.Empty<DesignerField>());
        DesignerDataSourceFactory.Build(vm).Should().BeNull();
    }

    // ─── DesignerDataSource ↔ DataSourceDefinition round-trip ──────────────────

    [Fact]
    public void Definition_round_trip_preserves_connection_metadata()
    {
        var original = new DesignerDataSource("Sales",
        [
            new DesignerField("id",   DesignerFieldType.Number),
            new DesignerField("name", DesignerFieldType.Text),
        ])
        {
            Kind = DataConnectionKind.PostgreSql,
            ConnectionString = "Host=localhost;Database=erp",
            Sql = "SELECT id, name FROM sales WHERE region = @region",
            IsStoredProcedure = false,
            CommandTimeoutSeconds = 60,
        };
        original.SqlParameters.Add(new DesignerSqlParameter("@region", reportParameter: "Region"));
        original.SqlParameters.Add(new DesignerSqlParameter("@year",   literal: "2026"));

        var def = original.ToDefinition();
        var restored = DesignerDataSource.FromDefinition(def);

        restored.Name.Should().Be("Sales");
        restored.Kind.Should().Be(DataConnectionKind.PostgreSql);
        restored.ConnectionString.Should().Be("Host=localhost;Database=erp");
        restored.Sql.Should().Be("SELECT id, name FROM sales WHERE region = @region");
        restored.IsStoredProcedure.Should().BeFalse();
        restored.CommandTimeoutSeconds.Should().Be(60);

        restored.Fields.Select(f => f.Name).Should().Equal("id", "name");
        restored.SqlParameters.Should().HaveCount(2);
        var region = restored.SqlParameters.First(p => p.SqlName == "@region");
        region.ReportParameter.Should().Be("Region");
        region.Literal.Should().BeNull();
        var year = restored.SqlParameters.First(p => p.SqlName == "@year");
        year.ReportParameter.Should().BeNull();
        year.Literal.Should().Be("2026");
    }
}
