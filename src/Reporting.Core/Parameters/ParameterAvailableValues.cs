using Reporting.Common;

namespace Reporting.Parameters;

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

    /// <summary>Cascading (SSRS dependent parameters): the dataset field whose value must equal the value of the
    /// <see cref="DependsOn"/> parameter for a row to be offered — e.g. a "Cidade" list restricted to the selected
    /// "Estado". Null/blank = no cascade. Requires <see cref="DependsOn"/>.</summary>
    public string? FilterField { get; init; }

    /// <summary>Cascading: the parent parameter this depends on. With <see cref="FilterField"/>, the available
    /// values are restricted to dataset rows where <c>row[FilterField] == Parameters[DependsOn]</c>.</summary>
    public string? DependsOn { get; init; }

    /// <summary>True when this draws from a dataset query.</summary>
    public bool IsQuery => !string.IsNullOrWhiteSpace(DataSet);

    /// <summary>True when this is a cascading (dependent) domain — filtered by a parent parameter's value.</summary>
    public bool IsCascading => !string.IsNullOrWhiteSpace(FilterField) && !string.IsNullOrWhiteSpace(DependsOn);

    /// <summary>A static domain from the given allowed values.</summary>
    public static ParameterAvailableValues FromList(params ParameterValue[] values)
        => new() { Values = new EquatableArray<ParameterValue>(values) };

    /// <summary>A query-driven domain: distinct rows of <paramref name="dataSet"/> projected to
    /// (<paramref name="valueField"/>, <paramref name="labelField"/> ?? value).</summary>
    public static ParameterAvailableValues FromQuery(string dataSet, string valueField, string? labelField = null)
        => new() { DataSet = dataSet, ValueField = valueField, LabelField = labelField };

    /// <summary>A cascading query-driven domain: like <see cref="FromQuery"/>, but rows are restricted to those
    /// where <paramref name="filterField"/> equals the value of the <paramref name="dependsOn"/> parameter.</summary>
    public static ParameterAvailableValues FromCascadingQuery(string dataSet, string valueField,
        string filterField, string dependsOn, string? labelField = null)
        => new() { DataSet = dataSet, ValueField = valueField, LabelField = labelField, FilterField = filterField, DependsOn = dependsOn };
}
