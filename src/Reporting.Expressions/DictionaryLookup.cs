namespace Reporting.Expressions;

/// <summary>Simple <see cref="IValueLookup"/> backed by a mutable dictionary —
/// suitable for tests and for the default <see cref="ReportExpressionContext"/>.</summary>
public sealed class DictionaryLookup : IValueLookup
{
    private readonly Dictionary<string, object?> _items;

    public DictionaryLookup(IEqualityComparer<string>? comparer = null)
        => _items = new Dictionary<string, object?>(comparer ?? StringComparer.OrdinalIgnoreCase);

    public DictionaryLookup(IEnumerable<KeyValuePair<string, object?>> items, IEqualityComparer<string>? comparer = null)
        : this(comparer)
    {
        foreach (var kv in items)
        {
            _items[kv.Key] = kv.Value;
        }
    }

    public object? this[string key]
    {
        get => _items.TryGetValue(key, out var v) ? v : null;
        set => _items[key] = value;
    }

    public bool Contains(string key) => _items.ContainsKey(key);

    public IEnumerable<string> Keys => _items.Keys;

    public void Set(string key, object? value) => _items[key] = value;

    public void Remove(string key) => _items.Remove(key);
}
