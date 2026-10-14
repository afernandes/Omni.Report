namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Result of auto-discovering a stored procedure's parameter signature.</summary>
public sealed record StoredProcedureSignatureResult(
    IReadOnlyList<StoredProcedureParameter> Parameters,
    TimeSpan Elapsed,
    string? Error = null);
