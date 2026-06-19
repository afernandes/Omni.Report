using Reporting.Common;
using Reporting.Elements;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    /// <summary>How deep nested subreports may go before we stop recursing — guards a report that
    /// references itself (directly or via a cycle in the resolver).</summary>
    private const int MaxSubreportDepth = 4;

    /// <summary>Renders a <see cref="SubreportElement"/> by paginating the referenced child report
    /// as a single continuous page at the subreport's width, then offsetting + clipping the child's
    /// primitives into the element's bounds. Child parameters come from the element's bindings
    /// (evaluated in the parent context); the child shares the parent's data sources.</summary>
    private IReadOnlyList<LayoutPrimitive> RenderSubreport(
        SubreportElement sub, Rectangle bounds, IReportExpressionContext parentCtx,
        PaginationRequest request, ITextMeasurer measurer)
    {
        // Resolve the child: inline definition wins; otherwise resolve the id via the host.
        var childDef = sub.InlineDefinition
            ?? (sub.ReportId is { Length: > 0 } id ? request.SubreportResolver?.Invoke(id) : null);
        if (childDef is null || request.SubreportDepth >= MaxSubreportDepth)
        {
            return [];
        }

        // Child parameters: inherit the parent's, then apply the bindings. Each binding expression
        // is evaluated in the PARENT context so {Fields.x}/{Parameters.y} resolve against the row
        // that owns the subreport.
        var childParams = new Dictionary<string, object?>(request.Parameters);
        foreach (var binding in sub.ParameterBindings)
        {
            childParams[binding.Key] = SafeEval(binding.Value, parentCtx);
        }

        // Lay the child out as one continuous page at the subreport's width (zero margins) so its
        // bands flow inside the element box; we then translate + clip the result into place.
        var childRequest = new PaginationRequest
        {
            Definition = childDef with { PageSetup = new PageSetup(new PaperSize("Subreport", bounds.Width, Unit.Zero)) },
            DataSources = request.DataSources,
            Parameters = childParams,
            Measurer = measurer,
            CodeFunctionResolver = request.CodeFunctionResolver,
            SubreportResolver = request.SubreportResolver,
            SubreportDepth = request.SubreportDepth + 1,
        };

        // The child's data is already materialised in-memory (shared registry), so the async
        // paginate completes synchronously — blocking here is safe and keeps BandRenderer sync.
        var child = new ReportPaginator(_compiler)
            .PaginateAsync(childRequest).GetAwaiter().GetResult();
        if (child.Pages.Count == 0)
        {
            return [];
        }

        var result = new List<LayoutPrimitive>();
        foreach (var prim in child.Pages[0].Primitives)
        {
            var moved = TranslatePrimitive(prim, bounds.X, bounds.Y);
            // Vertical clip: drop primitives that start beyond the element's bottom edge.
            if (moved.Bounds.Y >= bounds.Bottom)
            {
                continue;
            }
            result.Add(moved);
        }
        return result;
    }

    private object? SafeEval(string expression, IReportExpressionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }
        try
        {
            return _evaluator.Evaluate(expression, ctx);
        }
        catch
        {
            // A bad binding expression shouldn't blow up the whole report — the child just sees null.
            return null;
        }
    }

    /// <summary>Returns a copy of <paramref name="p"/> translated by (dx, dy). Handles the extra
    /// coordinate-bearing primitives (line endpoints, polygon vertices) in addition to Bounds.</summary>
    private static LayoutPrimitive TranslatePrimitive(LayoutPrimitive p, Unit dx, Unit dy)
    {
        var b = p.Bounds;
        var moved = new Rectangle(b.X + dx, b.Y + dy, b.Width, b.Height);
        return p switch
        {
            DrawLinePrimitive ln => ln with
            {
                Bounds = moved,
                From = new Point(ln.From.X + dx, ln.From.Y + dy),
                To = new Point(ln.To.X + dx, ln.To.Y + dy),
            },
            DrawPolygonPrimitive pg => pg with
            {
                Bounds = moved,
                Points = new EquatableArray<Point>(pg.Points.Select(pt => new Point(pt.X + dx, pt.Y + dy)).ToArray()),
            },
            _ => p with { Bounds = moved },
        };
    }
}
