using Microsoft.Data.Sqlite;
using Reporting.CodeFirst;
using Reporting.DataSources.Sqlite;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Demonstrates the canonical database-backed report path: open a real SQLite database,
/// seed it with sample data, run a parameterised query, and bind the streaming result
/// to a code-first report. Mirrors how <see cref="Reporting.Samples.DatabaseReport"/>
/// works, but lives inside <c>Reporting.Samples.CodeFirst</c> so the generator loop
/// can produce its full set of outputs (PDF / XLSX / HTML / SVG / CSV / JSON / Markdown
/// / .repx / .repjson) for parity with every other sample.
/// </summary>
/// <remarks>
/// <para>Uses an <em>in-memory shared-cache</em> SQLite database — the connection string
/// <c>Data Source=file:Sample11?mode=memory&amp;cache=shared</c> lets independent
/// connections see the same database during the lifetime of the process. That's the only
/// configuration that works with the streaming "open connection per <c>ReadAsync</c>"
/// pattern used by <see cref="AdoNetDataSource"/>.</para>
///
/// <para>SQL parameters use the canonical <c>$name</c> prefix; the same source code works
/// against PostgreSQL / SQL Server / MySQL by swapping the connection factory.</para>
/// </remarks>
public static class Sample11_AdoNetSqlite
{
    // Holds the seed connection alive for the lifetime of the process so the
    // shared-cache database stays populated between Build() and PaginateAsync().
    private static SqliteConnection? _keepalive;
    private const string ConnectionString = "Data Source=file:Sample11Db?mode=memory&cache=shared";

