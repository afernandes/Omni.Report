using System.Globalization;
using System.Text;

namespace Reporting.Expressions;

/// <summary>
/// Renders interpolation templates of the form <c>"Total: {Fields.Total:C} (página {PageNumber})"</c>.
/// Each <c>{ ... }</c> contains an expression and an optional <c>:format</c> spec, separated by a colon.
/// Literal braces are escaped as <c>{{</c> and <c>}}</c>.
/// </summary>
public sealed class TemplateRenderer
{
    private readonly ExpressionEvaluator _evaluator;

    public TemplateRenderer(ExpressionEvaluator? evaluator = null)
        => _evaluator = evaluator ?? new ExpressionEvaluator();

    /// <summary>Renders the template against <paramref name="context"/>.</summary>
    public string Render(string template, IReportExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);

        var sb = new StringBuilder(template.Length);
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    sb.Append('{');
                    i += 2;
                    continue;
                }
                int end = FindClosing(template, i + 1);
                if (end < 0)
                {
                    throw new FormatException($"Unterminated template placeholder at index {i}.");
                }
                var body = template.AsSpan(i + 1, end - (i + 1));
                AppendEvaluated(sb, body, context);
                i = end + 1;
                continue;
            }
            if (c == '}')
            {
                if (i + 1 < template.Length && template[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                    continue;
                }
                throw new FormatException($"Unexpected '}}' at index {i}. Use '}}}}' to emit a literal brace.");
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>If the template is a SINGLE placeholder spanning the whole (trimmed) string with no inline
    /// <c>:format</c> — e.g. <c>"{Fields.preco}"</c> — returns true and yields the inner expression. This
    /// lets a caller evaluate it to a typed value and apply the ELEMENT's own Format property (SSRS-style:
    /// a textbox bound to a single value is formatted by its Format property). Returns false for mixed
    /// templates, multiple placeholders, or a placeholder that already carries an inline format (which wins).</summary>
    public static bool TryGetSingleExpression(string template, out string expression)
    {
        ArgumentNullException.ThrowIfNull(template);
        expression = string.Empty;
        var trimmed = template.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[1] == '{' || trimmed[^1] != '}')
        {
            return false;
        }
        var end = FindClosing(trimmed, 1);
        if (end != trimmed.Length - 1) // the lone placeholder must close exactly at the end
        {
            return false;
        }
        var body = trimmed.AsSpan(1, end - 1);
        if (FindFormatSeparator(body) >= 0) // an inline :format is present → it takes precedence
        {
            return false;
        }
        expression = body.Trim().ToString();
        return expression.Length > 0;
    }

    /// <summary>Returns <c>true</c> if the input contains at least one placeholder.</summary>
    public static bool HasPlaceholders(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        for (int i = 0; i < template.Length - 1; i++)
        {
            if (template[i] == '{' && template[i + 1] != '{')
            {
                return true;
            }
        }
        return false;
    }

    private void AppendEvaluated(StringBuilder sb, ReadOnlySpan<char> body, IReportExpressionContext context)
    {
        var colon = FindFormatSeparator(body);
        string expression;
        string? format;
        if (colon >= 0)
        {
            expression = body[..colon].ToString();
            format = body[(colon + 1)..].ToString();
        }
        else
        {
            expression = body.ToString();
            format = null;
        }
        var value = _evaluator.Evaluate(expression, context);
        sb.Append(ValueFormatter.Format(value, format, context.Culture));
    }

    private static int FindClosing(string template, int start)
    {
        int depth = 1;
        for (int i = start; i < template.Length; i++)
        {
            char c = template[i];
            if (c == '\'' || c == '"')
            {
                // skip string literals inside the expression
                int end = template.IndexOf(c, i + 1);
                if (end < 0)
                {
                    return -1;
                }
                i = end;
                continue;
            }
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static int FindFormatSeparator(ReadOnlySpan<char> body)
    {
        int depth = 0;
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '(' || c == '[')
            {
                depth++;
            }
            else if (c == ')' || c == ']')
            {
                depth--;
            }
            else if (c == '\'' || c == '"')
            {
                int end = body[(i + 1)..].IndexOf(c);
                if (end < 0)
                {
                    return -1;
                }
                i += end + 1;
            }
            else if (c == ':' && depth == 0)
            {
                return i;
            }
        }
        return -1;
    }
}
