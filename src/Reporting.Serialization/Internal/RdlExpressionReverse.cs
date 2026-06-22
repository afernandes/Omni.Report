using System.Text;
using System.Text.RegularExpressions;

namespace Reporting.Serialization.Internal;

/// <summary>
/// The inverse of <see cref="RdlExpression"/>: translates an OmniReport expression back into an RDL/SSRS
/// expression (the form SSRS and Report Builder understand). OmniReport uses dotted paths
/// (<c>Fields.Name</c>, <c>Parameters.P</c>, <c>PageNumber</c>) and function forms (<c>Concat(a, b)</c>,
/// <c>Like(v, p)</c>); RDL uses VB member access (<c>Fields!Name.Value</c>) prefixed with <c>=</c>, the
/// <c>&amp;</c> concatenation operator and the <c>Like</c> infix operator. Used by the RDL exporter so a
/// report can round-trip <c>.rdl → import → edit → export → .rdl</c>.
/// </summary>
internal static partial class RdlExpressionReverse
{
    [GeneratedRegex(@"\bFields\.([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex FieldDot();

    [GeneratedRegex(@"\bParameters\.([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex ParameterDot();

    [GeneratedRegex(@"\bReportItems\.([A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex ReportItemDot();

    // Bare globals — only when NOT preceded by '.'/'!'/word char (so a field literally named "PageNumber",
    // already rewritten to Fields!PageNumber.Value, is left alone).
    [GeneratedRegex(@"(?<![.!A-Za-z0-9_])(PageNumber|TotalPages|ReportName|Now)\b")]
    private static partial Regex BareGlobal();

    [GeneratedRegex(@"(?<![.!A-Za-z0-9_])UserName\b")]
    private static partial Regex BareUser();

    /// <summary>Converts an OmniReport expression body to a full RDL expression (<c>=…</c>). An empty input
    /// returns empty. The result is the SSRS form a Report Builder / SSRS would author.</summary>
    public static string ToRdl(string? omni)
    {
        if (string.IsNullOrEmpty(omni))
        {
            return string.Empty;
        }
        // 1. Unwrap the function forms first (Concat → '&', Like → infix), recursively & quote/paren-aware.
        var body = UnwrapFunctions(omni);
        // 2. Map dotted collection refs back to VB member access.
        body = FieldDot().Replace(body, "Fields!$1.Value");
        body = ParameterDot().Replace(body, "Parameters!$1.Value");
        body = ReportItemDot().Replace(body, "ReportItems!$1.Value");
        body = BareGlobal().Replace(body, m => "Globals!" + (m.Value == "Now" ? "ExecutionTime" : m.Value));
        body = BareUser().Replace(body, "User!UserID");
        return "=" + body;
    }

    // Rewrites Concat(a, b, …) → a & b & … and Like(v, p) → (v Like p), recursing into every function's
    // arguments. Other functions (IIf, Switch, …) are preserved with their args recursed. String literals
    // are copied verbatim. Like is always parenthesised (it binds loosest in VB), so nesting stays correct.
    private static string UnwrapFunctions(string s)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c is '\'' or '"')
            {
                i = CopyStringLiteral(s, i, sb);
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                int idStart = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                {
                    i++;
                }
                string id = s[idStart..i];
                int j = i;
                while (j < s.Length && char.IsWhiteSpace(s[j]))
                {
                    j++;
                }
                if (j < s.Length && s[j] == '(')
                {
                    int close = RdlExpression.MatchingParen(s, j);
                    var inner = s[(j + 1)..close];
                    var rawArgs = RdlExpression.SplitTopLevel(inner, ',').Select(a => a.Trim()).ToList();
                    var args = rawArgs.Select(UnwrapFunctions).ToList();
                    if (id.Equals("Concat", StringComparison.OrdinalIgnoreCase) && args.Count >= 1)
                    {
                        // '&' binds TIGHTER than Like in VB, so a Like operand must be parenthesised to keep
                        // its grouping (a & (b Like c)); other operands need no parens. A top-level Like
                        // expression — and a Like passed as a comma-separated function arg — needs none.
                        var pieces = args.Select((u, k) => IsTopLevelLikeCall(rawArgs[k]) ? $"({u})" : u);
                        sb.Append(string.Join(" & ", pieces));
                    }
                    else if (id.Equals("Like", StringComparison.OrdinalIgnoreCase) && args.Count == 2)
                    {
                        sb.Append(args[0]).Append(" Like ").Append(args[1]);
                    }
                    else
                    {
                        sb.Append(id).Append('(').Append(string.Join(", ", args)).Append(')');
                    }
                    i = close + 1;
                    continue;
                }
                sb.Append(id); // bare identifier, not a call
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    // True when raw is exactly a single Like(...) call spanning the whole (trimmed) string — so unwrapping it
    // yields a top-level Like infix whose low precedence must be guarded when it becomes a '&' operand.
    private static bool IsTopLevelLikeCall(string raw)
    {
        raw = raw.Trim();
        if (!raw.StartsWith("Like(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        int open = raw.IndexOf('(');
        return RdlExpression.MatchingParen(raw, open) == raw.Length - 1;
    }

    // Appends the string literal starting at index i (a ' or " quote) verbatim, honouring the VB "" escape,
    // and returns the index just past the closing quote.
    private static int CopyStringLiteral(string s, int i, StringBuilder sb)
    {
        char quote = s[i];
        sb.Append(quote);
        i++;
        while (i < s.Length)
        {
            char c = s[i];
            sb.Append(c);
            if (c == quote)
            {
                if (i + 1 < s.Length && s[i + 1] == quote) // doubled quote = literal quote, stay in-string
                {
                    sb.Append(s[++i]);
                }
                else
                {
                    return i + 1;
                }
            }
            i++;
        }
        return i;
    }
}
