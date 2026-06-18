using System.Collections.Concurrent;
using System.Globalization;
using NCalc;
using NCalc.Domain;
using NCalc.Factories;

namespace Reporting.Expressions;

/// <summary>
/// Compiles (parses) NCalc expressions once and caches the AST for re-use across
/// every row evaluation. Cuts the per-row cost from <em>parse + evaluate</em> to
/// just <em>evaluate</em>.
/// </summary>
public sealed class ExpressionCompiler
{
    private readonly ConcurrentDictionary<string, LogicalExpression> _cache = new(StringComparer.Ordinal);

    /// <summary>The default <see cref="ExpressionOptions"/> used by compiled expressions:
    /// case-insensitive identifiers, decimals preferred over double for money math.</summary>
    /// <remarks>
    /// <c>StringConcat</c> is intentionally <em>not</em> set — it routes every <c>+</c> through
    /// string concatenation, which silently corrupts numeric arithmetic. Users that want to mix
    /// strings and values should use template syntax (<c>"Total: {expr:format}"</c>) instead.
    /// </remarks>
    public static ExpressionOptions DefaultOptions
        => ExpressionOptions.IgnoreCaseAtBuiltInFunctions
         | ExpressionOptions.DecimalAsDefault;

    /// <summary>
    /// Returns a fresh <see cref="Expression"/> instance bound to a cached AST. The caller is
    /// responsible for wiring <c>EvaluateParameter</c> and <c>EvaluateFunction</c> before evaluating.
    /// </summary>
    public Expression Compile(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var rewritten = ExpressionRewriter.Rewrite(expression);
        var ast = _cache.GetOrAdd(rewritten, static text =>
        {
            try
            {
                return LogicalExpressionFactory.Create(text, ExpressionOptions.None, CancellationToken.None);
            }
            catch (Exception ex)
            {
                throw new ExpressionParseException(text, ex);
            }
        });
        return new Expression(ast, DefaultOptions, CultureInfo.InvariantCulture);
    }

    /// <summary>Removes the cached AST for the given expression text (used by hot-reload scenarios).</summary>
    public void Invalidate(string expression)
        => _cache.TryRemove(ExpressionRewriter.Rewrite(expression), out _);

    /// <summary>Empties the cache. Primarily for tests.</summary>
    public void Clear() => _cache.Clear();
}

/// <summary>Thrown when an expression fails to parse.</summary>
public sealed class ExpressionParseException : Exception
{
    public ExpressionParseException(string expression, Exception inner)
        : base($"Failed to parse expression: {expression}", inner)
        => Expression = expression;

    public string Expression { get; }
}
