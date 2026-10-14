using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Reporting.DataSources.Enumerable;

public sealed partial class TypeAccessor<T>
{

    /// <summary>One property, ready to read.</summary>
    /// <param name="Name">Property name, which becomes the field name.</param>
    /// <param name="Type">Property type.</param>
    /// <param name="Get">Compiled getter — a delegate rather than a reflection call per row.</param>
    public sealed record Accessor(string Name, Type Type, Func<T, object?> Get);
}
