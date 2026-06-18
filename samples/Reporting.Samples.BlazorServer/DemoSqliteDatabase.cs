using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Reporting.Samples.BlazorServer;

/// <summary>
/// Lazily-initialized SQLite file used by the "DB · SQLite" sample in the designer page.
/// Lives in <c>%TEMP%\omnireport-demo.db</c> so the file persists across requests but
/// resets when the temp folder is cleared. Seeded once on first access.
/// </summary>
public static class DemoSqliteDatabase
{
    private static readonly Lazy<string> _path = new(EnsureSeeded);

    /// <summary>Path to the SQLite file. The connection string is
    /// <c>Data Source={Path};Mode=ReadOnly</c> by default — designers/previews never write.</summary>
    public static string Path => _path.Value;

    /// <summary>Connection string usable by Microsoft.Data.Sqlite. Read-only so the demo
    /// doesn't accidentally mutate the file when several browser tabs are open.</summary>
    public static string ConnectionString => $"Data Source={Path};Mode=ReadOnly;Cache=Shared";

    private static string EnsureSeeded()
    {
        var dbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "omnireport-demo.db");
        // Re-create on every cold start so the demo always shows fresh data
        // (avoids stale schemas after sample evolution).
        if (File.Exists(dbPath))
        {
            try { File.Delete(dbPath); } catch { /* in use — fine, keep existing seed */ }
        }

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE clientes (
                id    INTEGER PRIMARY KEY,
                nome  TEXT NOT NULL,
                cidade TEXT NOT NULL
            );
            CREATE TABLE pedidos (
                id             INTEGER PRIMARY KEY,
                cliente_id     INTEGER NOT NULL REFERENCES clientes(id),
                produto        TEXT NOT NULL,
                quantidade     REAL NOT NULL,
                preco_unitario REAL NOT NULL,
                data           TEXT NOT NULL
            );
            CREATE INDEX ix_pedidos_cliente ON pedidos(cliente_id);
            CREATE INDEX ix_pedidos_data    ON pedidos(data);

            INSERT INTO clientes (id, nome, cidade) VALUES
                (1, 'Ana Beatriz',  'São Paulo'),
                (2, 'Beto Silva',   'Curitiba'),
                (3, 'Carla Souza',  'Belo Horizonte'),
                (4, 'Daniel Lima',  'Recife'),
                (5, 'Eva Pinto',    'Porto Alegre');

            INSERT INTO pedidos (cliente_id, produto, quantidade, preco_unitario, data) VALUES
                (1, 'Caneta Bic Azul',      10, 2.50,  '2026-05-03'),
                (1, 'Caderno Brochura',      1, 27.40, '2026-05-04'),
                (1, 'Marcador de Texto',     3, 11.90, '2026-05-06'),
                (2, 'Caneta Bic Vermelha',   5, 2.50,  '2026-05-07'),
                (2, 'Borracha',              8, 1.20,  '2026-05-09'),
                (3, 'Mochila Escolar',       1, 189.00,'2026-05-10'),
                (3, 'Estojo',                1, 35.50, '2026-05-11'),
                (4, 'Caderno Universitário', 2, 42.80, '2026-05-13'),
                (4, 'Lapiseira 0.7mm',       4, 18.90, '2026-05-14'),
                (5, 'Régua 30cm',            2, 5.40,  '2026-05-17');
        """;
        cmd.ExecuteNonQuery();
        return dbPath;
    }
}
