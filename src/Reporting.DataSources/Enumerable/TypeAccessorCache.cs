using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Reporting.DataSources.Enumerable;

/// <summary>Internal cache used by non-generic helpers when the element type is only known at runtime.</summary>
internal static class TypeAccessorCache
{
    private static readonly ConcurrentDictionary<Type, object> _cache = new();

    /// <summary>Returns the <c>TypeAccessor&lt;T&gt;.Instance</c> for a type only known at runtime, for
    /// callers that hold an <c>IEnumerable</c> without its element type in hand.</summary>
    public static object For(Type elementType)
        => _cache.GetOrAdd(elementType, t =>
        {
            var accessor = typeof(TypeAccessor<>).MakeGenericType(t);
            var property = accessor.GetProperty(nameof(TypeAccessor<object>.Instance), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Missing static accessor for {accessor.Name}.");
            return property.GetValue(null)!;
        });
}
