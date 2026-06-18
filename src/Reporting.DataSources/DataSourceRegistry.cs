using System.Collections.Concurrent;

namespace Reporting.DataSources;

/// <summary>
/// Name-keyed registry resolved at runtime by the layout engine to bind a
/// <c>DataSourceDefinition</c> to a concrete <see cref="IReportDataSource"/>.
/// </summary>
public sealed class DataSourceRegistry
{
    private readonly ConcurrentDictionary<string, IReportDataSource> _sources = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IReportDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources[source.Name] = source;
    }

    public bool TryGet(string name, out IReportDataSource source)
        => _sources.TryGetValue(name, out source!);

    public IReportDataSource Get(string name)
        => _sources.TryGetValue(name, out var s)
            ? s
            : throw new InvalidOperationException($"No data source named '{name}' is registered.");

    public bool Remove(string name) => _sources.TryRemove(name, out _);

    public IEnumerable<string> Names => _sources.Keys;
}
