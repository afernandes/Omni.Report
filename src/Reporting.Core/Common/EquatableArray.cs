using System.Collections;
using System.Collections.Immutable;

namespace Reporting.Common;

/// <summary>
/// Immutable, value-equality wrapper around <see cref="ImmutableArray{T}"/>. Records using
/// this type as a property gain structural equality for free — required for the
/// round-trip equality guarantee of <c>ReportDefinition</c>.
/// </summary>
/// <typeparam name="T">Element type. Equality uses <see cref="EqualityComparer{T}.Default"/>, so records
/// and value types compare structurally while reference types fall back to their own <c>Equals</c>.</typeparam>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    /// <summary>An empty array. Also what a <c>default</c> instance behaves as.</summary>
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _items;

    /// <summary>Wraps an existing immutable array. A <c>default</c> (uninitialised) array becomes empty,
    /// so the wrapper never throws on a struct that was never assigned.</summary>
    public EquatableArray(ImmutableArray<T> items) => _items = items.IsDefault ? ImmutableArray<T>.Empty : items;

    /// <summary>Copies any sequence into an immutable array. A null sequence becomes empty.</summary>
    public EquatableArray(IEnumerable<T> items) : this(items?.ToImmutableArray() ?? ImmutableArray<T>.Empty) { }

    private ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    /// <summary>Number of elements.</summary>
    public int Count => Items.Length;

    /// <summary>The element at <paramref name="index"/>.</summary>
    public T this[int index] => Items[index];

    /// <summary>Enumerates the elements in order.</summary>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Element-by-element structural equality. This is the whole point of the type: a record
    /// holding an <see cref="ImmutableArray{T}"/> would compare by reference and break round-trip equality.</summary>
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

    /// <summary>Structural equality against any object.</summary>
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    /// <summary>A hash combined from every element, consistent with <see cref="Equals(EquatableArray{T})"/>.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }
        return hash.ToHashCode();
    }

    /// <summary>Structural equality.</summary>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    /// <summary>Structural inequality.</summary>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);


    /// <summary>Wraps an immutable array implicitly, so call sites read naturally.</summary>
    public static implicit operator EquatableArray<T>(ImmutableArray<T> items) => new(items);

    /// <summary>Copies a plain array implicitly — lets a collection expression bind straight to a property.
    /// A null array becomes <see cref="Empty"/>, matching the constructors and the <c>default</c> instance:
    /// every other way into this type treats "nothing" as an empty array rather than throwing.</summary>
    public static implicit operator EquatableArray<T>(T[] items)
        => items is null ? Empty : new(items.ToImmutableArray());
}

/// <summary>Factory helpers for <see cref="EquatableArray{T}"/>, so the element type can be inferred.</summary>
public static class EquatableArray
{
    /// <summary>Builds an array from the given elements.</summary>
    public static EquatableArray<T> Create<T>(params T[] items) => new(items.ToImmutableArray());

    /// <summary>Builds an array by copying a sequence.</summary>
    public static EquatableArray<T> From<T>(IEnumerable<T> items) => new(items);
}
