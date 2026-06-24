using System.Globalization;
using Reporting.Parameters;

namespace Reporting.DataSources;

/// <summary>
/// Materializes a parameter's <see cref="ParameterAvailableValues"/> into a concrete, ordered,
/// de-duplicated list of (value, label) pairs a host can bind to a validated dropdown. Static entries
/// come first; query-driven entries are read from the named dataset (distinct by value, first-seen order).
/// Returns an empty list when the domain is empty or the referenced dataset is unknown.
/// </summary>
public static class ParameterValueResolver
{
    public static async Task<IReadOnlyList<ParameterValue>> ResolveAsync(
        ParameterAvailableValues available, DataSourceRegistry sources,
        IReadOnlyDictionary<string, object?>? parameterValues = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(sources);

        var result = new List<ParameterValue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var v in available.Values)
        {
            if (seen.Add(v.Value))
            {
                result.Add(v);
            }
        }

        // Cascading: restrict rows to those whose FilterField equals the parent (DependsOn) parameter's value.
        // When the parent value is absent, no rows match — the dependent list is empty until the parent is chosen.
        var cascadeActive = available.IsCascading;
        var cascadeField = available.FilterField;
        var cascadeValue = cascadeActive && parameterValues is not null && parameterValues.TryGetValue(available.DependsOn!, out var pv)
            ? Convert.ToString(pv, CultureInfo.InvariantCulture)
            : null;

        if (available.IsQuery
            && !string.IsNullOrWhiteSpace(available.ValueField)
            && sources.TryGet(available.DataSet!, out var ds))
        {
            await foreach (var record in ds.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (cascadeActive && !string.Equals(
                        Convert.ToString(record[cascadeField!], CultureInfo.InvariantCulture), cascadeValue, StringComparison.Ordinal))
                {
                    continue; // row's parent-key doesn't match the selected parent value
                }
                var raw = record[available.ValueField!];
                if (raw is null)
                {
                    continue;
                }
                var value = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!seen.Add(value))
                {
                    continue;
                }
                string? label = null;
                if (!string.IsNullOrWhiteSpace(available.LabelField))
                {
                    var rawLabel = Convert.ToString(record[available.LabelField!], CultureInfo.InvariantCulture);
                    // Null/empty label cell → leave the label null so consumers fall back to the value
                    // (honoring the LabelField "falls back to the value" contract).
                    label = string.IsNullOrEmpty(rawLabel) ? null : rawLabel;
                }
                result.Add(new ParameterValue(value, label));
            }
        }

        return result;
    }
}
