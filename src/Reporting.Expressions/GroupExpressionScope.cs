namespace Reporting.Expressions;

internal sealed class GroupExpressionScope(string name, object? key)
{
    internal string Name { get; } = name;
    internal object? Key { get; } = key;
    internal DictionaryLookup Variables { get; } = new();
    internal List<DictionaryLookup> Rows { get; } = [];
}
