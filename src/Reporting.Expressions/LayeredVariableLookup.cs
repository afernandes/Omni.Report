namespace Reporting.Expressions;

internal sealed class LayeredVariableLookup(Func<IEnumerable<DictionaryLookup>> stores) : IValueLookup
{
    public object? this[string key] => stores().FirstOrDefault(store => store.Contains(key))?[key];
    public bool Contains(string key) => stores().Any(store => store.Contains(key));
    public IEnumerable<string> Keys => stores().SelectMany(store => store.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
}
