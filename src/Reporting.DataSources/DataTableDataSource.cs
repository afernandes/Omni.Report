using System.Data;
using System.Runtime.CompilerServices;

namespace Reporting.DataSources;

/// <summary>Data source over an ADO.NET <see cref="DataTable"/>. Column names map to field names.</summary>
public sealed class DataTableDataSource : IReportDataSource
{
    private readonly DataTable _table;

    /// <summary>Wraps an ADO.NET <see cref="DataTable"/> as a report data source.</summary>
    /// <param name="name">Name the report binds to.</param>
    /// <param name="table">The table. Its columns become the schema; <c>DBNull</c> surfaces as null.</param>
    public DataTableDataSource(string name, DataTable table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(table);
        Name = name;
        _table = table;
        var fields = new List<ReportField>(table.Columns.Count);
        foreach (DataColumn column in table.Columns)
        {
            fields.Add(new ReportField(column.ColumnName, column.DataType));
        }
        Schema = new ReportRecordSchema(fields);
    }

    /// <summary>Name the report binds to.</summary>
    public string Name { get; }

    /// <summary>Fields derived from the table columns.</summary>
    public IReportRecordSchema Schema { get; }

    /// <summary>Yields one record per row, in table order.</summary>
    public async IAsyncEnumerable<IReportRecord> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (DataRow row in _table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new DataTableRecord(row, Schema);
            await Task.Yield();
        }
    }

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
