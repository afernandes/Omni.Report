using System.Globalization;
using Reporting.Aggregates;
using Reporting.Expressions;

namespace Reporting.Layout.Internal;

internal sealed partial class RowScopedContext
{

    /// <summary>Case-insensitive read-only view over one row's key/value pairs.</summary>
    private sealed class RowLookup : IValueLookup
    {
        private readonly Dictionary<string, object?> _map;

        public RowLookup(IReadOnlyList<KeyValuePair<string, object?>> row)
        {
            _map = new Dictionary<string, object?>(row.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in row)
            {
                _map[kv.Key] = kv.Value;
            }
        }

        public object? this[string key] => _map.TryGetValue(key, out var v) ? v : null;
        public bool Contains(string key) => _map.ContainsKey(key);
        public IEnumerable<string> Keys => _map.Keys;
    }
}
