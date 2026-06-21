using System.Globalization;

namespace Reporting.Expressions;

/// <summary>Computes Sum/Avg/Count/Min/Max/RunningTotal over a list of row snapshots.</summary>
internal static class AggregateCalculator
{
    public static object? Calculate(
        string function,
        string expression,
        IReadOnlyList<DictionaryLookup> rows,
        ExpressionEvaluator evaluator,
        ReportExpressionContext owner)
    {
        return function.ToUpperInvariant() switch
        {
            "COUNT" => CountRows(expression, rows, evaluator, owner),
            "SUM" or "RUNNINGTOTAL" => SumRows(expression, rows, evaluator, owner),
            "AVG" or "AVERAGE" => AverageRows(expression, rows, evaluator, owner),
            "MIN" => MinRows(expression, rows, evaluator, owner),
            "MAX" => MaxRows(expression, rows, evaluator, owner),
            "FIRST" => EvaluatePerRow(expression, rows, evaluator, owner).FirstOrDefault(),
            "LAST" => EvaluatePerRow(expression, rows, evaluator, owner).LastOrDefault(),
            "COUNTDISTINCT" => CountDistinctRows(expression, rows, evaluator, owner),
            // SSRS statistical aggregates: Var/StDev are SAMPLE (÷ n-1); VarP/StDevP are POPULATION (÷ n).
            "VAR" => VarianceRows(expression, rows, evaluator, owner, sample: true),
            "VARP" => VarianceRows(expression, rows, evaluator, owner, sample: false),
            "STDEV" => StdDevRows(expression, rows, evaluator, owner, sample: true),
            "STDEVP" => StdDevRows(expression, rows, evaluator, owner, sample: false),
            _ => throw new InvalidOperationException($"Unknown aggregate function: {function}"),
        };
    }

    private static int CountDistinctRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        var seen = new HashSet<object>();
        foreach (var value in EvaluatePerRow(expression, rows, evaluator, owner))
        {
            if (value is not null)
            {
                // Normalize numerics to decimal so int 1 / long 1 / double 1.0 / decimal 1m count as one
                // distinct value (consistent with SumRows/Compare, which also funnel through decimal).
                seen.Add(NormalizeDistinctKey(value));
            }
        }
        return seen.Count;
    }

    private static object NormalizeDistinctKey(object value) => value switch
    {
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        _ => value,
    };

    private static int CountRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        // Count rows where expression evaluates to non-null
        int count = 0;
        foreach (var _ in EvaluatePerRow(expression, rows, evaluator, owner))
        {
            count++;
        }
        return count;
    }

    private static decimal SumRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        decimal sum = 0m;
        foreach (var value in EvaluatePerRow(expression, rows, evaluator, owner))
        {
            sum += ToDecimal(value);
        }
        return sum;
    }

    private static decimal AverageRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        decimal sum = 0m;
        int count = 0;
        foreach (var value in EvaluatePerRow(expression, rows, evaluator, owner))
        {
            sum += ToDecimal(value);
            count++;
        }
        return count == 0 ? 0m : sum / count;
    }

    private static object? MinRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        object? min = null;
        foreach (var value in EvaluatePerRow(expression, rows, evaluator, owner))
        {
            if (value is null)
            {
                continue;
            }
            if (min is null || Compare(value, min) < 0)
            {
                min = value;
            }
        }
        return min;
    }

    private static object? MaxRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        object? max = null;
        foreach (var value in EvaluatePerRow(expression, rows, evaluator, owner))
        {
            if (value is null)
            {
                continue;
            }
            if (max is null || Compare(value, max) > 0)
            {
                max = value;
            }
        }
        return max;
    }

    private static decimal VarianceRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner, bool sample)
        => Variance(MaterializeDecimals(expression, rows, evaluator, owner), sample);

    private static decimal StdDevRows(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner, bool sample)
    {
        var variance = Variance(MaterializeDecimals(expression, rows, evaluator, owner), sample);
        // decimal has no Sqrt; route through double. Variance is non-negative so the cast back is safe.
        return variance <= 0m ? 0m : (decimal)Math.Sqrt((double)variance);
    }

    /// <summary>Population (<paramref name="sample"/>=false) variance divides by n; sample variance divides by
    /// n-1 (Bessel's correction). Returns 0 when there are too few rows (sample needs ≥2, population ≥1),
    /// matching the engine's 0-on-empty convention (e.g. <c>AverageRows</c>).</summary>
    private static decimal Variance(List<decimal> values, bool sample)
    {
        int n = values.Count;
        int divisor = sample ? n - 1 : n;
        if (divisor <= 0)
        {
            return 0m;
        }
        decimal sum = 0m;
        foreach (var v in values)
        {
            sum += v;
        }
        decimal mean = sum / n;
        decimal sumSq = 0m;
        foreach (var v in values)
        {
            var delta = v - mean;
            sumSq += delta * delta;
        }
        return sumSq / divisor;
    }

    // EvaluatePerRow swaps the live Fields row per item, so it MUST be enumerated once; variance needs two
    // passes (mean, then squared deviations), hence materialize to a list up front.
    private static List<decimal> MaterializeDecimals(string expression, IReadOnlyList<DictionaryLookup> rows, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        var list = new List<decimal>();
        foreach (var value in EvaluatePerRow(expression, rows, evaluator, owner))
        {
            list.Add(ToDecimal(value));
        }
        return list;
    }

    private static IEnumerable<object?> EvaluatePerRow(
        string expression,
        IReadOnlyList<DictionaryLookup> rows,
        ExpressionEvaluator evaluator,
        ReportExpressionContext owner)
    {
        // Temporarily swap the current Fields lookup with each historical row, then restore.
        var liveSnapshot = ((DictionaryLookup)owner.Fields).Keys
            .Select(k => new KeyValuePair<string, object?>(k, owner.Fields[k]))
            .ToList();

        try
        {
            foreach (var row in rows)
            {
                owner.SetCurrentRowNoSnapshot(row);
                object? value;
                try
                {
                    value = evaluator.Evaluate(expression, owner);
                }
                catch
                {
                    continue;
                }
                yield return value;
            }
        }
        finally
        {
            owner.SetCurrentRowNoSnapshot(liveSnapshot);
        }
    }

    private static decimal ToDecimal(object? value)
    {
        if (value is null)
        {
            return 0m;
        }
        if (value is decimal d)
        {
            return d;
        }
        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static int Compare(object a, object b)
    {
        if (a is IComparable ca && a.GetType() == b.GetType())
        {
            return ca.CompareTo(b);
        }
        var da = ToDecimal(a);
        var db = ToDecimal(b);
        return da.CompareTo(db);
    }
}
