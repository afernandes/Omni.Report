using FluentAssertions;
using Microsoft.Data.Sqlite;
using Reporting.CodeFirst;
using Reporting.DataSources;
using Reporting.DataSources.Sqlite;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.DataSources.Sqlite.Tests;

/// <summary>
/// End-to-end tests for <see cref="SqliteDataSource"/> against a real on-disk SQLite database,
/// exercising the factory constructor (a fresh connection per read — the production path) and
/// the full report pipeline (provider → paginator → primitives).
/// </summary>
/// <remarks>
/// PostgreSQL, SQL Server and MySQL share the same <c>AdoNetDataSource</c> code path that these
/// SQLite tests cover; provider-specific container-backed E2E (Testcontainers) belongs in CI
/// where a Docker daemon is available, and is intentionally not run here.
/// </remarks>
public class SqliteDataSourceE2ETests
{
    private static string CreateTempDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"omnireport_{Guid.NewGuid():N}.sqlite");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE produtos (id INTEGER PRIMARY KEY, nome TEXT NOT NULL, preco REAL NOT NULL, ativo INTEGER);
                INSERT INTO produtos (id, nome, preco, ativo) VALUES
                    (1, 'Caneta', 2.5, 1),
                    (2, 'Caderno', 27.4, 1),
                    (3, 'Lápis', 1.2, 0);";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        return path;
    }

    private static void Cleanup(string path)
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort — it's a temp file; the OS reclaims it later.
        }
    }

    private static async Task<List<IReportRecord>> ReadAll(IReportDataSource source)
    {
        var list = new List<IReportRecord>();
        await foreach (var r in source.ReadAsync())
        {
            list.Add(r);
        }
        return list;
    }

    [Fact]
    public async Task File_backed_source_streams_rows_with_a_fresh_connection_per_read()
    {
        var path = CreateTempDb();
        try
        {
            var source = new SqliteDataSource("Produtos", $"Data Source={path}",
                "SELECT id, nome, preco FROM produtos ORDER BY id");

            var first = await ReadAll(source);
            first.Should().HaveCount(3);
            first[0]["nome"].Should().Be("Caneta");
            first[0]["preco"].Should().Be(2.5);

            // Factory mode opens a brand-new connection each call — a second read must still work.
            var second = await ReadAll(source);
            second.Should().HaveCount(3);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task File_backed_schema_infers_sqlite_column_types()
    {
        var path = CreateTempDb();
        try
        {
            var source = new SqliteDataSource("Produtos", $"Data Source={path}",
                "SELECT id, nome, preco FROM produtos");
            _ = await ReadAll(source);

            source.Schema.Fields.Select(f => f.Name).Should().Equal("id", "nome", "preco");
            source.Schema.Fields[0].Type.Should().Be(typeof(long));
            source.Schema.Fields[1].Type.Should().Be(typeof(string));
            source.Schema.Fields[2].Type.Should().Be(typeof(double));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task File_backed_parameterized_query_filters_rows()
    {
        var path = CreateTempDb();
        try
        {
            var source = new SqliteDataSource("Produtos", $"Data Source={path}",
                "SELECT nome FROM produtos WHERE ativo = $ativo ORDER BY id",
                new Dictionary<string, object?> { ["$ativo"] = 1 });

            var rows = await ReadAll(source);
            rows.Select(r => (string)r["nome"]!).Should().Equal("Caneta", "Caderno");
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Report_paginates_end_to_end_from_a_sqlite_source()
    {
        var path = CreateTempDb();
        try
        {
            var source = new SqliteDataSource("Produtos", $"Data Source={path}",
                "SELECT nome, preco FROM produtos ORDER BY id");

            var report = ReportBuilder.Create("DB")
                .Page(p => p.A4().Portrait().Margins(15))
                .DataSource("Produtos", source)
                .Detail(d => d.Height(6)
                    .Text("{Fields.nome}").At(0, 0).Size(60, 6)
                    .Text("{Fields.preco:C}").At(60, 0).Size(40, 6))
                .Build();

            var rendered = await report.PaginateAsync();
            var texts = rendered.Pages.SelectMany(p => p.Primitives)
                .OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

            texts.Should().Contain("Caneta");
            texts.Should().Contain("Caderno");
            texts.Should().Contain(t => t.Contains("R$"), "the price column renders with the :C currency format");
        }
        finally
        {
            Cleanup(path);
        }
    }
}
