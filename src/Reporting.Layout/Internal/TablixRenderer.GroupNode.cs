using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;

namespace Reporting.Layout.Internal;

internal static partial class TablixRenderer
{

    /// <summary>An ordered prefix tree of group-key paths: children keep first-seen insertion order so
    /// flattening to <see cref="Leaves"/> yields the hierarchical row/column ordering of a crosstab.</summary>
    private sealed class GroupNode
    {
        private readonly List<string> _order = new();
        private readonly Dictionary<string, GroupNode> _children = new(StringComparer.Ordinal);
        private IReportExpressionContext? _sample; // first row that reached this node (for SortExpression)

        public void Add(IReadOnlyList<string> path, IReportExpressionContext rowCtx)
        {
            var cur = this;
            foreach (var k in path)
            {
                if (!cur._children.TryGetValue(k, out var child))
                {
                    child = new GroupNode();
                    child._sample = rowCtx;
                    cur._children[k] = child;
                    cur._order.Add(k);
                }
                cur = child;
            }
        }

        /// <summary>Orders siblings at each level by that level's group SortExpression, evaluated on a
        /// representative (first-seen) row of each group. This correctly orders by per-row keys (group label,
        /// a field); an AGGREGATE SortExpression (e.g. Sum) resolves against the whole dataset, not the
        /// group, so it can't order groups — use a per-row sort key for now. Levels without a SortExpression
        /// keep data order; ties keep data order too (stable).</summary>
        public void Sort(IReadOnlyList<TablixGroup> levels, ExpressionEvaluator ev, int depth = 0)
        {
            if (depth < levels.Count && !string.IsNullOrWhiteSpace(levels[depth].SortExpression))
            {
                var expr = levels[depth].SortExpression!;
                bool desc = levels[depth].SortDescending;
                // Stable sort: List<T>.Sort is unstable (introsort), so carry the original index as a
                // tiebreaker — equal sort keys keep their data order, and the result is deterministic.
                var ordered = _order.Select((k, i) => (Key: k, Index: i)).ToList();
                ordered.Sort((a, b) =>
                {
                    int cmp = CompareSortValues(EvalSort(ev, expr, _children[a.Key]._sample),
                                                EvalSort(ev, expr, _children[b.Key]._sample));
                    if (cmp != 0)
                    {
                        return desc ? -cmp : cmp;
                    }
                    return a.Index.CompareTo(b.Index); // tie → data order, regardless of direction
                });
                _order.Clear();
                _order.AddRange(ordered.Select(t => t.Key));
            }
            foreach (var child in _children.Values)
            {
                child.Sort(levels, ev, depth + 1);
            }
        }

        public List<string[]> Leaves(int depth)
        {
            var result = new List<string[]>();
            var path = new string[depth];
            void Dfs(GroupNode node, int d)
            {
                if (d == depth)
                {
                    result.Add((string[])path.Clone());
                    return;
                }
                foreach (var k in node._order)
                {
                    path[d] = k;
                    Dfs(node._children[k], d + 1);
                }
            }
            Dfs(this, 0);
            return result;
        }
    }
}
