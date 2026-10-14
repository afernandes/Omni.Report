using System.Runtime.CompilerServices;

namespace Reporting.DataSources.Enumerable;

public sealed partial class EnumerableDataSource<T>
{

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