    public static Report Build()
    {
        EnsureSeeded();

        var ds = new SqliteDataSource(
            "Pedidos",
            ConnectionString,
            sql: @"
                SELECT cliente,
                       produto,
                       quantidade,
                       preco_unitario,
                       (quantidade * preco_unitario) AS total,
                       data
                  FROM pedidos
                 WHERE data >= $inicio AND data <= $fim
                 ORDER BY cliente, data",
            parameters: new Dictionary<string, object?>
            {
                ["$inicio"] = "2026-05-01",
                ["$fim"]    = "2026-05-31",
            });

        return ReportBuilder
            .Create("Vendas SQLite (ADO.NET)")
            .Page(p => p.A4().Portrait().Margins(18))
            .Parameters(p => p
                .Add<DateTime>("DataInicio", prompt: "Data inicial",
                    defaultValue: new DateTime(2026, 5, 1))
                .Add<DateTime>("DataFim", prompt: "Data final",
                    defaultValue: new DateTime(2026, 5, 31)))
            .DataSource("Pedidos", ds)
            .ReportHeader(h => h.Height(28)
                .Text("Vendas · Fonte SQLite (ADO.NET)")
                    .At(0, 0).Size(174, 12)
                    .Font("Arial", 16, FontStyle.Bold)
                    .Center()
                .Text("Período: {Parameters.DataInicio:dd/MM/yyyy} a {Parameters.DataFim:dd/MM/yyyy}")
                    .At(0, 14).Size(174, 6)
                    .Center()
                    .Color(Color.Gray)
                .Line().From(0, 22).To(174, 22).Thickness(0.5))
            .PageHeader(h => h.Height(8)
                .Label("Produto")        .At(0, 0).Size(82, 6).Bold()
                .Label("Qtde")           .At(82, 0).Size(20, 6).Bold().AlignRight()
                .Label("Preço Unitário") .At(104, 0).Size(30, 6).Bold().AlignRight()
                .Label("Total")          .At(136, 0).Size(34, 6).Bold().AlignRight()
                .Line().From(0, 6).To(174, 6).Thickness(0.25))
            .Group("PorCliente", "Fields.cliente", g => g
                .Header(h => h.Height(10)
                    .Text("Cliente: {Fields.cliente}")
                        .At(0, 2).Size(174, 6)
                        .Font("Arial", 11, FontStyle.Bold)
                        .Color(Color.FromHex("#C2410C")))
                .Footer(f => f.Height(8)
                    .Line().From(0, 0).To(174, 0).Thickness(0.25)
                    .Text("Subtotal {Fields.cliente}: {Sum(Fields.total, 'Group'):C}")
                        .At(0, 1).Size(174, 6).AlignRight().Bold()))
            .Detail(d => d.Height(6)
                .Text("{Fields.produto}").At(0, 0).Size(80, 6)
                .Text("{Fields.quantidade:N2}").At(82, 0).Size(20, 6).AlignRight()
                .Text("{Fields.preco_unitario:C}").At(104, 0).Size(30, 6).AlignRight()
                .Text("{Fields.total:C}").At(136, 0).Size(34, 6).AlignRight())
            .DetailNoRows("Nenhum pedido no período selecionado.")
            .ReportFooter(f => f.Height(15)
                .Line().From(0, 0).To(174, 0).Thickness(0.5)
                .Text("Total geral: {Sum(Fields.total):C}")
                    .At(0, 2).Size(174, 10).Bold().AlignRight()
                    .Font("Arial", 11, FontStyle.Bold))
            .PageFooter(f => f.Height(8)
                .Text("OmniReport · Página {Page.Number} de {Page.Total}")
                    .At(0, 1).Size(174, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();
    }

    /// <summary>Lazy idempotent seed. Opens the shared-cache database the first time,
    /// creates the schema if missing, and inserts the canonical sample rows. Subsequent
    /// calls are no-ops — the table count check short-circuits.</summary>
    /// <remarks>
    /// Re-seeding the database every Build() would be wrong on two fronts: (1) the
    /// generator loop builds AND paginates each sample, so seeding inside Build() and
    /// then disposing would leave the source with nothing to read; (2) repeated seeds
    /// would multiply the rows. Keeping <see cref="_keepalive"/> as a process-wide field
    /// is intentional — disposed when the host process exits.
    /// </remarks>
    private static void EnsureSeeded()
    {
        if (_keepalive is not null) return;
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using (var seed = conn.CreateCommand())
        {
            seed.CommandText = @"
                CREATE TABLE IF NOT EXISTS pedidos (
                    id            INTEGER PRIMARY KEY,
                    cliente       TEXT NOT NULL,
                    produto       TEXT NOT NULL,
                    quantidade    REAL NOT NULL,
                    preco_unitario REAL NOT NULL,
                    data          TEXT NOT NULL
                );
                DELETE FROM pedidos;
                INSERT INTO pedidos (cliente, produto, quantidade, preco_unitario, data) VALUES
                    ('Ana Beatriz',  'Caneta Bic Azul',       10, 2.50,  '2026-05-03'),
                    ('Ana Beatriz',  'Caderno Brochura',       1, 27.40, '2026-05-04'),
                    ('Bruno Costa',  'Pasta Catálogo',         5, 14.90, '2026-05-08'),
                    ('Bruno Costa',  'Caneta Bic Vermelha',   12, 2.50,  '2026-05-09'),
                    ('Bruno Costa',  'Borracha Branca',       20, 1.30,  '2026-05-10'),
                    ('Carla Dias',   'Bloco de Notas A5',      3, 18.00, '2026-05-12'),
                    ('Carla Dias',   'Marcador CD',            6, 4.50,  '2026-05-15'),
                    ('Diego Lima',   'Régua 30cm',             2, 8.20,  '2026-05-20'),
                    ('Diego Lima',   'Apontador c/depósito',  10, 3.90,  '2026-05-22'),
                    ('Eduardo Sousa','Caixa Clips 100un',      4, 6.70,  '2026-05-25');";
            seed.ExecuteNonQuery();
        }
        // Keep the seed connection open — the shared cache keeps the in-memory database
        // alive only while at least one connection to it is open. Closing this would let
        // SQLite drop the database before the paginator queries it.
        _keepalive = conn;
    }
}
