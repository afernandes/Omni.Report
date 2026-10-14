using Reporting.Common;

namespace Reporting.Parameters;

/// <summary>How often a <see cref="ReportVariable"/> is re-evaluated — per detail row, once per report, or once per group instance.</summary>
public enum VariableScope
{
    /// <summary>Re-evaluated for every row of the detail band.</summary>
    Row,

    /// <summary>Evaluated once at report start.</summary>
    Report,

    /// <summary>Evaluated once per group instance.</summary>
    Group,
}
