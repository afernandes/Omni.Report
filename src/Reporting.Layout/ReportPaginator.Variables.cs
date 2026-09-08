using Reporting.Expressions;
using Reporting.Layout.Internal;
using Reporting.Parameters;

namespace Reporting.Layout;

public sealed partial class ReportPaginator
{
    private VariableEvaluationPlan? _variables;
    private Dictionary<string, object?>? _reportVariableValues;
    private readonly Dictionary<Reporting.Bands.GroupBand, VariableEvaluationPlan> _groupVariables = new(ReferenceEqualityComparer.Instance);

    private void InitializeVariables(ReportExpressionContext context, ReportDefinition definition)
    {
        _variables ??= new VariableEvaluationPlan(definition.Variables, _compiler);
        foreach (var variable in definition.Variables) context.VariablesStore.Set(variable.Name, variable.InitialValue);
        if (_reportVariableValues is null)
        {
            _variables.Evaluate(_evaluator, context, variable => variable.Scope == VariableScope.Report,
                context.VariablesStore.Set, _cancellationToken);
            _reportVariableValues = definition.Variables.Where(variable => variable.Scope == VariableScope.Report)
                .ToDictionary(variable => variable.Name, variable => context.VariablesStore[variable.Name], StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            foreach (var value in _reportVariableValues) context.VariablesStore.Set(value.Key, value.Value);
        }
    }

    private void EvaluateGroupVariables(ReportExpressionContext context, Reporting.Bands.GroupBand group, int depth)
    {
        using var scope = context.UseGroup(depth);
        if (!_groupVariables.TryGetValue(group, out var plan))
        {
            plan = new VariableEvaluationPlan(group.Variables, _compiler, groupDefinition: true);
            _groupVariables.Add(group, plan);
        }
        foreach (var variable in group.Variables) context.SetGroupVariable(variable.Name, variable.InitialValue);
        plan.Evaluate(_evaluator, context, _ => true, context.SetGroupVariable, _cancellationToken);
        _variables!.Evaluate(_evaluator, context, variable => variable.Scope == VariableScope.Group,
            context.SetGroupVariable, _cancellationToken);
    }
}
