using System.Globalization;
using Reporting.Aggregates;
using Reporting.Expressions;

namespace Reporting.Layout.Internal;

/// <summary>
/// An <see cref="IReportExpressionContext"/> that exposes a single matrix cell's aggregate value as
/// <c>Value</c> (and <c>Fields.Value</c>) on top of an existing context. Used by <see cref="TablixRenderer"/>
/// to evaluate a body cell's conditional formats against the intersection it is painting — e.g. a condition
/// <c>Value &lt; 0</c> turns the negative cells red — which the static cell style cannot express because a
/// matrix aggregate has no per-row data context.
/// </summary>
/// <remarks>
/// Everything except the <c>Value</c> identifier delegates to the wrapped context, so a condition may still
/// mix in <c>Parameters.Meta</c> or paging globals. Both the SSRS-style bare <c>Value</c> and the qualified
/// <c>Fields.Value</c> route through <see cref="TryResolveUnqualifiedField"/> (see <c>ExpressionEvaluator</c>),
/// so overriding that one method covers both spellings.
/// </remarks>
internal sealed class ValueScopedContext : IReportExpressionContext
{
    private const string ValueField = "Value";
    private readonly IReportExpressionContext _inner;
    private readonly object? _value;

    public ValueScopedContext(IReportExpressionContext inner, object? value)
    {
        _inner = inner;
        _value = value;
    }

    public IValueLookup Fields => _inner.Fields;
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

    public object? EvaluateLookup(object? source, string destExpression, string resultExpression, string datasetName, bool all)
        => _inner.EvaluateLookup(source, destExpression, resultExpression, datasetName, all);

    public object? EvaluatePositional(string function, string expression, AggregateScope scope)
        => _inner.EvaluatePositional(function, expression, scope);

    public IValueLookup? GetSource(string sourceName) => _inner.GetSource(sourceName);

    public bool TryResolveUnqualifiedField(string fieldName, out object? value)
    {
        if (string.Equals(fieldName, ValueField, StringComparison.OrdinalIgnoreCase))
        {
            value = _value;
            return true;
        }
        return _inner.TryResolveUnqualifiedField(fieldName, out value);
    }

    public object? GetReportItem(string name) => _inner.GetReportItem(name);
    public void SetReportItem(string name, object? value) => _inner.SetReportItem(name, value);
}
