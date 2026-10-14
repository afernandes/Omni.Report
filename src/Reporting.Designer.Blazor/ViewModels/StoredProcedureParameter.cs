namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>One formal parameter of a stored procedure (auto-discovered from the DB catalog).</summary>
public sealed record StoredProcedureParameter(
    string Name,
    Type ClrType,
    bool IsOutput = false,
    bool IsNullable = true,
    string? ProviderType = null);
