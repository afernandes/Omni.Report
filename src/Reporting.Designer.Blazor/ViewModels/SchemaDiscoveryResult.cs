namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Result of a "Get Fields" round-trip.</summary>
public sealed record SchemaDiscoveryResult(
    IReadOnlyList<DiscoveredField> Fields,
    TimeSpan Elapsed,
    string? Error = null);
