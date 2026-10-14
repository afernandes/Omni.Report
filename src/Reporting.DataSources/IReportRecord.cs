namespace Reporting.DataSources;

/// <summary>
/// A single tabular record exposed to the layout engine. Field access is by name; the
/// underlying field set is described by <see cref="Schema"/>.
/// </summary>
public interface IReportRecord
{
    /// <summary>Schema (column names + CLR types) shared by every record in this source.</summary>
    IReportRecordSchema Schema { get; }

    /// <summary>Reads the field by name. Returns <c>null</c> if the field is unknown or empty.</summary>
    object? this[string name] { get; }

    /// <summary>Reads the field by ordinal.</summary>
    object? this[int ordinal] { get; }

    /// <summary>Materializes a key/value snapshot of the record (used to push into the expression context).</summary>
    IEnumerable<KeyValuePair<string, object?>> ToKeyValuePairs();
}
