using System.Text.RegularExpressions;

namespace Reporting.Serialization.Internal;

/// <summary>
/// Translates SSRS/RDL expression fragments into OmniReport expression syntax. RDL expressions start with
/// <c>=</c> and use VB-style member access (<c>Fields!Name.Value</c>, <c>Parameters!P.Value</c>,
/// <c>Globals!PageNumber</c>); OmniReport uses dotted paths (<c>Fields.Name</c>, <c>Parameters.P</c>,
/// <c>PageNumber</c>). A value without a leading <c>=</c> is literal text.
/// </summary>
internal static partial class RdlExpression
{
    // Match only the `.Value` member, not any `.Member` — otherwise `Parameters!P.Count` / `.Label`
    // would be silently rewritten dropping the member. Other members are left intact (and will surface
    // as a visible expression error rather than wrong data).
    [GeneratedRegex(@"Fields!([A-Za-z_][A-Za-z0-9_]*)\.Value")]
    private static partial Regex FieldRef();

    [GeneratedRegex(@"Parameters!([A-Za-z_][A-Za-z0-9_]*)\.Value")]
    private static partial Regex ParameterRef();

    [GeneratedRegex(@"Globals!(\w+)")]
    private static partial Regex GlobalRef();

    // ReportItems!Name.Value → ReportItems.Name (the value another named text box rendered to). Only the
    // .Value member is rewritten, mirroring FieldRef — other members surface as a visible error.
    [GeneratedRegex(@"ReportItems!([A-Za-z_][A-Za-z0-9_]*)\.Value")]
    private static partial Regex ReportItemRef();

    [GeneratedRegex(@"User!(\w+)")]
    private static partial Regex UserRef();

    /// <summary>True when the raw RDL value is an expression (starts with <c>=</c>).</summary>
    public static bool IsExpression(string? raw) => raw is { Length: > 0 } && raw[0] == '=';

