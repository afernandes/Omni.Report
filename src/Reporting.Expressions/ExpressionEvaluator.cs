using System.Globalization;
using NCalc;
using NCalc.Handlers;
using Reporting.Aggregates;

namespace Reporting.Expressions;

/// <summary>High-level evaluator that wires a compiled <see cref="Expression"/> to an
/// <see cref="IReportExpressionContext"/>, exposing report fields, parameters, variables,
/// aggregates and built-in functions.</summary>
public sealed class ExpressionEvaluator
{
    private readonly ExpressionCompiler _compiler;

    public ExpressionEvaluator(ExpressionCompiler? compiler = null)
        => _compiler = compiler ?? new ExpressionCompiler();

    /// <summary>Optional resolver for RDL <c>Code.MethodName(...)</c> calls. <c>null</c> by
    /// default — the core engine never executes C#. The opt-in <c>Reporting.Expressions.Roslyn</c>
    /// package sets this to a Roslyn-backed compiler. Receives the method name (without the
    /// <c>Code.</c> prefix) and the already-evaluated arguments, and returns the method's result.</summary>
    public Func<string, object?[], object?>? CodeFunctionResolver { get; set; }

    /// <summary>Evaluates <paramref name="expression"/> against <paramref name="context"/>.</summary>
    public object? Evaluate(string expression, IReportExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var expr = _compiler.Compile(expression);
        Bind(expr, context);
        try
        {
            return expr.Evaluate();
        }
        catch (Exception ex) when (ex is not ExpressionParseException)
        {
            throw new ExpressionEvaluationException(expression, ex);
        }
    }

    /// <summary>Evaluates an expression and coerces the result to <typeparamref name="T"/>.</summary>
    public T? Evaluate<T>(string expression, IReportExpressionContext context)
    {
        var value = Evaluate(expression, context);
        if (value is null)
        {
            return default;
        }
        if (value is T direct)
        {
            return direct;
        }
        return (T)Convert.ChangeType(value, typeof(T), context.Culture);
    }

    private void Bind(Expression expression, IReportExpressionContext context)
    {
        expression.EvaluateParameter += (name, args) =>
        {
            if (ExpressionRewriter.TrySplitScope(name, out var scope, out var member))
            {
                // Split nested chain: "Cliente.Nome" → head "Cliente", tail "Nome".
                var dot = member.IndexOf('.');
                var head = dot < 0 ? member : member[..dot];
                var tail = dot < 0 ? null : member[(dot + 1)..];

                // For Fields, qualified form Fields.SourceName.X takes precedence: when "head"
                // matches a registered source name AND a tail is present, resolve against that
                // source's current record. Falls through to single-source behavior otherwise,
                // preserving Fields.Cliente.Nome (member chain on a nested object) when no
                // source called "Cliente" exists.
                if (scope == "Fields" && tail is not null)
                {
                    var sourceLookup = context.GetSource(head);
                    if (sourceLookup is not null)
                    {
                        var dot2 = tail.IndexOf('.');
                        var tailHead = dot2 < 0 ? tail : tail[..dot2];
                        var tailRest = dot2 < 0 ? null : tail[(dot2 + 1)..];
                        var v = sourceLookup[tailHead];
                        args.Result = tailRest is null ? v : MemberPathResolver.Resolve(v, tailRest);
                        return;
                    }
                }

                object? value = scope switch
                {
                    // Fields.X resolution falls back to every other registered source when the
                    // live row doesn't contain the field. This is the master-detail UX: dropping
                    // a parent field into the child's detail band "just works" without forcing
                    // the user to qualify everything. Crystal/SSRS/FastReport designers all do
                    // this implicitly via their data-binding metadata; we replicate it at the
                    // expression layer so both code-first and designer reports benefit.
                    "Fields" => context.TryResolveUnqualifiedField(head, out var v) ? v : null,
                    "Parameters" => context.Parameters[head],
                    "Variables" => context.Variables[head],
                    "Page" => head switch
                    {
                        "Number" or "PageNumber" => context.PageNumber,
                        "Total" or "TotalPages" => context.TotalPages,
                        _ => null,
                    },
                    _ => null,
                };

                args.Result = tail is null ? value : MemberPathResolver.Resolve(value, tail);
                return;
            }

            // Bare identifiers — recognize a small set of well-known names. Field resolution
            // uses the same master-detail-aware fallback as Fields.X: live row first, then
            // every other registered source's current row.
            if (name == "Now") { args.Result = context.Now; return; }
            if (name == "Today") { args.Result = context.Today; return; }
            if (name == "UserName") { args.Result = context.UserName; return; }
            if (name == "GroupKey") { args.Result = context.GroupKey; return; }
            if (name == "PageNumber") { args.Result = context.PageNumber; return; }
            if (name == "TotalPages") { args.Result = context.TotalPages; return; }
            if (context.TryResolveUnqualifiedField(name, out var fieldValue))
            {
                args.Result = fieldValue;
                return;
            }
            if (context.Variables.Contains(name)) { args.Result = context.Variables[name]; return; }
            if (context.Parameters.Contains(name)) { args.Result = context.Parameters[name]; return; }
            args.Result = null;
        };

        expression.EvaluateFunction += (name, args) =>
        {
            if (TryEvaluateAggregate(name, args, context, out var aggregate))
            {
                args.Result = aggregate;
                return;
            }

            if (TryEvaluateBuiltin(name, args, context, out var builtin))
            {
                args.Result = builtin;
                return;
            }

            // RDL Code element: route Code.MethodName(...) to the opt-in resolver when present.
            // ExpressionRewriter flattens "Code.Method(" → "Code_Method(", so NCalc surfaces it
            // as the function name "Code_MethodName".
            if (CodeFunctionResolver is { } resolver && name.StartsWith("Code_", StringComparison.OrdinalIgnoreCase))
            {
                var methodName = name["Code_".Length..];
                var values = new object?[args.Parameters.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = args.Parameters[i].Evaluate();
                }
                args.Result = resolver(methodName, values);
            }
        };
    }

