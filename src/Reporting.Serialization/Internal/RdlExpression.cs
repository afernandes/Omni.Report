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

    [GeneratedRegex(@"User!(\w+)")]
    private static partial Regex UserRef();

    /// <summary>True when the raw RDL value is an expression (starts with <c>=</c>).</summary>
    public static bool IsExpression(string? raw) => raw is { Length: > 0 } && raw[0] == '=';

    /// <summary>Converts an RDL expression body (the part after <c>=</c>, or any string) to OmniReport
    /// syntax. Non-expression input is returned unchanged. Field/parameter/global references are mapped;
    /// other VB constructs (e.g. the <c>&amp;</c> concatenation operator) are left as-is — a documented
    /// limitation, since the common case is a textbox bound to a single field.</summary>
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
        body = GlobalRef().Replace(body, m => m.Groups[1].Value switch
        {
            "PageNumber" => "PageNumber",
            "TotalPages" or "OverallTotalPages" => "TotalPages",
            "ExecutionTime" => "Now",
            var other => other, // ReportName, etc. — left as a bare identifier
        });
        body = UserRef().Replace(body, m => m.Groups[1].Value is "UserID" ? "UserName" : m.Groups[1].Value);
        return ConvertConcat(body.Trim());
    }

    // VB '&' string concatenation has no operator in the engine, so rewrite top-level a & b & c into the
    // Concat(a, b, c) runtime function. Splits only at depth-0 '&' outside string literals, so nested
    // parens and quoted '&' are preserved. (VB uses And/AndAlso for logic, never '&', so '&' is concat.)
    private static string ConvertConcat(string body)
    {
        var parts = SplitTopLevel(body, '&');
        if (parts.Count <= 1)
        {
            return body;
        }
        return "Concat(" + string.Join(", ", parts.Select(p => p.Trim())) + ")";
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
}
