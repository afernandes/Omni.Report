using System.Collections.Concurrent;
using System.Globalization;
using NCalc;
using NCalc.Factories;

namespace Reporting.Expressions;

/// <summary>Thrown when an expression fails to parse.</summary>
public sealed class ExpressionParseException : Exception
{
    /// <summary>Wraps the underlying parser failure, keeping the offending text for the message.</summary>
    /// <param name="expression">The expression that failed to parse.</param>
    /// <param name="inner">The parser exception.</param>
    public ExpressionParseException(string expression, Exception inner)
        : base($"Failed to parse expression: {expression}", inner)
        => Expression = expression;

    /// <summary>The expression text that failed. Exposed so a caller can point the user at the right
    /// field instead of surfacing a parser message with no context.</summary>
    public string Expression { get; }
}
