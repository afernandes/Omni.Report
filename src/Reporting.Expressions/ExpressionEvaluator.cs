using System.Globalization;
using NCalc;
using NCalc.Extensions;
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

            if (TryEvaluateScalarFunction(name, args, context, out var scalar))
            {
                args.Result = scalar;
                return;
            }

            // RDL Code element: route Code.MethodName(...) to the opt-in resolver when present.
            // ExpressionRewriter flattens "Code.Method(" → "Code_Method(", so NCalc surfaces it
            // as the function name "Code_MethodName".
            if (CodeFunctionResolver is { } resolver && name.StartsWith("Code_", StringComparison.OrdinalIgnoreCase))
            {
                var methodName = name["Code_".Length..];
                var values = new object?[args.Parameters.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = args.Parameters.Evaluate(i);
                }
                args.Result = resolver(methodName, values);
            }
        };
    }

    private static bool TryEvaluateAggregate(string name, FunctionEventArgs args, IReportExpressionContext context, out object? result)
    {
        result = null;
        if (!AggregateNames.Contains(name))
        {
            return false;
        }
        if (args.Parameters.Count == 0)
        {
            return false;
        }
        // The first argument is the expression text (we re-extract it from the AST).
        var expressionText = ExtractRawExpression(args.Parameters[0]);
        var scope = AggregateScope.Report;
        if (args.Parameters.Count >= 2)
        {
            var scopeValue = args.Parameters.Evaluate(1);
            scope = ParseScope(scopeValue);
        }
        result = context.EvaluateAggregate(name, expressionText, scope);
        return true;
    }

    private static bool TryEvaluateBuiltin(string name, FunctionEventArgs args, IReportExpressionContext context, out object? result)
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
                for (int i = 0; i < args.Parameters.Count; i++)
                {
                    var v = args.Parameters.Evaluate(i);
                    if (v is not null)
                    {
                        result = v;
                        return true;
                    }
                }
                return true;
            case "IsNull":
                result = args.Parameters.Count > 0 && args.Parameters.Evaluate(0) is null;
                return true;
            case "Format":
                if (args.Parameters.Count >= 2)
                {
                    var value = args.Parameters.Evaluate(0);
                    var format = Convert.ToString(args.Parameters.Evaluate(1), context.Culture) ?? string.Empty;
                    result = ValueFormatter.Format(value, format, context.Culture);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    // SSRS/VB-style scalar functions NCalc doesn't provide natively (it already has Abs/Round/Sqrt/etc.):
    // the everyday RDL vocabulary — conditional, text and date-part helpers. Case-insensitive, like SSRS.
    private static bool TryEvaluateScalarFunction(string name, FunctionEventArgs args, IReportExpressionContext context, out object? result)
    {
        result = null;
        var p = args.Parameters;
        int n = p.Count;
        string S(int i) => Convert.ToString(p.Evaluate(i), context.Culture) ?? string.Empty;
        int Int(int i) => Convert.ToInt32(p.Evaluate(i), context.Culture);
        DateTime Dt(int i) { var v = p.Evaluate(i); return v is DateTime d ? d : Convert.ToDateTime(v, context.Culture); }
        bool Bool(int i) => Convert.ToBoolean(p.Evaluate(i), context.Culture);

        // A function whose argument can't be coerced (e.g. a text value where a date/number/bool is
        // expected) degrades to null for this cell — SSRS-style #Error — instead of aborting the whole
        // expression. Arithmetic errors (divide-by-zero) inside a taken branch still propagate as before.
        try
        {
        switch (name.ToLowerInvariant())
        {
            // ── Conditional ──
            case "iif": // only the taken branch is evaluated (safer than SSRS, which evaluates both)
                if (n < 3) return false;
                result = Bool(0) ? p.Evaluate(1) : p.Evaluate(2);
                return true;
            case "isnothing":
                result = n > 0 && p.Evaluate(0) is null;
                return true;
            case "switch": // (cond1, val1, cond2, val2, …) → first matching value, else null
                for (int i = 0; i + 1 < n; i += 2)
                {
                    if (Bool(i)) { result = p.Evaluate(i + 1); return true; }
                }
                return true;
            case "choose": // 1-based index into the value list
                if (n < 2) return false;
                var idx = Int(0);
                if (idx >= 1 && idx < n) { result = p.Evaluate(idx); }
                return true;

            // ── Text ──
            case "len":
                result = n > 0 ? S(0).Length : 0;
                return true;
            case "left":
                if (n < 2) return false;
                { var s = S(0); result = s[..Math.Clamp(Int(1), 0, s.Length)]; }
                return true;
            case "right":
                if (n < 2) return false;
                { var s = S(0); result = s[(s.Length - Math.Clamp(Int(1), 0, s.Length))..]; }
                return true;
            case "mid": // VB Mid: 1-based start, optional length
                if (n < 2) return false;
                {
                    var s = S(0);
                    var start = Math.Max(1, Int(1)) - 1;
                    if (start >= s.Length) { result = string.Empty; return true; }
                    var len = n >= 3 ? Math.Max(0, Int(2)) : s.Length - start;
                    result = s.Substring(start, Math.Min(len, s.Length - start));
                }
                return true;
            case "trim": result = n > 0 ? S(0).Trim() : string.Empty; return true;
            case "ltrim": result = n > 0 ? S(0).TrimStart() : string.Empty; return true;
            case "rtrim": result = n > 0 ? S(0).TrimEnd() : string.Empty; return true;
            case "ucase":
            case "upper": result = n > 0 ? S(0).ToUpper(context.Culture) : string.Empty; return true;
            case "lcase":
            case "lower": result = n > 0 ? S(0).ToLower(context.Culture) : string.Empty; return true;
            case "replace":
                if (n < 3) return false;
                result = S(0).Replace(S(1), S(2));
                return true;

            // ── Date parts ──
            case "year": result = n > 0 ? Dt(0).Year : 0; return true;
            case "month": result = n > 0 ? Dt(0).Month : 0; return true;
            case "day": result = n > 0 ? Dt(0).Day : 0; return true;

            default:
                return false;
        }
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            result = null;
            return true;
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

    private static string ExtractRawExpression(LogicalExpression expression)
    {
        // NCalc v6 surfaces the parsed AST node directly to function handlers. ToExpressionString()
        // serializes it back to its source text (the SerializationVisitor); for Identifier/Bracket
        // nodes that's the bracketed name, e.g. "[Fields.Total]" → "Fields.Total".
        var text = expression.ToExpressionString();
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
