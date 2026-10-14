using System.Data;
using System.Runtime.CompilerServices;

namespace Reporting.DataSources;

public sealed partial class DataTableDataSource
{

    private sealed class DataTableRecord(DataRow row, IReportRecordSchema schema) : IReportRecord
    {
        public IReportRecordSchema Schema => schema;

        public object? this[string name]
        {
            get
            {
                if (!row.Table.Columns.Contains(name))
                {
                    return null;
                }
                var value = row[name];
                return value == DBNull.Value ? null : value;
            }
        }

        public object? this[int ordinal]
        {
            get
            {
                if (ordinal < 0 || ordinal >= row.Table.Columns.Count)
                {
                    return null;
                }
                var value = row[ordinal];
                return value == DBNull.Value ? null : value;
            }
        }

        public IEnumerable<KeyValuePair<string, object?>> ToKeyValuePairs()
        {
            for (int i = 0; i < row.Table.Columns.Count; i++)
            {
                var value = row[i];
                yield return new KeyValuePair<string, object?>(
                    row.Table.Columns[i].ColumnName,
                    value == DBNull.Value ? null : value);
            }
        }
    }
}
