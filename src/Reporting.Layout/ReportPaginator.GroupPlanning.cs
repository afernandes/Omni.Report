using Reporting.Data;
using Reporting.Expressions;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    private IReadOnlyList<IterationRow> ApplyGroupFiltersAndSorts(IReadOnlyList<IterationRow> rows,
        ReportDefinition definition, ReportExpressionContext context, int depth = 0)
    {
        if (depth >= definition.Groups.Count || rows.Count == 0) return rows;
        var group = definition.Groups[depth];
        // Preserve the existing contiguous-group contract, including equal keys in separate instances.
        var instances = new List<(object? Key, List<IterationRow> Rows, object?[] SortKeys, int Index)>();
        foreach (var row in rows)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            PublishPlanningRow(context, row);
            var key = _evaluator.Evaluate(group.GroupExpression, context);
            if (instances.Count == 0 || !Equals(instances[^1].Key, key))
                instances.Add((key, [], [], instances.Count));
            instances[^1].Rows.Add(row);
        }
        var retained = new List<(object? Key, List<IterationRow> Rows, object?[] SortKeys, int Index)>();
        foreach (var instance in instances)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            context.BeginGroup(group.Name, instance.Key);
            try
            {
                context.PrimeGroupScope(instance.Rows.Select(row => row.Fields));
                PublishPlanningRow(context, instance.Rows[0]);
                EvaluateGroupVariables(context, group, depth);
                if (!string.IsNullOrWhiteSpace(group.FilterExpression)
                    && !IsTruthy(_evaluator.Evaluate(group.FilterExpression, context)))
                {
                    continue;
                }
                var keys = group.SortExpressions.Select(sort => _evaluator.Evaluate(sort.Expression, context)).ToArray();
                var children = ApplyGroupFiltersAndSorts(instance.Rows, definition, context, depth + 1);
                if (children.Count > 0)
                {
                    var identity = new object();
                    var identified = children.Select(row =>
                    {
                        _budget.IterationRow();
                        var identities = row.GroupInstances?.ToArray() ?? new object?[definition.Groups.Count];
                        identities[depth] = identity;
                        return row with { GroupInstances = identities };
                    }).ToList();
                    retained.Add((instance.Key, identified, keys, instance.Index));
                }
            }
            finally { context.EndGroup(); }
        }
        if (group.SortExpressions.Count > 0)
        {
            retained.Sort((left, right) =>
            {
                for (int i = 0; i < group.SortExpressions.Count; i++)
                {
                    int comparison = CompareValues(left.SortKeys[i], right.SortKeys[i]);
                    if (comparison != 0)
                    {
                        return group.SortExpressions[i].Direction == SortDirection.Descending
                            ? -Math.Sign(comparison) : comparison;
                    }
                }
                return left.Index.CompareTo(right.Index);
            });
        }
        _cancellationToken.ThrowIfCancellationRequested();
        return retained.SelectMany(instance => instance.Rows).ToList();
    }

    private static void PublishPlanningRow(ReportExpressionContext context, IterationRow row)
    {
        context.SetCurrentRowNoSnapshot(row.Fields);
        if (row.SourceRows is null) return;
        foreach (var pair in row.SourceRows) context.SetSourceCurrentRow(pair.Key, pair.Value);
    }
}
