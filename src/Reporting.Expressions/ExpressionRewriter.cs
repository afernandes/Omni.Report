using System.Text.RegularExpressions;

namespace Reporting.Expressions;

/// <summary>
/// Rewrites report-friendly dotted identifiers (<c>Fields.Total</c>) into NCalc's
/// bracket-quoted parameter form (<c>[Fields.Total]</c>) so they survive tokenization.
/// </summary>
/// <remarks>
/// NCalc treats <c>.</c> as a member-access operator that it does not currently support,
/// so dotted names must be wrapped. Identifiers already inside brackets are left untouched,
/// and string literals are preserved.
/// </remarks>
internal static partial class ExpressionRewriter
{
    private static readonly string[] Scopes = ["Fields", "Parameters", "Variables", "Page", "ReportItems"];

    [GeneratedRegex(
        @"(?<str>'(?:[^'\\]|\\.)*')|(?<br>\[[^\]]*\])|(?<code>\bCode\.(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\()|(?<dot>\b(Fields|Parameters|Variables|Page|ReportItems)\.([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*))",
        RegexOptions.Compiled)]
    private static partial Regex Tokenizer();

    /// <summary>Rewrites <c>Fields.X</c> → <c>[Fields.X]</c> and <c>Code.Method(</c> →
    /// <c>Code_Method(</c> outside of string literals and brackets. The <c>Code_</c> flattening
    /// lets NCalc route the RDL <c>Code.Method(...)</c> call as a function (the dotted form is
    /// member access NCalc can't dispatch); <see cref="ExpressionEvaluator"/> intercepts it.</summary>
    public static string Rewrite(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Tokenizer().Replace(expression, m =>
        {
            if (m.Groups["str"].Success || m.Groups["br"].Success)
            {
                return m.Value;
            }
            if (m.Groups["code"].Success)
            {
                return "Code_" + m.Groups["method"].Value + "(";
            }
            return "[" + m.Value + "]";
        });
    }

    /// <summary>Indicates whether the parameter name produced by <see cref="Rewrite"/> is a scoped lookup.</summary>
    public static bool TrySplitScope(string parameter, out string scope, out string member)
    {
        var dot = parameter.IndexOf('.');
        if (dot > 0)
        {
            scope = parameter[..dot];
            member = parameter[(dot + 1)..];
            if (Array.IndexOf(Scopes, scope) >= 0)
            {
                return true;
            }
        }
        scope = string.Empty;
        member = string.Empty;
        return false;
    }
}
