namespace Reporting.Layout;

/// <summary>An explicit limitation encountered while rendering a definition.</summary>
public sealed record PaginationDiagnostic(string Code, string Feature, string Message);
