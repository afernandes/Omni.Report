using Reporting.Elements;

namespace Reporting.Expressions.Roslyn;

/// <summary>
/// Entry point for the opt-in Roslyn code feature. Produces a resolver wired into
/// <see cref="ExpressionEvaluator.CodeFunctionResolver"/> (or
/// <c>PaginationRequest.CodeFunctionResolver</c>) so report expressions can call
/// <c>Code.MethodName(...)</c>.
/// </summary>
/// <remarks>
/// <b>Security:</b> enabling this compiles and runs arbitrary C# embedded in the report. Only use
/// it with report definitions you trust. See <see cref="RoslynCodeEvaluator"/>.
/// </remarks>
public static class RoslynCode
{
    /// <summary>Compiles a single C# code block (helper method declarations) and returns a
    /// resolver. The methods become callable as <c>Code.MethodName(...)</c> in expressions.</summary>
    public static Func<string, object?[], object?> CreateResolver(string codeSource)
        => new RoslynCodeEvaluator(codeSource).Invoke;

    /// <summary>Builds a resolver from the report's <see cref="CodeElement"/>s (concatenating their
    /// sources), or <c>null</c> when the report declares none — so a host can opt in generically:
    /// <c>req.CodeFunctionResolver = RoslynCode.CreateResolver(def.CollectCodeElements())</c>.</summary>
    public static Func<string, object?[], object?>? CreateResolver(IEnumerable<CodeElement> codeElements)
    {
        ArgumentNullException.ThrowIfNull(codeElements);
        var sources = codeElements
            .Select(c => c.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        return sources.Count == 0 ? null : new RoslynCodeEvaluator(string.Join("\n", sources)).Invoke;
    }
}