    /// <summary>Converts an RDL expression body (the part after <c>=</c>, or any string) to OmniReport
    /// syntax. Non-expression input is returned unchanged. Field/parameter/global references are mapped,
    /// the VB <c>&amp;</c> concatenation operator is rewritten to <c>Concat(...)</c>, and the VB
    /// <c>Like</c> infix operator to the <c>Like(value, pattern)</c> function — both including inside
    /// function arguments and honouring VB precedence (<c>&amp;</c> binds tighter than <c>Like</c>, so
    /// <c>a &amp; b Like "X*"</c> → <c>Like(Concat(a, b), "X*")</c>). The underlying runtime
    /// <c>Like()</c> supports the VB wildcards <c>* ? #</c> (character classes <c>[...]</c> are not).</summary>
    public static string Convert(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }
        if (!IsExpression(raw))
        {
            return raw;
        }
        var body = raw[1..];
        body = FieldRef().Replace(body, m => $"Fields.{m.Groups[1].Value}");
        body = ParameterRef().Replace(body, m => $"Parameters.{m.Groups[1].Value}");
        body = ReportItemRef().Replace(body, m => $"ReportItems.{m.Groups[1].Value}");
        body = GlobalRef().Replace(body, m => m.Groups[1].Value switch
        {
            "PageNumber" or "OverallPageNumber" => "PageNumber",
            "TotalPages" or "OverallTotalPages" => "TotalPages",
            "ExecutionTime" => "Now",
            var other => other, // ReportName, etc. — left as a bare identifier
        });
        body = UserRef().Replace(body, m => m.Groups[1].Value is "UserID" ? "UserName" : m.Groups[1].Value);
        return ConvertLike(body.Trim());
    }

    // VB 'a Like pattern' has no infix operator in the engine, so rewrite it to the Like(a, pattern) runtime
    // function. Like binds LOOSER than '&' (VB precedence), so it splits first (outermost) and each operand
    // is then '&'-folded by ConvertConcat — giving a & b Like "X*" → Like(Concat(a, b), "X*"). The keyword
    // is matched whole-word and outside strings/parens, so "Likelihood", a quoted "x Like y", and an
    // existing Like(...) function call are all left alone. Nested Like (in function args) is handled because
    // ConvertParenGroups recurses through here.
    private static string ConvertLike(string body)
    {
        var parts = SplitTopLevelLike(body);
        if (parts.Count <= 1)
        {
            return ConvertConcat(body);
        }
        var result = ConvertConcat(parts[0]).Trim();
        for (int i = 1; i < parts.Count; i++) // left-associative fold (chained Like is rare but consistent)
        {
            result = $"Like({result}, {ConvertConcat(parts[i]).Trim()})";
        }
        return result;
    }

    // VB '&' string concatenation has no operator in the engine, so rewrite a & b & c into the Concat(...)
    // runtime function. Recurses into parenthesized groups first so '&' inside function arguments
    // (e.g. IIf(c, a & b, x)) and sub-expressions are also folded; then folds depth-0 '&' outside string
    // literals. Quoted '&' is preserved. (VB uses And/AndAlso for logic, never '&', so '&' is concat.)
    private static string ConvertConcat(string body)
    {
        var withGroups = ConvertParenGroups(body);
        var parts = SplitTopLevel(withGroups, '&');
        if (parts.Count <= 1)
        {
            return withGroups;
        }
        return "Concat(" + string.Join(", ", parts.Select(p => p.Trim())) + ")";
    }

    // Rewrites the inside of every parenthesized group: splits its content on top-level commas (argument
    // boundaries) and runs ConvertConcat on each argument, so '&' nested in function calls is folded too.
    private static string ConvertParenGroups(string s)
    {
        var sb = new System.Text.StringBuilder();
        bool inString = false;
        char quote = '\0';
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                sb.Append(c);
                if (c == quote) { inString = false; }
                continue;
            }
            if (c is '\'' or '"')
            {
                inString = true;
                quote = c;
                sb.Append(c);
                continue;
            }
            if (c == '(')
            {
                int close = MatchingParen(s, i);
                var inner = s[(i + 1)..close];
                sb.Append('(');
                sb.Append(string.Join(", ", SplitTopLevel(inner, ',').Select(a => ConvertLike(a.Trim()))));
                sb.Append(')');
                i = close;
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    // Index of the ')' matching the '(' at openIndex (or end-of-string if unbalanced), quote-aware.
    private static int MatchingParen(string s, int openIndex)
    {
        int depth = 0;
        bool inString = false;
        char quote = '\0';
        for (int j = openIndex; j < s.Length; j++)
        {
            char c = s[j];
            if (inString)
            {
                if (c == quote) { inString = false; }
                continue;
            }
            switch (c)
            {
                case '\'' or '"': inString = true; quote = c; break;
                case '(': depth++; break;
                case ')': depth--; if (depth == 0) { return j; } break;
            }
        }
        return s.Length - 1;
    }

    private static List<string> SplitTopLevel(string s, char separator)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        bool inString = false;
        char quote = '\0';
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (c == quote) { inString = false; }
                continue;
            }
            switch (c)
            {
                case '\'' or '"': inString = true; quote = c; break;
                case '(': depth++; break;
                case ')': depth--; break;
                default:
                    if (c == separator && depth == 0)
                    {
                        parts.Add(s[start..i]);
                        start = i + 1;
                    }
                    break;
            }
        }
        parts.Add(s[start..]);
        return parts;
    }

    // Splits on the VB 'Like' keyword at paren-depth 0, outside string literals. The keyword is matched
    // whole-word (whitespace on both sides), so it never fires on "Likelihood", a quoted "x Like y", or an
    // existing Like(...) function call (followed by '(' not whitespace).
    private static List<string> SplitTopLevelLike(string s)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        bool inString = false;
        char quote = '\0';
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inString)
            {
                if (c == quote) { inString = false; }
                continue;
            }
            switch (c)
            {
                case '\'' or '"': inString = true; quote = c; break;
                case '(': depth++; break;
                case ')': depth--; break;
                default:
                    if (depth == 0 && IsLikeKeywordAt(s, i))
                    {
                        parts.Add(s[start..i]);
                        start = i + 4;
                        i += 3; // skip the keyword (loop ++ lands just past it)
                    }
                    break;
            }
        }
        parts.Add(s[start..]);
        return parts;
    }

    // True when "Like" begins a whole-word keyword at index i: preceded and followed by whitespace
    // (a leading or trailing/parenthesised "Like" is not a binary infix operator).
    private static bool IsLikeKeywordAt(string s, int i)
    {
        if (i == 0 || i + 4 > s.Length || !char.IsWhiteSpace(s[i - 1]))
        {
            return false;
        }
        if (string.Compare(s, i, "Like", 0, 4, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }
        return i + 4 >= s.Length || char.IsWhiteSpace(s[i + 4]);
    }
}
