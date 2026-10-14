using Reporting.Common;

namespace Reporting.Parameters;

/// <summary>A computed variable evaluated once per data row (or once per report when global).</summary>
public sealed record ReportVariable(
    string Name,
    string Expression,
    VariableScope Scope = VariableScope.Row,
    object? InitialValue = null);
