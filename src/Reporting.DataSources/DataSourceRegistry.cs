using System.Collections.Concurrent;

namespace Reporting.DataSources;

/// <summary>
/// Name-keyed registry resolved at runtime by the layout engine to bind a
/// <c>DataSourceDefinition</c> to a concrete <see cref="IReportDataSource"/>.
/// </summary>
public sealed class DataSourceRegistry
{
    private readonly ConcurrentDictionary<string, IReportDataSource> _sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a source under its own name, replacing any previous one with that name.</summary>
    public void Register(IReportDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources[source.Name] = source;
    }

    /// <summary>Looks up a source without throwing when it is absent.</summary>
    public bool TryGet(string name, out IReportDataSource source)
        => _sources.TryGetValue(name, out source!);

    /// <summary>Looks up a source, throwing when it is absent. Use for the report's own declared sources,
    /// where a missing one is a definition error rather than a runtime condition.</summary>
    public IReportDataSource Get(string name)
        => _sources.TryGetValue(name, out var s)
            ? s
            : throw new InvalidOperationException($"No data source named '{name}' is registered.");

    /// <summary>Removes a source. False when it was not registered.</summary>
    public bool Remove(string name) => _sources.TryRemove(name, out _);

    /// <summary>Names of every registered source.</summary>
    public IEnumerable<string> Names => _sources.Keys;
}
