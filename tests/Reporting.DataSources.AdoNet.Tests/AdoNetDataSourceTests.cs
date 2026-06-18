using FluentAssertions;
using Microsoft.Data.Sqlite;
using Reporting.DataSources;
using Reporting.DataSources.AdoNet;
using Reporting.DataSources.Sqlite;
using Xunit;

namespace Reporting.DataSources.AdoNet.Tests;

/// <summary>
/// End-to-end tests for the ADO.NET data source. SQLite in-memory is the test backbone — no
/// external service required, runs identically on Windows / Linux / CI.
/// </summary>
public class AdoNetDataSourceTests
{
    /// <summary>Boots an in-memory SQLite database with a seeded <c>orders</c> table.
    /// Returns the open connection; caller disposes.</summary>
    private static SqliteConnection NewSeededConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE orders (
                id INTEGER PRIMARY KEY,
                customer TEXT NOT NULL,
                total REAL NOT NULL,
                placed_at TEXT NOT NULL
            );
            INSERT INTO orders (id, customer, total, placed_at) VALUES
                (1, 'Ana',  120.50, '2026-05-01'),
                (2, 'Beto',  45.00, '2026-05-02'),
                (3, 'Ana',   89.90, '2026-05-03'),
                (4, 'Carla', 350.00, '2026-05-04');";
        cmd.ExecuteNonQuery();
        return conn;
    }

    [Fact]
    public async Task Streams_rows_with_inferred_schema()
    {
        using var conn = NewSeededConnection();
        var source = new SqliteDataSource("Vendas", conn, "SELECT id, customer, total FROM orders ORDER BY id");

        var rows = new List<IReportRecord>();
        await foreach (var row in source.ReadAsync())
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(4);
        // Schema is populated as a side-effect of iterating.
        source.Schema.Fields.Should().HaveCount(3);
        source.Schema.Fields[0].Name.Should().Be("id");
        source.Schema.Fields[1].Name.Should().Be("customer");
        source.Schema.Fields[2].Name.Should().Be("total");

        // Spot-check column types — Microsoft.Data.Sqlite reports id as long, total as double.
        source.Schema.Fields[0].Type.Should().Be(typeof(long));
        source.Schema.Fields[2].Type.Should().Be(typeof(double));
    }

    [Fact]
    public async Task Records_expose_fields_by_name_and_ordinal()
    {
        using var conn = NewSeededConnection();
        var source = new SqliteDataSource("Vendas", conn, "SELECT id, customer, total FROM orders WHERE id = 1");

        var record = await source.ReadAsync().FirstAsync();

        record["id"].Should().Be(1L);
        record["customer"].Should().Be("Ana");
        record["total"].Should().Be(120.50);

        record[0].Should().Be(1L);
        record[1].Should().Be("Ana");
        record[2].Should().Be(120.50);

        // Unknown field returns null (matches IReportRecord contract — never throws).
        record["nope"].Should().BeNull();
        record[42].Should().BeNull();
    }

    [Fact]
    public async Task Parameter_binding_uses_DbParameter_not_string_concat()
    {
        using var conn = NewSeededConnection();
        var source = new SqliteDataSource(
            "Vendas",
            conn,
            "SELECT customer, total FROM orders WHERE customer = $name ORDER BY total",
            new Dictionary<string, object?> { ["$name"] = "Ana" });

        var customers = new List<string>();
        var totals = new List<double>();
        await foreach (var row in source.ReadAsync())
        {
            customers.Add((string)row["customer"]!);
            totals.Add((double)row["total"]!);
        }

        customers.Should().BeEquivalentTo(["Ana", "Ana"]);
        totals.Should().BeEquivalentTo([89.90, 120.50]);
    }

    [Fact]
    public async Task Null_values_surface_as_clr_null_not_DBNull()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE t (a INTEGER, b TEXT);
                INSERT INTO t VALUES (1, NULL), (NULL, 'hello');";
            cmd.ExecuteNonQuery();
        }

        var source = new SqliteDataSource("T", conn, "SELECT a, b FROM t ORDER BY rowid");
        var rows = await source.ReadAsync().ToListAsync();

        rows[0]["a"].Should().Be(1L);
        rows[0]["b"].Should().BeNull();
        rows[1]["a"].Should().BeNull();
        rows[1]["b"].Should().Be("hello");
    }

    [Fact]
    public async Task Empty_result_set_yields_no_rows_but_still_populates_schema()
    {
        using var conn = NewSeededConnection();
        var source = new SqliteDataSource("Vendas", conn, "SELECT id, customer FROM orders WHERE 1 = 0");

        var rows = await source.ReadAsync().ToListAsync();

        rows.Should().BeEmpty();
        source.Schema.Fields.Should().HaveCount(2);
        source.Schema.Fields.Select(f => f.Name).Should().Equal("id", "customer");
    }

    [Fact]
    public async Task Cancellation_aborts_iteration_promptly()
    {
        using var conn = NewSeededConnection();
        var source = new SqliteDataSource("Vendas", conn, "SELECT id FROM orders ORDER BY id");

        using var cts = new CancellationTokenSource();
        var read = 0;

        var act = async () =>
        {
            await foreach (var row in source.ReadAsync(cts.Token))
            {
                read++;
                if (read == 2) cts.Cancel();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        // Either 2 (cancelled before checking next iter) or 3 (one more snuck through) — both fine.
        read.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Factory_mode_opens_and_disposes_connection_per_read()
    {
        // Use a shared-cache in-memory database so multiple connections see the same data.
        // Factory creates a fresh connection per ReadAsync call.
        var connectionString = $"Data Source=file:test_{Guid.NewGuid():N}?mode=memory&cache=shared";

        // Keep ONE connection alive for the duration of the test — :memory: + shared-cache
        // requires at least one open connection or the DB is freed between calls.
        using var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();
        using (var seed = keepAlive.CreateCommand())
        {
            seed.CommandText = "CREATE TABLE t (v INTEGER); INSERT INTO t VALUES (10), (20), (30);";
            seed.ExecuteNonQuery();
        }

        var source = new AdoNetDataSource(
            "T",
            () => new SqliteConnection(connectionString),
            "SELECT v FROM t ORDER BY v");

        // First iteration.
        var first = await source.ReadAsync().Select(r => (long)r["v"]!).ToListAsync();
        first.Should().Equal(10L, 20L, 30L);

        // Second iteration — connection is recycled (or recreated) but the data is intact.
        var second = await source.ReadAsync().Select(r => (long)r["v"]!).ToListAsync();
        second.Should().Equal(10L, 20L, 30L);
    }

    [Fact]
    public async Task Borrowed_connection_is_never_closed_by_source()
    {
        using var conn = NewSeededConnection();
        var source = new SqliteDataSource("Vendas", conn, "SELECT id FROM orders");

        // Iterate fully.
        _ = await source.ReadAsync().ToListAsync();

        // The borrowed connection must still be usable afterward.
        conn.State.Should().Be(System.Data.ConnectionState.Open);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM orders";
        var count = (long)cmd.ExecuteScalar()!;
        count.Should().Be(4L);
    }
}

internal static class AsyncEnumerableExtensions
{
    public static async ValueTask<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    public static async ValueTask<T> FirstAsync<T>(this IAsyncEnumerable<T> source)
    {
        await foreach (var item in source) return item;
        throw new InvalidOperationException("Sequence contains no elements.");
    }

    public static IAsyncEnumerable<TResult> Select<TSource, TResult>(
        this IAsyncEnumerable<TSource> source, Func<TSource, TResult> selector)
        => SelectImpl(source, selector);

    private static async IAsyncEnumerable<TResult> SelectImpl<TSource, TResult>(
        IAsyncEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        await foreach (var item in source) yield return selector(item);
    }
}
