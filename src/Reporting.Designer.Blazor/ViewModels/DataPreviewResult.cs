namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Result of a "Preview" round-trip.</summary>
public sealed record DataPreviewResult(
    IReadOnlyList<DiscoveredField> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    TimeSpan Elapsed,
    string? Error = null);
