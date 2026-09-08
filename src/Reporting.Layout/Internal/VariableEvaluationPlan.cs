using NCalc;
using Reporting.Expressions;
using Reporting.Parameters;
namespace Reporting.Layout.Internal;

internal sealed class VariableEvaluationPlan
{
    private readonly List<ReportVariable> _ordered = [];
    private readonly Dictionary<string, string[]> _references = new(StringComparer.OrdinalIgnoreCase);

    internal VariableEvaluationPlan(IEnumerable<ReportVariable> variables, ExpressionCompiler compiler, bool groupDefinition = false)
    {
        var definitions = new Dictionary<string, ReportVariable>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            if (!definitions.TryAdd(variable.Name, variable)) throw new InvalidOperationException($"Duplicate variable '{variable.Name}'.");
            _references[variable.Name] = string.IsNullOrWhiteSpace(variable.Expression) ? [] : References(compiler.Compile(variable.Expression).LogicalExpression!);
        }
        var pending = definitions.Keys.ToDictionary(name => name, _ => 0, StringComparer.OrdinalIgnoreCase);
        var dependents = definitions.Keys.ToDictionary(name => name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var variable in definitions.Values)
        {
            foreach (var name in _references[variable.Name])
            {
                if (!definitions.TryGetValue(name, out var dependency)) continue;
                if (!groupDefinition && ((variable.Scope == VariableScope.Report && dependency.Scope != VariableScope.Report)
                    || (variable.Scope == VariableScope.Group && dependency.Scope == VariableScope.Row)))
                {
                    throw new InvalidOperationException($"Variable '{variable.Name}' depends on the shorter-lived scope of '{name}'.");
                }
                pending[variable.Name]++;
                dependents[name].Add(variable.Name);
            }
        }
        var ready = new Queue<string>(pending.Where(item => item.Value == 0).Select(item => item.Key));
        while (ready.TryDequeue(out var name))
        {
            _ordered.Add(definitions[name]);
            foreach (var dependent in dependents[name]) if (--pending[dependent] == 0) ready.Enqueue(dependent);
        }
        if (_ordered.Count != definitions.Count) throw new InvalidOperationException("Cyclic variable dependency.");
    }

    internal void Evaluate(ExpressionEvaluator evaluator, ReportExpressionContext context,
        Func<ReportVariable, bool> select, Action<string, object?> set, CancellationToken token)
    {
        foreach (var variable in _ordered)
        {
            token.ThrowIfCancellationRequested();
            if (!select(variable)) continue;
            foreach (string name in _references[variable.Name])
            {
                if (!context.Variables.Contains(name)) throw new InvalidOperationException($"Variable '{variable.Name}' references unknown variable '{name}'.");
            }
            set(variable.Name, string.IsNullOrWhiteSpace(variable.Expression) ? variable.InitialValue : evaluator.Evaluate(variable.Expression, context));
            token.ThrowIfCancellationRequested();
        }
    }

    private static string[] References(LogicalExpression expression)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<LogicalExpression>();
        pending.Push(expression);
        while (pending.TryPop(out var node))
        {
            switch (node)
            {
                case Identifier id when id.Name.StartsWith("Variables.", StringComparison.OrdinalIgnoreCase) || id.Name.StartsWith("Variables!", StringComparison.OrdinalIgnoreCase):
                    names.Add(id.Name[10..]); break;
                case BinaryExpression binary:
                    pending.Push(binary.LeftExpression); pending.Push(binary.RightExpression); break;
                case UnaryExpression unary: pending.Push(unary.Expression); break;
                case TernaryExpression ternary:
                    pending.Push(ternary.LeftExpression); pending.Push(ternary.MiddleExpression); pending.Push(ternary.RightExpression); break;
                case Function function:
                    foreach (var argument in function.Parameters) pending.Push(argument);
                    break;
            }
        }
        return names.ToArray();
    }
}
