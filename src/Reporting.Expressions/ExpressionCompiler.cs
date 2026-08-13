using System.Collections.Concurrent;
using System.Globalization;
using NCalc;
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
    /// <see cref="DefaultOptions"/> as the NCalc 7 configuration object.
    /// </summary>
    /// <remarks>
    /// <para>NCalc 7 split options in two: <c>Parsing</c> decides how the text becomes an AST, and
    /// <c>Evaluation</c> how that AST is executed. The implicit conversion from
    /// <see cref="ExpressionOptions"/> fills both — <c>DecimalAsDefault</c> lands on
    /// <c>Parsing.FloatingPointNumberType</c> <em>and</em> <c>Evaluation.Math.FloatingPointNumberType</c>.</para>
    ///
    /// <para>The parse half is the one that matters for literals, and it is easy to lose: a numeric
    /// literal becomes a <c>double</c> or a <c>decimal</c> node <b>when it is parsed</b>, and no
    /// evaluation option undoes that afterwards. Passing these options to
    /// <see cref="LogicalExpressionFactory"/> is therefore not optional — see <see cref="Compile"/>.</para>
    ///
    /// <para>Built once and shared: the object is effectively immutable and every compiled expression
    /// uses the same one.</para>
    /// </remarks>
    private static readonly ExpressionConfiguration Configuration = DefaultOptions;

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
                // As opções de PARSE têm de ser passadas aqui, e não só as de avaliação lá embaixo: é no
                // parse que um literal numérico vira nó double ou decimal, e nenhuma opção de avaliação
                // desfaz isso depois. Passar null aqui — que parece inofensivo, já que a versão anterior
                // passava ExpressionOptions.None — coloca toda a aritmética do relatório de volta em
                // ponto flutuante binário, com 0.1 + 0.2 dando 0.30000000000000004.
                // A cultura é invariante porque o texto da expressão é do AUTOR do relatório, não do
                // usuário final: "1.5" tem de significar um e meio em qualquer máquina.
                return LogicalExpressionFactory.Create(
                    text, Configuration.Parsing, CultureInfo.InvariantCulture, CancellationToken.None);
            }
            catch (Exception ex)
            {
                throw new ExpressionParseException(text, ex);
            }
        });
        // A metade de AVALIAÇÃO da mesma configuração. As duas precisam vir do mesmo objeto: parsear com
        // um conjunto e avaliar com outro é como o decimal se perde sem ninguém notar.
        return new Expression(ast, Configuration, null, CultureInfo.InvariantCulture);
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
