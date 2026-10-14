using Reporting.Common;

namespace Reporting.Parameters;

/// <summary>One allowed value of a parameter: the bound <see cref="Value"/> (string form, coerced to the
/// parameter's <see cref="ReportParameter.ValueType"/> at bind) and an optional display
/// <see cref="Label"/> (defaults to the value when null).</summary>
public sealed record ParameterValue(string Value, string? Label = null);
