using System.Collections.Concurrent;
using System.Reflection;

namespace Reporting.Expressions;

/// <summary>Walks a dotted member-path (<c>Cliente.Endereco.Cidade</c>) on a starting object,
/// resolving public properties or fields via reflection. Each <c>(Type, name)</c> pair is cached
/// as a compiled accessor for repeated row evaluations.</summary>
internal static class MemberPathResolver
{
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>?> _cache = new();

    public static object? Resolve(object? root, ReadOnlySpan<char> path)
    {
        if (root is null || path.IsEmpty)
        {
            return root;
        }

        int dot = path.IndexOf('.');
        var head = dot < 0 ? path : path[..dot];
        var headName = head.ToString();
        var accessor = _cache.GetOrAdd((root.GetType(), headName), static key => Compile(key.Item1, key.Item2));
        if (accessor is null)
        {
            return null;
        }
        var next = accessor(root);
        if (dot < 0)
        {
            return next;
        }
        return Resolve(next, path[(dot + 1)..]);
    }

    private static Func<object, object?>? Compile(Type type, string name)
    {
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null && prop.CanRead && prop.GetIndexParameters().Length == 0)
        {
            return obj => prop.GetValue(obj);
        }
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null)
        {
            return obj => field.GetValue(obj);
        }
        return null;
    }
}
