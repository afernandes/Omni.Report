using System.Globalization;
namespace Reporting.Expressions;

/// <summary>Accumulates newly appended immutable rows, committing only a successful batch.</summary>
internal sealed class FieldAggregateAccumulator
{
    private int _processed;
    private int _count;
    private decimal _sum;
    internal void Append(IReadOnlyList<DictionaryLookup> rows, string expression, string? field,
        bool countOnly, ExpressionEvaluator evaluator, ReportExpressionContext owner)
    {
        if (_processed == rows.Count) return;
        decimal sum = _sum;
        int count = _count;
        foreach (var value in Values())
        {
            owner.CancellationToken.ThrowIfCancellationRequested();
            if (!countOnly) sum += value is null ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            count++;
        }
        _sum = sum; _count = count; _processed = rows.Count;
        IEnumerable<object?> Values()
        {
            if (field is null)
            {
                foreach (var value in AggregateCalculator.EvaluatePerRow(expression, rows, evaluator, owner, _processed)) yield return value;
            }
            else
            {
                for (int index = _processed; index < rows.Count; index++) yield return rows[index][field];
            }
        }
    }
    internal object Value(string function) => function.ToUpperInvariant() switch
    {
        "COUNT" => _count,
        "AVG" or "AVERAGE" => _count == 0 ? 0m : _sum / _count,
        _ => _sum
    };
}
