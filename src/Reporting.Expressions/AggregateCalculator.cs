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
                seen.Add(value);
            }
        }
        return seen.Count;
    }

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
