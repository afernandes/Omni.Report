namespace Reporting.Parameters;

/// <summary>Strongly typed report parameter — its <see cref="ValueType"/> is the CLR type
/// the runtime coerces the prompted input to before binding.</summary>
public sealed record ReportParameter(
    string Name,
    Type ValueType,
    string? Prompt = null,
    object? DefaultValue = null,
    bool AllowMultiple = false,
    bool Required = true);

/// <summary>A computed variable evaluated once per data row (or once per report when global).</summary>
public sealed record ReportVariable(
    string Name,
    string Expression,
    VariableScope Scope = VariableScope.Row,
    object? InitialValue = null);

public enum VariableScope
{
    /// <summary>Re-evaluated for every row of the detail band.</summary>
    Row,

    /// <summary>Evaluated once at report start.</summary>
    Report,

    /// <summary>Evaluated once per group instance.</summary>
    Group,
}
