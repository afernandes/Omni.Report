namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>The supported provider catalog. Each value maps to a thin wrapper project
/// under <c>Reporting.DataSources.*</c>. The designer ships only this contract; the
/// concrete <see cref="IDesignerDataConnect"/> implementation that knows about Npgsql /
/// SqlClient / etc. lives in <c>Reporting.Designer.Blazor.DataConnect</c> (opt-in).</summary>
public enum DataConnectionKind
{
    /// <summary>No external database. Fields are typed by hand or imported from a JSON sample.
    /// Sample data drives the preview only — runtime data is supplied by the host.</summary>
    InMemory,
    Sqlite,
    PostgreSql,
    SqlServer,
    MySql,
}

/// <summary>Outcome of <see cref="IDesignerDataConnect.TestConnectionAsync"/>.</summary>
public sealed record TestConnectionResult(bool Success, string? Message, TimeSpan Elapsed);

/// <summary>A single column produced by schema discovery.</summary>
public sealed record DiscoveredField(string Name, Type ClrType, string? ProviderType = null);

/// <summary>Result of a "Get Fields" round-trip.</summary>
public sealed record SchemaDiscoveryResult(
    IReadOnlyList<DiscoveredField> Fields,
    TimeSpan Elapsed,
    string? Error = null);

/// <summary>Result of a "Preview" round-trip.</summary>
public sealed record DataPreviewResult(
    IReadOnlyList<DiscoveredField> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    TimeSpan Elapsed,
    string? Error = null);

/// <summary>One column inside a database table or view (schema explorer leaf).</summary>
public sealed record DatabaseColumn(string Name, string DataType, bool IsNullable, bool IsPrimaryKey = false);

/// <summary>One table or view discovered by the schema explorer.</summary>
public sealed record DatabaseTable(
    string Schema,
    string Name,
    bool IsView,
    IReadOnlyList<DatabaseColumn> Columns)
{
    public string QualifiedName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}

/// <summary>Result of listing all tables / views in a connection.</summary>
public sealed record SchemaExplorerResult(
    IReadOnlyList<DatabaseTable> Tables,
    TimeSpan Elapsed,
    string? Error = null);

/// <summary>One formal parameter of a stored procedure (auto-discovered from the DB catalog).</summary>
public sealed record StoredProcedureParameter(
    string Name,
    Type ClrType,
    bool IsOutput = false,
    bool IsNullable = true,
    string? ProviderType = null);

/// <summary>Result of auto-discovering a stored procedure's parameter signature.</summary>
public sealed record StoredProcedureSignatureResult(
    IReadOnlyList<StoredProcedureParameter> Parameters,
    TimeSpan Elapsed,
    string? Error = null);

/// <summary>Server-side service the designer dialog calls to interrogate a user-supplied
/// database connection at design time. The host registers a concrete implementation in DI;
/// when none is registered the dialog falls back to JSON-sample mode.</summary>
public interface IDesignerDataConnect
{
    Task<TestConnectionResult> TestConnectionAsync(
        DataConnectionKind kind,
        string connectionString,
        CancellationToken cancellationToken = default);

    Task<SchemaDiscoveryResult> DiscoverSchemaAsync(
        DataConnectionKind kind,
        string connectionString,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<DataPreviewResult> PreviewAsync(
        DataConnectionKind kind,
        string connectionString,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int maxRows = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Lists every table and view visible to the connection's principal,
    /// with their column definitions. Drives the schema-explorer tree in the designer.</summary>
    Task<SchemaExplorerResult> ListSchemaAsync(
        DataConnectionKind kind,
        string connectionString,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the formal parameter list of a stored procedure from the DB catalog.
    /// Used by the dialog to pre-populate the SQL parameter grid when "Stored procedure"
    /// is toggled on and a name is typed.</summary>
    Task<StoredProcedureSignatureResult> DiscoverProcedureSignatureAsync(
        DataConnectionKind kind,
        string connectionString,
        string procedureName,
        CancellationToken cancellationToken = default);
}
