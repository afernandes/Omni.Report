using System.Globalization;
using NCalc;
using NCalc.Extensions;
using NCalc.Handlers;
using Reporting.Aggregates;

namespace Reporting.Expressions;

/// <summary>Thrown when an expression parses but fails while being evaluated — a missing field, a bad cast,
/// a division by zero.</summary>
public sealed class ExpressionEvaluationException : Exception
{
    /// <summary>Wraps the underlying failure, keeping the offending text for the message.</summary>
    /// <param name="expression">The expression that failed to evaluate.</param>
    /// <param name="inner">The original exception.</param>
    public ExpressionEvaluationException(string expression, Exception inner)
        : base($"Failed to evaluate expression: {expression}", inner)
        => Expression = expression;

    /// <summary>The expression text that failed, so the error can name the field it came from.</summary>
    public string Expression { get; }
}
