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

    public DictionaryRecord(IReportRecordSchema schema, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        Schema = schema;
        _values = values;
    }

    public IReportRecordSchema Schema { get; }

    public object? this[string name] => _values.TryGetValue(name, out var v) ? v : null;

    public object? this[int ordinal]
    {
        get
        {
            if (ordinal < 0 || ordinal >= Schema.Fields.Count) return null;
            var name = Schema.Fields[ordinal].Name;
            return _values.TryGetValue(name, out var v) ? v : null;
        }
    }

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
