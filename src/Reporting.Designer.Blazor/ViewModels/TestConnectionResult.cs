namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Outcome of <see cref="IDesignerDataConnect.TestConnectionAsync"/>.</summary>
public sealed record TestConnectionResult(bool Success, string? Message, TimeSpan Elapsed);
