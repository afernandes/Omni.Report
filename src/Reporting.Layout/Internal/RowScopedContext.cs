using System.Globalization;
using Reporting.Aggregates;
using Reporting.Expressions;

namespace Reporting.Layout.Internal;

/// <summary>
/// An <see cref="IReportExpressionContext"/> that overlays a single data row's fields onto an
/// existing context. Used by <see cref="ChartRenderer"/> to evaluate each series' category and
/// value expressions against every row of the chart's data source, without mutating — or
/// snapshotting into the aggregate buffers of — the live band context.
/// </summary>
/// <remarks>
/// Parameters, variables, paging state and culture all delegate to the wrapped context, so a
/// chart expression like <c>Fields.total</c> sees the row while <c>Parameters.Ano</c> still
/// resolves. Aggregates fall back to the live context's scope: per-row chart values are the
/// common case; whole-chart aggregates inside a series expression are a documented edge case.
/// </remarks>
internal sealed partial class RowScopedContext : IReportExpressionContext
{
    private readonly IReportExpressionContext _inner;
    private readonly RowLookup _fields;

    public RowScopedContext(IReportExpressionContext inner, IReadOnlyList<KeyValuePair<string, object?>> row)
    {
        _inner = inner;
        _fields = new RowLookup(row);
    }

    public IValueLookup Fields => _fields;
    public IValueLookup Parameters => _inner.Parameters;
    public IValueLookup Variables => _inner.Variables;
    public object? GroupKey => _inner.GroupKey;
    public int PageNumber => _inner.PageNumber;
    public int TotalPages => _inner.TotalPages;
    public DateTime Now => _inner.Now;
    public DateTime Today => _inner.Today;
    public string UserName => _inner.UserName;
    public string ReportName => _inner.ReportName;
    public CultureInfo Culture => _inner.Culture;

    public object? EvaluateAggregate(string function, string expression, AggregateScope scope)
        => _inner.EvaluateAggregate(function, expression, scope);

    // Lookup scans whole datasets registered on the root context, independent of the row scope.
    public object? EvaluateLookup(object? source, string destExpression, string resultExpression, string datasetName, bool all)
        => _inner.EvaluateLookup(source, destExpression, resultExpression, datasetName, all);

    public object? EvaluatePositional(string function, string expression, AggregateScope scope)
        => _inner.EvaluatePositional(function, expression, scope);

    public IValueLookup? GetSource(string sourceName) => _inner.GetSource(sourceName);

    public bool TryResolveUnqualifiedField(string fieldName, out object? value)
    {
        if (!string.IsNullOrEmpty(fieldName) && _fields.Contains(fieldName))
        {
            value = _fields[fieldName];
            return true;
        }
        return _inner.TryResolveUnqualifiedField(fieldName, out value);
    }

    // ReportItems is a report-wide registry — delegate to the shared root context.
    public object? GetReportItem(string name) => _inner.GetReportItem(name);
    public void SetReportItem(string name, object? value) => _inner.SetReportItem(name, value);
}
