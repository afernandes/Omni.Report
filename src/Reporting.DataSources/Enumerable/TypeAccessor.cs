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

    public IReadOnlyList<Accessor> Accessors => _byOrdinal;

    public Accessor? Get(string name) => _byName.GetValueOrDefault(name);

    private static Accessor BuildAccessor(PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(T), "x");
        var body = Expression.Convert(Expression.Property(instance, property), typeof(object));
        var lambda = Expression.Lambda<Func<T, object?>>(body, instance).Compile();
        return new Accessor(property.Name, property.PropertyType, lambda);
    }

    public sealed record Accessor(string Name, Type Type, Func<T, object?> Get);
}

/// <summary>Internal cache used by non-generic helpers when the element type is only known at runtime.</summary>
internal static class TypeAccessorCache
{
    private static readonly ConcurrentDictionary<Type, object> _cache = new();

    public static object For(Type elementType)
        => _cache.GetOrAdd(elementType, t =>
        {
            var accessor = typeof(TypeAccessor<>).MakeGenericType(t);
            var property = accessor.GetProperty(nameof(TypeAccessor<object>.Instance), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Missing static accessor for {accessor.Name}.");
            return property.GetValue(null)!;
        });
}
