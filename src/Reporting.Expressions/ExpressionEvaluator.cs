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

            if (TryEvaluateLookup(name, args, context, out var lookup))
            {
                args.Result = lookup;
                return;
            }

            if (TryEvaluatePositional(name, args, context, out var positional))
            {
                args.Result = positional;
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

    // SSRS Lookup(source, dest, result, "Dataset") / LookupSet(...). dest & result are kept as RAW
    // expression text (like aggregates) so they evaluate per target row, not eagerly in the caller scope;
    // source IS evaluated in the caller scope and matched against each row's dest.
    private static bool TryEvaluateLookup(string name, FunctionEventArgs args, IReportExpressionContext context, out object? result)
    {
        result = null;
        bool all;
        if (string.Equals(name, "Lookup", StringComparison.OrdinalIgnoreCase)) { all = false; }
        else if (string.Equals(name, "LookupSet", StringComparison.OrdinalIgnoreCase)) { all = true; }
        else { return false; }
        if (args.Parameters.Count < 4)
        {
            return false;
        }
        var source = args.Parameters.Evaluate(0);
        var destExpr = ExtractRawExpression(args.Parameters[1]);
        var resultExpr = ExtractRawExpression(args.Parameters[2]);
        // The dataset name is an identifier, not a display value — convert invariantly so a non-string
        // arg never picks up culture-specific formatting that wouldn't match the registered key.
        var dataset = Convert.ToString(args.Parameters.Evaluate(3), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        result = context.EvaluateLookup(source, destExpr, resultExpr, dataset, all);
        return true;
    }

    // SSRS positional functions over the current row's position within a scope:
    // RowNumber(scope?) / CountRows(scope?) take an optional SCOPE as the first arg (not an expression);
    // Previous(expr, scope?) takes a raw expression first. All delegate to context.EvaluatePositional.
    private static bool TryEvaluatePositional(string name, FunctionEventArgs args, IReportExpressionContext context, out object? result)
    {
        result = null;
        if (string.Equals(name, "RowNumber", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "CountRows", StringComparison.OrdinalIgnoreCase))
        {
            var scope = args.Parameters.Count >= 1 ? ParseScope(args.Parameters.Evaluate(0)) : AggregateScope.Report;
            result = context.EvaluatePositional(name, string.Empty, scope);
            return true;
        }
        if (string.Equals(name, "Previous", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Parameters.Count == 0)
            {
                return false;
            }
            var expr = ExtractRawExpression(args.Parameters[0]);
            var scope = args.Parameters.Count >= 2 ? ParseScope(args.Parameters.Evaluate(1)) : AggregateScope.Report;
            result = context.EvaluatePositional("Previous", expr, scope);
            return true;
        }
        return false;
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
            case "hour": result = n > 0 ? Dt(0).Hour : 0; return true;
            case "minute": result = n > 0 ? Dt(0).Minute : 0; return true;
            case "second": result = n > 0 ? Dt(0).Second : 0; return true;
            case "weekday": result = n > 0 ? (int)Dt(0).DayOfWeek + 1 : 0; return true; // VB: Sunday = 1

            // ── Date math (VB-style interval strings) ──
            case "dateadd":
                if (n < 3) return false;
                result = AddInterval(S(0), Int(1), Dt(2));
                return true;
            case "datediff":
                if (n < 3) return false;
                result = DiffInterval(S(0), Dt(1), Dt(2));
                return true;

            // ── More text ──
            case "instr": // VB: 1-based position, 0 if not found. InStr(s, sub) or InStr(start, s, sub).
                if (n == 2) { result = S(0).IndexOf(S(1), StringComparison.Ordinal) + 1; return true; }
                if (n >= 3)
                {
                    var from = Math.Max(1, Int(0)) - 1;
                    var hay = S(1);
                    result = from >= hay.Length ? 0 : hay.IndexOf(S(2), from, StringComparison.Ordinal) + 1;
                    return true;
                }
                return false;

            // ── Conversions (VB Cxxx) ──
            // Numeric/date parsing uses InvariantCulture so a string literal means the same as the
            // equivalent NCalc literal (the expression language is invariant — '1.5' == 1.5, never 15).
            // CStr formats with the report culture (the display side); already-typed values pass through.
            case "cstr": result = n > 0 ? S(0) : string.Empty; return true;
            case "cint": // parse as double then round (VB CInt; "2.5" → 2), banker's rounding like VB
                result = n > 0 ? (int)Math.Round(Convert.ToDouble(p.Evaluate(0), CultureInfo.InvariantCulture), MidpointRounding.ToEven) : 0;
                return true;
            case "cdbl": result = n > 0 ? Convert.ToDouble(p.Evaluate(0), CultureInfo.InvariantCulture) : 0d; return true;
            case "cdec": result = n > 0 ? Convert.ToDecimal(p.Evaluate(0), CultureInfo.InvariantCulture) : 0m; return true;
            case "cbool": result = n > 0 && Bool(0); return true;
            case "cdate":
                if (n == 0) { result = default(DateTime); return true; }
                { var v = p.Evaluate(0); result = v is DateTime cd ? cd : Convert.ToDateTime(v, CultureInfo.InvariantCulture); }
                return true;

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

    // VB-style DateAdd: the interval is a short string ("yyyy"/"m"/"d"/"h"/"n"/"s"/"ww"/"q", with long
    // aliases). An unknown interval returns the date unchanged.
    private static DateTime AddInterval(string interval, int number, DateTime date) => interval.ToLowerInvariant() switch
    {
        "yyyy" or "year" => date.AddYears(number),
        "q" or "quarter" => date.AddMonths(number * 3),
        "m" or "month" => date.AddMonths(number),
        "d" or "day" or "y" or "dy" => date.AddDays(number),
        "ww" or "week" or "wk" => date.AddDays(number * 7),
        "h" or "hour" => date.AddHours(number),
        "n" or "minute" or "mi" => date.AddMinutes(number),
        "s" or "second" => date.AddSeconds(number),
        _ => date,
    };

    // VB-style DateDiff: count of interval boundaries between d1 and d2 (year/month/quarter are
    // boundary-based like VB; day/week/hour/minute/second are elapsed).
    private static int DiffInterval(string interval, DateTime d1, DateTime d2)
    {
        var span = d2 - d1;
        return interval.ToLowerInvariant() switch
        {
            "yyyy" or "year" => d2.Year - d1.Year,
            "q" or "quarter" => (d2.Year - d1.Year) * 4 + ((d2.Month - 1) / 3 - (d1.Month - 1) / 3),
            "m" or "month" => (d2.Year - d1.Year) * 12 + (d2.Month - d1.Month),
            "d" or "day" or "y" or "dy" => (int)(d2.Date - d1.Date).TotalDays,
            "ww" or "week" or "wk" => (int)((d2.Date - d1.Date).TotalDays / 7),
            "h" or "hour" => (int)span.TotalHours,
            "n" or "minute" or "mi" => (int)span.TotalMinutes,
            "s" or "second" => (int)span.TotalSeconds,
            _ => 0,
        };
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
        new(StringComparer.OrdinalIgnoreCase) { "Sum", "Avg", "Average", "Count", "Min", "Max", "RunningTotal", "First", "Last", "CountDistinct" };
}

public sealed class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string expression, Exception inner)
        : base($"Failed to evaluate expression: {expression}", inner)
        => Expression = expression;

    public string Expression { get; }
}
