using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Reporting.Common;

/// <summary>
/// Immutable, value-equality wrapper around <see cref="ImmutableDictionary{TKey,TValue}"/>.
/// Used for metadata bags on records that must support structural equality.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type, compared with <see cref="EqualityComparer{T}.Default"/>.</typeparam>
public readonly struct EquatableDictionary<TKey, TValue>
    : IEquatable<EquatableDictionary<TKey, TValue>>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    /// <summary>An empty dictionary. Also what a <c>default</c> instance behaves as.</summary>
    public static readonly EquatableDictionary<TKey, TValue> Empty = new(ImmutableDictionary<TKey, TValue>.Empty);

    private readonly ImmutableDictionary<TKey, TValue> _items;

    /// <summary>Wraps an existing immutable dictionary; null becomes empty.</summary>
    public EquatableDictionary(ImmutableDictionary<TKey, TValue> items)
        => _items = items ?? ImmutableDictionary<TKey, TValue>.Empty;

    /// <summary>Copies any sequence of pairs; null becomes empty.</summary>
    public EquatableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items)
        : this(items?.ToImmutableDictionary() ?? ImmutableDictionary<TKey, TValue>.Empty) { }

    private ImmutableDictionary<TKey, TValue> Items => _items ?? ImmutableDictionary<TKey, TValue>.Empty;

    /// <summary>Number of entries.</summary>
    public int Count => Items.Count;

    /// <summary>The value stored under <paramref name="key"/>.</summary>
    public TValue this[TKey key] => Items[key];

    /// <summary>All keys, in no guaranteed order.</summary>
    public IEnumerable<TKey> Keys => Items.Keys;

    /// <summary>All values, in no guaranteed order.</summary>
    public IEnumerable<TValue> Values => Items.Values;

    /// <summary>True when the key is present.</summary>
    public bool ContainsKey(TKey key) => Items.ContainsKey(key);

    /// <summary>Looks up a key without throwing when it is absent.</summary>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => Items.TryGetValue(key, out value);

    /// <summary>Enumerates the entries.</summary>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Entry-by-entry structural equality, independent of insertion order.</summary>
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

    /// <summary>Structural equality against any object.</summary>
    public override bool Equals(object? obj) => obj is EquatableDictionary<TKey, TValue> other && Equals(other);

    /// <summary>A hash over the entries sorted by key, so two equal dictionaries built in different
    /// orders hash the same.</summary>
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

    /// <summary>Structural equality.</summary>
    public static bool operator ==(EquatableDictionary<TKey, TValue> left, EquatableDictionary<TKey, TValue> right) => left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(EquatableDictionary<TKey, TValue> left, EquatableDictionary<TKey, TValue> right) => !left.Equals(right);
}
