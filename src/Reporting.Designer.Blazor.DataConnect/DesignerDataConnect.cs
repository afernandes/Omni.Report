using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Reporting.Designer.Blazor.ViewModels;

namespace Reporting.Designer.Blazor.DataConnect;

/// <summary>
/// Default <see cref="IDesignerDataConnect"/> implementation. Switches on
/// <see cref="DataConnectionKind"/> to build a provider-specific <see cref="DbConnection"/>,
/// then talks to it through <see cref="System.Data.Common"/> only.
/// </summary>
/// <remarks>
/// <para>Register in DI:
/// <code>services.AddSingleton&lt;IDesignerDataConnect, DesignerDataConnect&gt;();</code>
/// </para>
/// <para>Connection strings may contain <c>{secret:NAME}</c> placeholders — when an
/// <see cref="ISecretResolver"/> is registered, the placeholders are expanded before opening
/// the connection. Without a resolver, placeholders fall back to environment variables.</para>
/// <para>Errors never throw out: each method returns a record with a non-null
/// <c>Error</c>/<c>Message</c> string on failure. The dialog renders those inline.</para>
/// </remarks>
public sealed class DesignerDataConnect : IDesignerDataConnect
{
    private readonly ISecretResolver _secretResolver;

    public DesignerDataConnect(ISecretResolver? secretResolver = null)
    {
        _secretResolver = secretResolver ?? new EnvironmentSecretResolver();
    }

    public async Task<TestConnectionResult> TestConnectionAsync(
        DataConnectionKind kind,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (kind == DataConnectionKind.InMemory)
        {
            return new TestConnectionResult(true, "Conexão in-memory (nenhum banco real).", TimeSpan.Zero);
        }
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new TestConnectionResult(false, "Connection string vazia.", TimeSpan.Zero);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var expanded = await ExpandSecretsAsync(connectionString, cancellationToken).ConfigureAwait(false);
            await using var connection = CreateConnection(kind, expanded);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return new TestConnectionResult(true, $"Conectado em {sw.ElapsedMilliseconds} ms.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestConnectionResult(false, ex.Message, sw.Elapsed);
        }
    }

