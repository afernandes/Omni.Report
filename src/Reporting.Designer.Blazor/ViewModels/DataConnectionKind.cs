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
