using System.Collections;
using System.Reflection;

namespace Reporting.Layout.Internal;

// One traversal per preparation, including nested containers, tablix bodies, runs and property bindings.
internal static class DefinitionInspection
{
    internal static IEnumerable<object> Walk(object root, CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.TryPop(out var value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is byte[] or Reporting.Common.EquatableArray<byte> or ReadOnlyMemory<byte> or Memory<byte>) continue;
            if (value is string) { yield return value; continue; }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal or DateTime or DateTimeOffset or Type) continue;
            if (!type.IsValueType && !seen.Add(value)) continue;
            yield return value;
            if (value is IEnumerable collection)
            {
                foreach (var item in collection) if (item is not null) pending.Push(item);
                continue;
            }
            if (type.Assembly != typeof(ReportDefinition).Assembly &&
                !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length == 0 && property.GetValue(value) is { } child) pending.Push(child);
            }
        }
    }
}