    public async Task<SchemaDiscoveryResult> DiscoverSchemaAsync(
        DataConnectionKind kind,
        string connectionString,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (kind == DataConnectionKind.InMemory)
        {
            return new SchemaDiscoveryResult(Array.Empty<DiscoveredField>(), TimeSpan.Zero,
                "InMemory: defina os campos manualmente ou via JSON.");
        }
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new SchemaDiscoveryResult(Array.Empty<DiscoveredField>(), TimeSpan.Zero, "SQL vazio.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var expanded = await ExpandSecretsAsync(connectionString, cancellationToken).ConfigureAwait(false);
            await using var connection = CreateConnection(kind, expanded);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            BindParameters(cmd, parameters);

            await using var reader = await cmd.ExecuteReaderAsync(
                CommandBehavior.SchemaOnly | CommandBehavior.SingleResult,
                cancellationToken).ConfigureAwait(false);

            var fields = ExtractSchema(reader);
            sw.Stop();
            return new SchemaDiscoveryResult(fields, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new SchemaDiscoveryResult(Array.Empty<DiscoveredField>(), sw.Elapsed, ex.Message);
        }
    }

    public async Task<DataPreviewResult> PreviewAsync(
        DataConnectionKind kind,
        string connectionString,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int maxRows = 50,
        CancellationToken cancellationToken = default)
    {
        if (kind == DataConnectionKind.InMemory)
        {
            return new DataPreviewResult(Array.Empty<DiscoveredField>(),
                Array.Empty<IReadOnlyDictionary<string, object?>>(),
                TimeSpan.Zero,
                "InMemory: preview indisponível (sem banco).");
        }
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new DataPreviewResult(Array.Empty<DiscoveredField>(),
                Array.Empty<IReadOnlyDictionary<string, object?>>(),
                TimeSpan.Zero, "SQL vazio.");
        }
        if (maxRows <= 0) maxRows = 50;

        var sw = Stopwatch.StartNew();
        try
        {
            var expanded = await ExpandSecretsAsync(connectionString, cancellationToken).ConfigureAwait(false);
            await using var connection = CreateConnection(kind, expanded);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            BindParameters(cmd, parameters);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var fields = ExtractSchema(reader);
            var rows = new List<IReadOnlyDictionary<string, object?>>(Math.Min(maxRows, 256));
            int read = 0;
            while (read < maxRows && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var dict = new Dictionary<string, object?>(fields.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fields.Count; i++)
                {
                    dict[fields[i].Name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(dict);
                read++;
            }
            sw.Stop();
            return new DataPreviewResult(fields, rows, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DataPreviewResult(Array.Empty<DiscoveredField>(),
                Array.Empty<IReadOnlyDictionary<string, object?>>(),
                sw.Elapsed, ex.Message);
        }
    }

    public async Task<SchemaExplorerResult> ListSchemaAsync(
        DataConnectionKind kind,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (kind == DataConnectionKind.InMemory)
        {
            return new SchemaExplorerResult(Array.Empty<DatabaseTable>(), TimeSpan.Zero,
                "InMemory: nenhum banco para explorar.");
        }
        var sw = Stopwatch.StartNew();
        try
        {
            var expanded = await ExpandSecretsAsync(connectionString, cancellationToken).ConfigureAwait(false);
            await using var connection = CreateConnection(kind, expanded);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var tables = await ReadSchemaAsync(kind, connection, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return new SchemaExplorerResult(tables, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new SchemaExplorerResult(Array.Empty<DatabaseTable>(), sw.Elapsed, ex.Message);
        }
    }

    public async Task<StoredProcedureSignatureResult> DiscoverProcedureSignatureAsync(
        DataConnectionKind kind,
        string connectionString,
        string procedureName,
        CancellationToken cancellationToken = default)
    {
        if (kind == DataConnectionKind.InMemory || kind == DataConnectionKind.Sqlite)
        {
            return new StoredProcedureSignatureResult(Array.Empty<StoredProcedureParameter>(), TimeSpan.Zero,
                kind == DataConnectionKind.Sqlite ? "SQLite não suporta stored procedures." : "InMemory: indisponível.");
        }
        if (string.IsNullOrWhiteSpace(procedureName))
        {
            return new StoredProcedureSignatureResult(Array.Empty<StoredProcedureParameter>(), TimeSpan.Zero,
                "Nome do procedure vazio.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var expanded = await ExpandSecretsAsync(connectionString, cancellationToken).ConfigureAwait(false);
            await using var connection = CreateConnection(kind, expanded);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var paramList = await ReadProcedureParamsAsync(kind, connection, procedureName, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return new StoredProcedureSignatureResult(paramList, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StoredProcedureSignatureResult(Array.Empty<StoredProcedureParameter>(), sw.Elapsed, ex.Message);
        }
    }

    // ── schema-explorer queries (per provider) ────────────────────────────────

    private static async Task<IReadOnlyList<DatabaseTable>> ReadSchemaAsync(
        DataConnectionKind kind, DbConnection connection, CancellationToken ct)
    {
        // We rely on the ADO.NET schema-collection API where possible — every supported
        // provider implements GetSchema("Tables") / GetSchema("Columns") with provider-specific
        // column names. We normalize to a common DatabaseTable shape.
        switch (kind)
        {
            case DataConnectionKind.Sqlite:
                return await ReadSchemaSqliteAsync(connection, ct).ConfigureAwait(false);
            case DataConnectionKind.PostgreSql:
                return await ReadSchemaPostgresAsync(connection, ct).ConfigureAwait(false);
            case DataConnectionKind.SqlServer:
                return await ReadSchemaSqlServerAsync(connection, ct).ConfigureAwait(false);
            case DataConnectionKind.MySql:
                return await ReadSchemaMySqlAsync(connection, ct).ConfigureAwait(false);
            default:
                return Array.Empty<DatabaseTable>();
        }
    }

    private static async Task<IReadOnlyList<DatabaseTable>> ReadSchemaSqliteAsync(DbConnection connection, CancellationToken ct)
    {
        // sqlite_master gives table+view list; PRAGMA table_info() per object gives columns.
        var tables = new List<DatabaseTable>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT name, type FROM sqlite_master WHERE type IN ('table','view') AND name NOT LIKE 'sqlite_%' ORDER BY name";
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var name = reader.GetString(0);
                var type = reader.GetString(1);
                tables.Add(new DatabaseTable(string.Empty, name, type == "view", Array.Empty<DatabaseColumn>()));
            }
        }
        // Now resolve columns for each. PRAGMA returns: cid|name|type|notnull|dflt_value|pk
        var enriched = new List<DatabaseTable>(tables.Count);
        foreach (var t in tables)
        {
            var cols = new List<DatabaseColumn>();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{t.Name.Replace("\"", "\"\"")}\")";
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var cname = reader.GetString(1);
                var ctype = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
                var notNull = !reader.IsDBNull(3) && reader.GetInt32(3) != 0;
                var isPk = !reader.IsDBNull(5) && reader.GetInt32(5) != 0;
                cols.Add(new DatabaseColumn(cname, ctype, !notNull, isPk));
            }
            enriched.Add(t with { Columns = cols });
        }
        return enriched;
    }

    private static async Task<IReadOnlyList<DatabaseTable>> ReadSchemaPostgresAsync(DbConnection connection, CancellationToken ct)
    {
        // information_schema.tables + columns; exclude pg_catalog / information_schema.
        const string sql = @"
SELECT t.table_schema, t.table_name, t.table_type, c.column_name, c.data_type, c.is_nullable,
       (CASE WHEN k.column_name IS NOT NULL THEN 1 ELSE 0 END) AS is_pk
FROM information_schema.tables t
JOIN information_schema.columns c
  ON c.table_schema = t.table_schema AND c.table_name = t.table_name
LEFT JOIN (
  SELECT kcu.table_schema, kcu.table_name, kcu.column_name
  FROM information_schema.table_constraints tc
  JOIN information_schema.key_column_usage kcu
    ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
  WHERE tc.constraint_type = 'PRIMARY KEY'
) k ON k.table_schema = t.table_schema AND k.table_name = t.table_name AND k.column_name = c.column_name
WHERE t.table_schema NOT IN ('pg_catalog','information_schema')
ORDER BY t.table_schema, t.table_name, c.ordinal_position";
        return await ReadGroupedSchemaAsync(connection, sql, ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<DatabaseTable>> ReadSchemaSqlServerAsync(DbConnection connection, CancellationToken ct)
    {
        const string sql = @"
SELECT TABLE_SCHEMA AS table_schema, TABLE_NAME AS table_name, TABLE_TYPE AS table_type,
       c.COLUMN_NAME AS column_name, c.DATA_TYPE AS data_type, c.IS_NULLABLE AS is_nullable,
       CASE WHEN kcu.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS is_pk
FROM INFORMATION_SCHEMA.TABLES t
JOIN INFORMATION_SCHEMA.COLUMNS c
  ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
LEFT JOIN (
  SELECT kcu.TABLE_SCHEMA, kcu.TABLE_NAME, kcu.COLUMN_NAME
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
  JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
    ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND kcu.TABLE_SCHEMA = tc.TABLE_SCHEMA
  WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
) kcu ON kcu.TABLE_SCHEMA = t.TABLE_SCHEMA AND kcu.TABLE_NAME = t.TABLE_NAME AND kcu.COLUMN_NAME = c.COLUMN_NAME
WHERE t.TABLE_SCHEMA NOT IN ('sys','INFORMATION_SCHEMA')
ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";
        return await ReadGroupedSchemaAsync(connection, sql, ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<DatabaseTable>> ReadSchemaMySqlAsync(DbConnection connection, CancellationToken ct)
    {
        const string sql = @"
SELECT t.table_schema, t.table_name, t.table_type,
       c.column_name, c.data_type, c.is_nullable,
       (c.column_key = 'PRI') AS is_pk
FROM information_schema.tables t
JOIN information_schema.columns c
  ON c.table_schema = t.table_schema AND c.table_name = t.table_name
WHERE t.table_schema = DATABASE()
ORDER BY t.table_schema, t.table_name, c.ordinal_position";
        return await ReadGroupedSchemaAsync(connection, sql, ct).ConfigureAwait(false);
    }

    /// <summary>Common reader: query returns one row per (table, column) ordered by
    /// (schema, table, ordinal). We group into <see cref="DatabaseTable"/> instances on the fly.</summary>
    private static async Task<IReadOnlyList<DatabaseTable>> ReadGroupedSchemaAsync(
        DbConnection connection, string sql, CancellationToken ct)
    {
        var tables = new List<DatabaseTable>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);

        string? currentSchema = null;
        string? currentName = null;
        bool currentIsView = false;
        List<DatabaseColumn>? currentCols = null;

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var schema = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var name = reader.GetString(1);
            var tableType = reader.IsDBNull(2) ? "BASE TABLE" : reader.GetString(2);
            var isView = tableType.Contains("VIEW", StringComparison.OrdinalIgnoreCase);

            if (schema != currentSchema || name != currentName)
            {
                FlushTable(tables, currentSchema, currentName, currentIsView, currentCols);
                currentSchema = schema;
                currentName = name;
                currentIsView = isView;
                currentCols = new List<DatabaseColumn>();
            }

            var colName = reader.GetString(3);
            var dataType = reader.IsDBNull(4) ? "unknown" : reader.GetString(4);
            var nullable = !reader.IsDBNull(5) &&
                          (reader.GetValue(5).ToString() is "YES" or "1" or "True");
            var isPk = !reader.IsDBNull(6) && Convert.ToInt32(reader.GetValue(6)) == 1;
            currentCols!.Add(new DatabaseColumn(colName, dataType, nullable, isPk));
        }
        FlushTable(tables, currentSchema, currentName, currentIsView, currentCols);
        return tables;

        static void FlushTable(List<DatabaseTable> sink, string? schema, string? name, bool isView, List<DatabaseColumn>? cols)
        {
            if (name is null) return;
            sink.Add(new DatabaseTable(schema ?? string.Empty, name, isView, cols ?? new List<DatabaseColumn>()));
        }
    }

    // ── stored-procedure signature discovery ──────────────────────────────────

    private static async Task<IReadOnlyList<StoredProcedureParameter>> ReadProcedureParamsAsync(
        DataConnectionKind kind, DbConnection connection, string procedureName, CancellationToken ct)
    {
        // Use the DbCommandBuilder.DeriveParameters extension when the provider supplies it
        // — supports PostgreSQL (Npgsql), SQL Server (Microsoft.Data.SqlClient), and MySQL
        // (MySqlConnector). It's a single line that knows the right catalog query for each.
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = procedureName;
        cmd.CommandType = CommandType.StoredProcedure;

        // Each provider exposes DeriveParameters on its own factory's command builder type;
        // System.Data.Common.DbCommandBuilder is abstract. Dispatch by kind:
        switch (kind)
        {
            case DataConnectionKind.PostgreSql:
                NpgsqlCommandBuilder.DeriveParameters((NpgsqlCommand)cmd);
                break;
            case DataConnectionKind.SqlServer:
                SqlCommandBuilder.DeriveParameters((SqlCommand)cmd);
                break;
            case DataConnectionKind.MySql:
                MySqlCommandBuilder.DeriveParameters((MySqlCommand)cmd);
                break;
            default:
                return Array.Empty<StoredProcedureParameter>();
        }

        var list = new List<StoredProcedureParameter>(cmd.Parameters.Count);
        foreach (DbParameter p in cmd.Parameters)
        {
            // The returnvalue parameter (SQL Server) shows up first — skip it.
            if (p.Direction == ParameterDirection.ReturnValue) continue;

            // Try to derive a useful CLR type from DbType. Not always 1:1 but close enough
            // for designer hints.
            var clr = DbTypeToClr(p.DbType);
            var isOutput = p.Direction is ParameterDirection.Output or ParameterDirection.InputOutput;
            list.Add(new StoredProcedureParameter(p.ParameterName, clr, isOutput, p.IsNullable, p.DbType.ToString()));
            await Task.Yield(); // keep loop cancellable
            ct.ThrowIfCancellationRequested();
        }
        return list;
    }

    private static Type DbTypeToClr(DbType t) => t switch
    {
        DbType.Boolean => typeof(bool),
        DbType.Byte or DbType.SByte => typeof(byte),
        DbType.Int16 or DbType.UInt16 => typeof(short),
        DbType.Int32 or DbType.UInt32 => typeof(int),
        DbType.Int64 or DbType.UInt64 => typeof(long),
        DbType.Single => typeof(float),
        DbType.Double => typeof(double),
        DbType.Decimal or DbType.Currency or DbType.VarNumeric => typeof(decimal),
        DbType.Date or DbType.DateTime or DbType.DateTime2 or DbType.DateTimeOffset => typeof(DateTime),
        DbType.Time => typeof(TimeSpan),
        DbType.Guid => typeof(Guid),
        DbType.Binary => typeof(byte[]),
        _ => typeof(string),
    };

    // ── helpers ───────────────────────────────────────────────────────────────

    private async ValueTask<string> ExpandSecretsAsync(string connectionString, CancellationToken ct)
    {
        if (!SecretTemplate.ContainsPlaceholder(connectionString)) return connectionString;
        return await SecretTemplate.ExpandAsync(connectionString, _secretResolver, missing: null, ct).ConfigureAwait(false);
    }

    private static DbConnection CreateConnection(DataConnectionKind kind, string connectionString)
        => kind switch
        {
            DataConnectionKind.Sqlite     => new SqliteConnection(connectionString),
            DataConnectionKind.PostgreSql => new NpgsqlConnection(connectionString),
            DataConnectionKind.SqlServer  => new SqlConnection(connectionString),
            DataConnectionKind.MySql      => new MySqlConnection(connectionString),
            _ => throw new NotSupportedException($"Provider '{kind}' não é suportado por esta build do designer."),
        };

    private static void BindParameters(DbCommand cmd, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null || parameters.Count == 0) return;
        foreach (var kv in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = kv.Key;
            p.Value = kv.Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    private static IReadOnlyList<DiscoveredField> ExtractSchema(DbDataReader reader)
    {
        var list = new List<DiscoveredField>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            var clr = reader.GetFieldType(i) ?? typeof(object);
            string? providerType = null;
            try { providerType = reader.GetDataTypeName(i); } catch { /* not every provider supports it */ }
            list.Add(new DiscoveredField(name, clr, providerType));
        }
        return list;
    }
}
