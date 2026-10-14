using System.Collections;
using System.Collections.Immutable;

namespace Reporting.Common;

/// <summary>Factory helpers for <see cref="EquatableArray{T}"/>, so the element type can be inferred.</summary>
public static class EquatableArray
{
    /// <summary>Builds an array from the given elements.</summary>
    public static EquatableArray<T> Create<T>(params T[] items) => new(items.ToImmutableArray());

    /// <summary>Builds an array by copying a sequence.</summary>
    public static EquatableArray<T> From<T>(IEnumerable<T> items) => new(items);
}
