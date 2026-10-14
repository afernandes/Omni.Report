using System.Text.RegularExpressions;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Helpers to expand <c>{secret:NAME}</c> placeholders in a connection string
/// (or any string) using an <see cref="ISecretResolver"/>.</summary>
public static class SecretTemplate
{
    private static readonly Regex Placeholder = new(
        @"\{secret:(?<name>[A-Za-z_][A-Za-z0-9_]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Returns true if <paramref name="value"/> contains at least one secret placeholder.</summary>
    public static bool ContainsPlaceholder(string? value)
        => !string.IsNullOrEmpty(value) && Placeholder.IsMatch(value);

    /// <summary>Replaces every <c>{secret:NAME}</c> occurrence with the resolved value from
    /// <paramref name="resolver"/>. Missing secrets leave the placeholder intact and append
    /// the name to <paramref name="missing"/> so the caller can decide what to do.</summary>
    public static async Task<string> ExpandAsync(
        string template,
        ISecretResolver resolver,
        List<string>? missing = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        if (string.IsNullOrEmpty(template)) return template;

        // Materialize match list because we need to await per match — Regex.Replace's
        // delegate overload is synchronous.
        var matches = Placeholder.Matches(template);
        if (matches.Count == 0) return template;

        var sb = new System.Text.StringBuilder(template.Length);
        int cursor = 0;
        foreach (Match m in matches)
        {
            sb.Append(template, cursor, m.Index - cursor);
            var name = m.Groups["name"].Value;
            var value = await resolver.ResolveAsync(name, cancellationToken).ConfigureAwait(false);
            if (value is null)
            {
                missing?.Add(name);
                sb.Append(m.Value); // leave the placeholder verbatim
            }
            else
            {
                sb.Append(value);
            }
            cursor = m.Index + m.Length;
        }
        sb.Append(template, cursor, template.Length - cursor);
        return sb.ToString();
    }

    /// <summary>Synchronous expansion using <see cref="Environment.GetEnvironmentVariable(string)"/>
    /// — convenient for callers that don't have async context (constructors, ctor-only
    /// connection-string builders). Missing variables leave the placeholder verbatim.</summary>
    public static string ExpandFromEnvironment(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return Placeholder.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            return Environment.GetEnvironmentVariable(name) ?? m.Value;
        });
    }
}
