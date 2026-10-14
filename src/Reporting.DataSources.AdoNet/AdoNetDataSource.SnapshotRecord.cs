using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Reporting.DataSources.AdoNet;

public sealed partial class AdoNetDataSource
{

    private sealed partial class SnapshotRecord(IReportRecordSchema schema, object?[] values) : IReportRecord
    {
        public IReportRecordSchema Schema => schema;

        public object? this[string name]
        {
            get
            {
                var ord = schema.IndexOf(name);
                return ord < 0 ? null : values[ord];
            }
        }

        public object? this[int ordinal]
            => ordinal < 0 || ordinal >= values.Length ? null : values[ordinal];

        public IEnumerable<KeyValuePair<string, object?>> ToKeyValuePairs()
        {
            for (int i = 0; i < values.Length; i++)
            {
                yield return new KeyValuePair<string, object?>(schema.Fields[i].Name, values[i]);
            }
        }
    }
}
