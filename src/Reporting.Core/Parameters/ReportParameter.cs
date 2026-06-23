using Reporting.Common;

namespace Reporting.Parameters;

/// <summary>Strongly typed report parameter — its <see cref="ValueType"/> is the CLR type
/// the runtime coerces the prompted input to before binding.</summary>
public sealed record ReportParameter(
    string Name,
    Type ValueType,
    string? Prompt = null,
    object? DefaultValue = null,
    bool AllowMultiple = false,
    bool Required = true,
    ParameterAvailableValues? AvailableValues = null,
    bool Nullable = false,
    bool AllowBlank = false,
    bool Hidden = false)
{
    /// <summary>An expression default (SSRS <c>=Today()</c>, <c>=DateAdd(...)</c>, <c>=Parameters!Other.Value</c>)
    /// in OmniReport syntax, evaluated at run start to seed the value when no literal <see cref="DefaultValue"/>
    /// and no prompted value are supplied. Mutually exclusive with <see cref="DefaultValue"/>.</summary>
    public string? DefaultValueExpression { get; init; }
}

/// <summary>Domain of allowed values for a parameter — a static list and/or a query over a dataset, so a
/// host can render a validated dropdown instead of a free-text box (SSRS "Available Values"). Query-driven
/// values are materialized at run time (see <c>ParameterValueResolver</c>); a static list needs no data
/// access. When both are present the static entries come first.</summary>
public sealed record ParameterAvailableValues
{
    /// <summary>Static allowed values, in display order.</summary>
    public EquatableArray<ParameterValue> Values { get; init; } = EquatableArray<ParameterValue>.Empty;

    /// <summary>Dataset that supplies the values (query-driven). Null/blank = static list only.</summary>
    public string? DataSet { get; init; }

    /// <summary>Field providing the bound value, when <see cref="DataSet"/> is set.</summary>
    public string? ValueField { get; init; }

    /// <summary>Field providing the display label (falls back to the value), when <see cref="DataSet"/> is set.</summary>
    public string? LabelField { get; init; }

    /// <summary>True when this draws from a dataset query.</summary>
    public bool IsQuery => !string.IsNullOrWhiteSpace(DataSet);

    /// <summary>A static domain from the given allowed values.</summary>
    public static ParameterAvailableValues FromList(params ParameterValue[] values)
        => new() { Values = new EquatableArray<ParameterValue>(values) };

    /// <summary>A query-driven domain: distinct rows of <paramref name="dataSet"/> projected to
    /// (<paramref name="valueField"/>, <paramref name="labelField"/> ?? value).</summary>
    public static ParameterAvailableValues FromQuery(string dataSet, string valueField, string? labelField = null)
        => new() { DataSet = dataSet, ValueField = valueField, LabelField = labelField };
}

/// <summary>One allowed value of a parameter: the bound <see cref="Value"/> (string form, coerced to the
/// parameter's <see cref="ReportParameter.ValueType"/> at bind) and an optional display
/// <see cref="Label"/> (defaults to the value when null).</summary>
public sealed record ParameterValue(string Value, string? Label = null);

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
