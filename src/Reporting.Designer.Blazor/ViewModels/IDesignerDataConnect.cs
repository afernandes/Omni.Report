namespace Reporting.Designer.Blazor.ViewModels;

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
