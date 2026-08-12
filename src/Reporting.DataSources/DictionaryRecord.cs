namespace Reporting.DataSources;

/// <summary>
/// Generic <see cref="IReportRecord"/> backed by a name→value <see cref="IDictionary{TKey,TValue}"/>.
/// Used by every text-source provider (JSON, XML, REST, FileSystem) because the natural
/// shape of "a row I just parsed from a key/value document" is a dictionary. The schema
/// drives the ordinal access path; missing keys read as <c>null</c> (matches RDL
/// semantics where an absent column doesn't throw).
/// </summary>
/// <remarks>
/// Lookups are case-insensitive when the dictionary uses an ordinal-ignore-case comparer
/// — passing in a <c>Dictionary&lt;string, object?&gt;(StringComparer.OrdinalIgnoreCase)</c>
/// at construction time makes <c>{Fields.Total}</c> match a JSON property named "total"
/// or "TOTAL". The default <see cref="Dictionary{TKey,TValue}"/> comparer is case-sensitive,
/// so providers that want case-insensitive matching must pick the comparer when filling
/// the dictionary.
/// </remarks>
public sealed class DictionaryRecord : IReportRecord
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    /// <summary>Builds a record over a name/value map.</summary>
    /// <param name="schema">Field list the record reports.</param>
    /// <param name="values">The values. Keys absent from the map read as null.</param>
    public DictionaryRecord(IReportRecordSchema schema, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        Schema = schema;
        _values = values;
    }

    /// <summary>Schema shared with every record of this source.</summary>
    public IReportRecordSchema Schema { get; }

    /// <summary>Value by field name; null when the field is unknown.</summary>
    public object? this[string name] => _values.TryGetValue(name, out var v) ? v : null;

    /// <summary>Value by ordinal; null when out of range.</summary>
    public object? this[int ordinal]
    {
        get
        {
            if (ordinal < 0 || ordinal >= Schema.Fields.Count) return null;
            var name = Schema.Fields[ordinal].Name;
            return _values.TryGetValue(name, out var v) ? v : null;
        }
    }

    /// <summary>The record as name/value pairs, in schema order — how the expression context ingests a row.</summary>
    public IEnumerable<KeyValuePair<string, object?>> ToKeyValuePairs()
    {
        // Iterate by schema order — the expression engine's "Fields" enumeration relies on
        // a stable, schema-driven ordinal, not the dictionary's internal hash order.
        foreach (var f in Schema.Fields)
        {
            _values.TryGetValue(f.Name, out var v);
            yield return new KeyValuePair<string, object?>(f.Name, v);
        }
    }
}