    private static bool TryEvaluateAggregate(string name, FunctionArgs args, IReportExpressionContext context, out object? result)
    {
        result = null;
        if (!AggregateNames.Contains(name))
        {
            return false;
        }
        if (args.Parameters.Length == 0)
        {
            return false;
        }
        // The first argument is the expression text (we re-extract it from the AST).
        var expressionText = ExtractRawExpression(args.Parameters[0]);
        var scope = AggregateScope.Report;
        if (args.Parameters.Length >= 2)
        {
            var scopeValue = args.Parameters[1].Evaluate();
            scope = ParseScope(scopeValue);
        }
        result = context.EvaluateAggregate(name, expressionText, scope);
        return true;
    }

    private static bool TryEvaluateBuiltin(string name, FunctionArgs args, IReportExpressionContext context, out object? result)
    {
        result = null;
        switch (name)
        {
            case "Today":
                result = context.Today;
                return true;
            case "Now":
                result = context.Now;
                return true;
            case "PageNumber":
                result = context.PageNumber;
                return true;
            case "TotalPages":
                result = context.TotalPages;
                return true;
            case "Coalesce":
                foreach (var p in args.Parameters)
                {
                    var v = p.Evaluate();
                    if (v is not null)
                    {
                        result = v;
                        return true;
                    }
                }
                return true;
            case "IsNull":
                result = args.Parameters.Length > 0 && args.Parameters[0].Evaluate() is null;
                return true;
            case "Format":
                if (args.Parameters.Length >= 2)
                {
                    var value = args.Parameters[0].Evaluate();
                    var format = Convert.ToString(args.Parameters[1].Evaluate(), context.Culture) ?? string.Empty;
                    result = ValueFormatter.Format(value, format, context.Culture);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static AggregateScope ParseScope(object? value)
    {
        if (value is null)
        {
            return AggregateScope.Report;
        }
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return text switch
        {
            "Group" or "group" => AggregateScope.Group,
            "Page" or "page" => AggregateScope.Page,
            "Running" or "running" => AggregateScope.Running,
            _ => AggregateScope.Report,
        };
    }

    private static string ExtractRawExpression(Expression expression)
    {
        // NCalc keeps the original parsed text on the underlying LogicalExpression via ToString().
        // For Identifier/Bracket nodes this yields the bare name, e.g. "[Fields.Total]" → "Fields.Total".
        var text = expression.LogicalExpression?.ToString() ?? string.Empty;
        return text.Trim('[', ']');
    }

    private static readonly HashSet<string> AggregateNames =
        new(StringComparer.OrdinalIgnoreCase) { "Sum", "Avg", "Average", "Count", "Min", "Max", "RunningTotal" };
}

public sealed class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string expression, Exception inner)
        : base($"Failed to evaluate expression: {expression}", inner)
        => Expression = expression;

    public string Expression { get; }
}
