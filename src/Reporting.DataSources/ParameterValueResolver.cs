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
        ParameterAvailableValues available, DataSourceRegistry sources, CancellationToken cancellationToken = default)
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

        if (available.IsQuery
            && !string.IsNullOrWhiteSpace(available.ValueField)
            && sources.TryGet(available.DataSet!, out var ds))
        {
            await foreach (var record in ds.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
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
                string? label = string.IsNullOrWhiteSpace(available.LabelField)
                    ? null
                    : Convert.ToString(record[available.LabelField!], CultureInfo.InvariantCulture);
                result.Add(new ParameterValue(value, label));
            }
        }

        return result;
    }
}
