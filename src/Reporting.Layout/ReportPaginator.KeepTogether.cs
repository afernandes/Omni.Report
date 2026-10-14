using Reporting.Bands;
using Reporting.Common;
using Reporting.Expressions;
using Reporting.Geometry;
using Reporting.Layout.Internal;
using Reporting.Paper;
using Reporting.Rendering;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    private bool _measuringGroup;
    private Unit? _measurementLimit;
    private Unit _completedHeight;
    private IReadOnlyList<IterationRow>? _reportScopeRows;
    private Dictionary<string, object?>? _initialVariableValues;
    private int _pageNumberOffset;
    private ReportExpressionContext? _planningContext;
    private ReportExpressionContext? _ancestorContext;
    private int _measurementGroupDepth;

    private Unit MeasureWholeGroup(PaginationRequest request, IReadOnlyList<IterationRow> rows, int start,
        int depth, object?[] keys, ReportExpressionContext current, Unit maximum, Unit width, ITextMeasurer measurer)
    {
        var probe = current.ForkForPlanning();
        var groupRows = new List<IterationRow>();
        for (int index = start; index < rows.Count; index++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            PublishPlanningRow(probe, rows[index]);
            bool belongs = true;
            for (int level = 0; level <= depth; level++)
            {
                if (rows[start].GroupInstances is { } identities
                    ? !ReferenceEquals(identities[level], rows[index].GroupInstances?[level])
                    : !Equals(keys[level], _evaluator.Evaluate(request.Definition.Groups[level].GroupExpression, probe)))
                {
                    belongs = false;
                    break;
                }
            }
            if (!belongs) break;
            groupRows.Add(rows[index]);
        }
        var definition = request.Definition;
        var fragment = definition with
        {
            PageSetup = new PageSetup(new PaperSize("Group measurement", width, Unit.Zero)),
            ReportHeader = null,
            ReportFooter = null,
            PageHeader = null,
            PageFooter = null,
            Detail = definition.Detail with { PageBreak = PageBreak.None, FilterExpression = null, SortExpressions = [] },
            DataSources = new(definition.DataSources.Select(source => source with { FilterExpression = null, SortExpressions = [] })),
            Groups = new(definition.Groups.Select((group, level) => group with
            {
                Header = level < depth ? null : group.Header,
                Footer = level < depth ? null : group.Footer,
                KeepTogether = false,
                NewPageBefore = false,
                NewPageAfter = false,
                PageBreak = PageBreak.None,
                FilterExpression = null,
                SortExpressions = []
            }))
        };
        var fragmentRequest = new PaginationRequest
        {
            Definition = fragment,
            DataSources = request.DataSources,
            Parameters = request.Parameters,
            Measurer = measurer,
            PrimaryDataSource = request.PrimaryDataSource,
            CodeFunctionResolver = request.CodeFunctionResolver,
            SubreportResolver = request.SubreportResolver,
            MapTileResolver = request.MapTileResolver,
            SubreportDepth = request.SubreportDepth
        };
        var run = new ReportPaginator(_compiler)
        {
            _budget = _budget,
            _cancellationToken = _cancellationToken,
            _preparedSources = _preparedSources,
            _measuringGroup = true,
            _measurementLimit = maximum,
            _pageSink = _ => { },
            _reportScopeRows = rows,
            _reportVariableValues = _reportVariableValues,
            _initialVariableValues = current.VariablesStore.Keys.ToDictionary(name => name, name => current.VariablesStore[name]),
            _pageNumberOffset = current.PageNumber - 1,
            _planningContext = current.ForkForPlanning(includeGroups: false),
            _ancestorContext = current,
            _measurementGroupDepth = depth
        };
        run._evaluator.CodeFunctionResolver = request.CodeFunctionResolver;
        foreach (var pair in _resolvedSubreports) run._resolvedSubreports[pair.Key] = pair.Value;
        try
        {
            run.ExecutePass(fragmentRequest, groupRows, _preparedSources, measurer, current.TotalPages);
            return run._completedHeight;
        }
        catch (GroupMeasureOverflowException) { return maximum + new Unit(1); }
    }
}
