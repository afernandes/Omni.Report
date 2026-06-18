using System.Collections;
using System.Collections.Immutable;

namespace Reporting.Common;

/// <summary>
/// Immutable, value-equality wrapper around <see cref="ImmutableArray{T}"/>. Records using
/// this type as a property gain structural equality for free — required for the
/// round-trip equality guarantee of <c>ReportDefinition</c>.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items.IsDefault ? ImmutableArray<T>.Empty : items;

    public EquatableArray(IEnumerable<T> items) : this(items?.ToImmutableArray() ?? ImmutableArray<T>.Empty) { }

    private ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public int Count => Items.Length;
    public T this[int index] => Items[index];

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(EquatableArray<T> other)
    {
        var left = Items;
        var right = other.Items;
        if (left.Length != right.Length)
        {
            return false;
        }
        var cmp = EqualityComparer<T>.Default;
        for (int i = 0; i < left.Length; i++)
        {
            if (!cmp.Equals(left[i], right[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public static implicit operator EquatableArray<T>(ImmutableArray<T> items) => new(items);
    public static implicit operator EquatableArray<T>(T[] items) => new(items.ToImmutableArray());
}

public static class EquatableArray
{
    public static EquatableArray<T> Create<T>(params T[] items) => new(items.ToImmutableArray());
    public static EquatableArray<T> From<T>(IEnumerable<T> items) => new(items);
}
