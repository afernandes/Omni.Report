using System.Runtime.CompilerServices;

namespace Reporting.DataSources.Enumerable;

/// <summary>
/// In-memory data source backed by an <see cref="IEnumerable{T}"/>. Field access uses
/// compiled accessors (<see cref="TypeAccessor{T}"/>) so per-row read cost is a single
/// delegate invocation per property — fast enough for hundreds of thousands of rows.
/// </summary>
public sealed class EnumerableDataSource<T> : IReportDataSource
{
    private readonly IEnumerable<T> _items;
    private readonly TypeAccessor<T> _accessor;

    public EnumerableDataSource(string name, IEnumerable<T> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);
        Name = name;
        _items = items;
        _accessor = TypeAccessor<T>.Instance;
        Schema = new ReportRecordSchema(_accessor.Accessors.Select(a => new ReportField(a.Name, a.Type)));
    }

    public string Name { get; }
    public IReportRecordSchema Schema { get; }

    public async IAsyncEnumerable<IReportRecord> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in _items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new EnumerableRecord(item, _accessor, Schema);
            await Task.Yield();
        }
    }

    private sealed class EnumerableRecord(T item, TypeAccessor<T> accessor, IReportRecordSchema schema) : IReportRecord
    {
        public IReportRecordSchema Schema => schema;

        public object? this[string name]
        {
            get
            {
                var a = accessor.Get(name);
                return a is null ? null : a.Get(item);
            }
        }

        public object? this[int ordinal]
        {
            get
            {
                if (ordinal < 0 || ordinal >= accessor.Accessors.Count)
                {
                    return null;
                }
                return accessor.Accessors[ordinal].Get(item);
            }
        }

        public IEnumerable<KeyValuePair<string, object?>> ToKeyValuePairs()
        {
            foreach (var a in accessor.Accessors)
            {
                yield return new KeyValuePair<string, object?>(a.Name, a.Get(item));
            }
        }
    }
}
