namespace Reporting.Expressions;

/// <summary>Optional expression-context support for explicitly named, currently open groups.</summary>
public interface INamedGroupExpressionContext
{
    /// <summary>Evaluates an aggregate in the named open group. Unknown names are rejected.</summary>
    object? EvaluateGroupAggregate(string function, string expression, string groupName);

    /// <summary>Evaluates a positional function in the named open group. Unknown names are rejected.</summary>
    object? EvaluateGroupPositional(string function, string expression, string groupName);
}
