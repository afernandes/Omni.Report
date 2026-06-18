using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Reporting.DataSources.AdoNet;

/// <summary>
/// Streaming <see cref="IReportDataSource"/> backed by any ADO.NET provider. Wraps a
/// <see cref="DbDataReader"/> — rows are pulled lazily via <see cref="DbDataReader.ReadAsync(CancellationToken)"/>,
/// so reports with millions of rows don't materialize the whole result set in memory.
/// </summary>
/// <remarks>
/// <para>Connection lifecycle (two modes):</para>
/// <list type="bullet">
/// <item><b>Factory mode</b> — caller provides a <c>Func&lt;DbConnection&gt;</c>. Each
/// <see cref="ReadAsync"/> call creates a fresh connection, opens it, runs the query, and
/// disposes the connection. Recommended for most cases (works correctly with connection
/// pooling).</item>
/// <item><b>Borrowed-connection mode</b> — caller passes an already-opened
/// <see cref="DbConnection"/>. We never close it. Use when you have an existing
/// transaction or unit-of-work boundary.</item>
/// </list>
///
/// <para><b>Schema discovery is lazy</b>: <see cref="Schema"/> returns an empty schema
/// until the first <see cref="ReadAsync"/> iteration produces a reader; thereafter it
/// reflects the actual reader columns. Pass an explicit <c>schema</c> to the constructor
/// when you need <see cref="Schema"/> populated up-front (e.g. designer field tree).</para>
///
/// <para>Provider-agnostic — works with Npgsql, Microsoft.Data.SqlClient,
/// Microsoft.Data.Sqlite, MySqlConnector, Oracle.ManagedDataAccess, etc., because it only
/// touches the <see cref="System.Data.Common"/> abstractions.</para>
/// </remarks>
public sealed class AdoNetDataSource : IReportDataSource
{
    private readonly Func<DbConnection>? _connectionFactory;
    private readonly DbConnection? _borrowedConnection;
    private readonly AdoNetQuery _query;
    private IReportRecordSchema _schema;

    /// <summary>Creates a source that opens a fresh connection per <see cref="ReadAsync"/> call
    /// via the supplied factory. The connection is closed and disposed when iteration finishes.</summary>
    public AdoNetDataSource(
        string name,
        Func<DbConnection> connectionFactory,
        AdoNetQuery query,
        IReportRecordSchema? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(query);
        Name = name;
        _connectionFactory = connectionFactory;
        _query = query;
        _schema = schema ?? EmptySchema.Instance;
    }

    /// <summary>Convenience overload: takes raw SQL + optional parameter dictionary.</summary>
    public AdoNetDataSource(
        string name,
        Func<DbConnection> connectionFactory,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null)
        : this(name, connectionFactory, new AdoNetQuery(sql, parameters))
    {
    }

    /// <summary>Creates a source that reuses an existing (caller-owned) connection. The
    /// connection is never opened or closed by this source — caller is responsible for both.</summary>
    public AdoNetDataSource(
        string name,
        DbConnection connection,
        AdoNetQuery query,
        IReportRecordSchema? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);
        Name = name;
        _borrowedConnection = connection;
        _query = query;
        _schema = schema ?? EmptySchema.Instance;
    }

    public string Name { get; }

    /// <summary>The current schema. Empty until the first <see cref="ReadAsync"/> iteration
    /// runs the query (and populates it from the <see cref="DbDataReader"/>).</summary>
    public IReportRecordSchema Schema => _schema;

    public async IAsyncEnumerable<IReportRecord> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DbConnection connection;
        bool ownsConnection;
        if (_borrowedConnection is not null)
        {
            connection = _borrowedConnection;
            ownsConnection = false;
        }
        else
        {
            connection = _connectionFactory!();
            ownsConnection = true;
        }

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = _query.Sql;
            command.CommandType = _query.CommandType;
            if (_query.CommandTimeoutSeconds is int timeout)
            {
                command.CommandTimeout = timeout;
            }
            if (_query.Parameters is { Count: > 0 } parameters)
            {
                foreach (var kv in parameters)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = kv.Key;
                    param.Value = kv.Value ?? DBNull.Value;
                    command.Parameters.Add(param);
                }
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            // Populate the schema from the reader. Field types come from
            // DbDataReader.GetFieldType — provider-native types (e.g. NpgsqlTypes.NpgsqlPoint)
            // bubble through as object when there's no CLR equivalent.
            var fields = new ReportField[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                fields[i] = new ReportField(reader.GetName(i), reader.GetFieldType(i) ?? typeof(object));
            }
            _schema = new ReportRecordSchema(fields);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return SnapshotRow(reader, _schema);
            }
        }
        finally
        {
            if (ownsConnection)
            {
                await connection.CloseAsync().ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Copies the current reader row into a self-contained <see cref="IReportRecord"/>
    /// — the record outlives the reader, so it's safe to materialize / cache.</summary>
    private static IReportRecord SnapshotRow(DbDataReader reader, IReportRecordSchema schema)
    {
        var values = new object?[schema.Fields.Count];
        for (int i = 0; i < values.Length; i++)
        {
            if (reader.IsDBNull(i))
            {
                values[i] = null;
            }
            else
            {
                values[i] = reader.GetValue(i);
            }
        }
        return new SnapshotRecord(schema, values);
    }

    private sealed class SnapshotRecord(IReportRecordSchema schema, object?[] values) : IReportRecord
    {
        public IReportRecordSchema Schema => schema;

        public object? this[string name]
        {
            get
            {
                var ord = schema.IndexOf(name);
                return ord < 0 ? null : values[ord];
            }
        }

        public object? this[int ordinal]
            => ordinal < 0 || ordinal >= values.Length ? null : values[ordinal];

        public IEnumerable<KeyValuePair<string, object?>> ToKeyValuePairs()
        {
            for (int i = 0; i < values.Length; i++)
            {
                yield return new KeyValuePair<string, object?>(schema.Fields[i].Name, values[i]);
            }
        }
    }

    private sealed class EmptySchema : IReportRecordSchema
    {
        public static readonly EmptySchema Instance = new();
        public IReadOnlyList<ReportField> Fields { get; } = Array.Empty<ReportField>();
        public int IndexOf(string name) => -1;
    }
}
