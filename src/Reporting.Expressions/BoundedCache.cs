namespace Reporting.Expressions;

internal sealed class BoundedCache<TKey, TValue>(int capacity, IEqualityComparer<TKey>? comparer = null) where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items = new(comparer);
    private readonly Queue<TKey> _order = new();
    private readonly object _gate = new();

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(key, out var value)) return value;
            value = factory(key);
            while (_items.Count >= capacity && _order.TryDequeue(out var oldest)) _items.Remove(oldest);
            _items.Add(key, value);
            _order.Enqueue(key);
            return value;
        }
    }

    public bool TryRemove(TKey key, out TValue? value)
    {
        lock (_gate)
        {
            bool removed = _items.Remove(key, out value);
            // Invalidations must not leave stale queue entries that accumulate or evict a replacement.
            if (removed)
            {
                _order.Clear();
                foreach (var existing in _items.Keys) _order.Enqueue(existing);
            }
            return removed;
        }
    }

    public void Clear()
    {
        lock (_gate) { _items.Clear(); _order.Clear(); }
    }
}
