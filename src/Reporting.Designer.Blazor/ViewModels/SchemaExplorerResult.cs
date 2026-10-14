namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Result of listing all tables / views in a connection.</summary>
public sealed record SchemaExplorerResult(
    IReadOnlyList<DatabaseTable> Tables,
    TimeSpan Elapsed,
    string? Error = null);
