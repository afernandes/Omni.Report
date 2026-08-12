using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Reporting.DataSources.Enumerable;

/// <summary>
/// Cached, compiled accessor for the public readable properties of <typeparamref name="T"/>.
/// Property reads compile down to a single delegate invocation — orders of magnitude
/// faster than <see cref="PropertyInfo.GetValue(object?)"/>.
/// </summary>
public sealed class TypeAccessor<T>
{
    private static readonly Lazy<TypeAccessor<T>> _instance = new(() => new TypeAccessor<T>());

    /// <summary>The shared accessor for <typeparamref name="T"/>. Built once: reflecting over the type per
    /// row would dominate the cost of reading a large source.</summary>
    public static TypeAccessor<T> Instance => _instance.Value;

    private readonly Dictionary<string, Accessor> _byName;
    private readonly Accessor[] _byOrdinal;

    private TypeAccessor()
    {
        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        _byOrdinal = properties.Select(BuildAccessor).ToArray();
        _byName = new Dictionary<string, Accessor>(StringComparer.OrdinalIgnoreCase);
        foreach (var accessor in _byOrdinal)
        {
            _byName[accessor.Name] = accessor;
        }
    }

    /// <summary>Property accessors in declaration order, which fixes the field ordinals.</summary>
    public IReadOnlyList<Accessor> Accessors => _byOrdinal;

    /// <summary>Accessor for a property by name, or null when the type has no such property.</summary>
    public Accessor? Get(string name) => _byName.GetValueOrDefault(name);

    private static Accessor BuildAccessor(PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(T), "x");
        var body = Expression.Convert(Expression.Property(instance, property), typeof(object));
        var lambda = Expression.Lambda<Func<T, object?>>(body, instance).Compile();
        return new Accessor(property.Name, property.PropertyType, lambda);
    }

    /// <summary>One property, ready to read.</summary>
    /// <param name="Name">Property name, which becomes the field name.</param>
    /// <param name="Type">Property type.</param>
    /// <param name="Get">Compiled getter — a delegate rather than a reflection call per row.</param>
    public sealed record Accessor(string Name, Type Type, Func<T, object?> Get);
}

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
