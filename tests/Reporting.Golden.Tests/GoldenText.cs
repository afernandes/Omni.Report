using System.Text;

namespace Reporting.Golden.Tests;

/// <summary>
/// Escaping shared by the two golden formats.
/// </summary>
/// <remarks>
/// Both <see cref="DisplayList"/> and <see cref="SvgShape"/> wrap report text in double quotes, and
/// both are line-oriented — one primitive (or one element) per line. So a value that itself contains
/// a quote or a newline would break the structure: the golden would become ambiguous to read and
/// noisy to diff, and a real change could hide inside the malformed line. Report text is author
/// data, not a controlled vocabulary, so this has to be handled rather than assumed away.
/// </remarks>
internal static class GoldenText
{
    /// <summary>Renders <paramref name="value"/> as a quoted, single-line, unambiguous token.</summary>
    public static string Quote(string value) => "\"" + Escape(value) + "\"";

    /// <summary>
    /// Escapes the characters that would otherwise break the format. The backslash goes first: doing
    /// it later would also escape the backslashes introduced by the other rules, so <c>a"b</c> would
    /// come out as <c>a\\"b</c>.
    /// </summary>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
