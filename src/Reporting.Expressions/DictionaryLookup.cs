namespace Reporting.Expressions;

/// <summary>Simple <see cref="IValueLookup"/> backed by a mutable dictionary —
/// suitable for tests and for the default <see cref="ReportExpressionContext"/>.</summary>
public sealed class DictionaryLookup : IValueLookup
{
    private readonly Dictionary<string, object?> _items;

    /// <summary>Creates an empty lookup.</summary>
    /// <param name="comparer">Key comparer. The default is case-insensitive, matching how report expressions
    /// address fields — <c>Fields.Total</c> and <c>Fields.total</c> are the same field to an author.</param>
    public DictionaryLookup(IEqualityComparer<string>? comparer = null)
        => _items = new Dictionary<string, object?>(comparer ?? StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a lookup seeded with <paramref name="items"/>. Later duplicates win.</summary>
    /// <param name="items">Initial entries.</param>
    /// <param name="comparer">Key comparer; see the other constructor.</param>
    public DictionaryLookup(IEnumerable<KeyValuePair<string, object?>> items, IEqualityComparer<string>? comparer = null)
        : this(comparer)
    {
        foreach (var kv in items)
        {
            _items[kv.Key] = kv.Value;
        }
    }

    /// <summary>Reads or writes a value. Reading an unknown key yields null instead of throwing, which is what
    /// keeps a typo in one expression from aborting the whole render.</summary>
    public object? this[string key]
    {
        get => _items.TryGetValue(key, out var v) ? v : null;
        set => _items[key] = value;
    }

    /// <summary>Whether the key exists — distinguishes "absent" from "present but null".</summary>
    public bool Contains(string key) => _items.ContainsKey(key);

    /// <summary>Every key currently stored.</summary>
    public IEnumerable<string> Keys => _items.Keys;

    /// <summary>Stores a value, replacing any previous one. Same as the indexer setter, in method form.</summary>
    public void Set(string key, object? value) => _items[key] = value;

    /// <summary>Removes a key. Silent when it was not there.</summary>
    public void Remove(string key) => _items.Remove(key);
}
