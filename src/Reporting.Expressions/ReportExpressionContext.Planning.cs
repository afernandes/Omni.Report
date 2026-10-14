namespace Reporting.Expressions;

public sealed partial class ReportExpressionContext
{
    // Snapshots contain borrowed scalar values but independent mutable collections. The parent is paused
    // during planning; dataset buffers are read-only after registration and can be shared safely.
    internal ReportExpressionContext ForkForPlanning(bool includeGroups = true)
    {
        var copy = new ReportExpressionContext(_evaluator, Culture)
        {
            CancellationToken = CancellationToken,
            GroupKey = GroupKey,
            PageNumber = PageNumber,
            TotalPages = TotalPages,
            Now = Now,
            UserName = UserName,
            ReportName = ReportName,
            _reportScopeOverride = _reportScopeOverride
        };
        CopyLookup(_fieldsLookup, copy._fieldsLookup);
        CopyLookup(ParametersStore, copy.ParametersStore);
        CopyLookup(VariablesStore, copy.VariablesStore);
        copy._reportRows.AddRange(_reportRows);
        copy._pageRows.AddRange(_pageRows);
        copy._groupRows.AddRange(_groupRows);
        foreach (var pair in _sources)
        {
            var lookup = new DictionaryLookup();
            CopyLookup(pair.Value, lookup);
            copy._sources.Add(pair.Key, lookup);
        }
        foreach (var pair in _datasets) copy._datasets.Add(pair.Key, pair.Value);
        foreach (var pair in _reportItems) copy._reportItems.Add(pair.Key, pair.Value);
        if (includeGroups)
        {
            foreach (var group in _groups) copy.BeginGroup(group.Name, group.Key);
            for (int depth = 0; depth < _groups.Count; depth++) copy.CopyGroupState(this, depth);
        }
        else
        {
            // The fragment will commit its first row again; retain only history preceding that row.
            if (copy._reportRows.Count > 0) copy._reportRows.RemoveAt(copy._reportRows.Count - 1);
            if (copy._pageRows.Count > 0) copy._pageRows.RemoveAt(copy._pageRows.Count - 1);
        }
        return copy;
    }

    internal void CopyGroupState(ReportExpressionContext source, int depth)
    {
        var target = _groups[depth];
        _fieldAggregates.Remove(target.Rows);
        target.Rows.Clear();
        target.Rows.AddRange(source._groups[depth].Rows);
        CopyLookup(source._groups[depth].Variables, target.Variables);
    }

    private static void CopyLookup(DictionaryLookup source, DictionaryLookup target)
    {
        foreach (var key in source.Keys) target.Set(key, source[key]);
    }
}
