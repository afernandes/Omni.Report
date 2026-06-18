using System.Collections;
using System.Collections.Immutable;

namespace Reporting.Common;

/// <summary>
/// Immutable, value-equality wrapper around <see cref="ImmutableDictionary{TKey,TValue}"/>.
/// Used for metadata bags on records that must support structural equality.
/// </summary>
public readonly struct EquatableDictionary<TKey, TValue>
    : IEquatable<EquatableDictionary<TKey, TValue>>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public static readonly EquatableDictionary<TKey, TValue> Empty = new(ImmutableDictionary<TKey, TValue>.Empty);

    private readonly ImmutableDictionary<TKey, TValue> _items;

    public EquatableDictionary(ImmutableDictionary<TKey, TValue> items)
        => _items = items ?? ImmutableDictionary<TKey, TValue>.Empty;

    public EquatableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items)
        : this(items?.ToImmutableDictionary() ?? ImmutableDictionary<TKey, TValue>.Empty) { }

    private ImmutableDictionary<TKey, TValue> Items => _items ?? ImmutableDictionary<TKey, TValue>.Empty;

    public int Count => Items.Count;
    public TValue this[TKey key] => Items[key];
    public IEnumerable<TKey> Keys => Items.Keys;
    public IEnumerable<TValue> Values => Items.Values;
    public bool ContainsKey(TKey key) => Items.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => Items.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(EquatableDictionary<TKey, TValue> other)
    {
        var left = Items;
        var right = other.Items;
        if (left.Count != right.Count)
        {
            return false;
        }
        var cmp = EqualityComparer<TValue>.Default;
        foreach (var kv in left)
        {
            if (!right.TryGetValue(kv.Key, out var v) || !cmp.Equals(kv.Value, v))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableDictionary<TKey, TValue> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kv in Items.OrderBy(static k => k.Key))
        {
            hash.Add(kv.Key);
            hash.Add(kv.Value);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(EquatableDictionary<TKey, TValue> left, EquatableDictionary<TKey, TValue> right) => left.Equals(right);
    public static bool operator !=(EquatableDictionary<TKey, TValue> left, EquatableDictionary<TKey, TValue> right) => !left.Equals(right);
}
