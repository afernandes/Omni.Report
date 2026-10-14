namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>A single column produced by schema discovery.</summary>
public sealed record DiscoveredField(string Name, Type ClrType, string? ProviderType = null);
