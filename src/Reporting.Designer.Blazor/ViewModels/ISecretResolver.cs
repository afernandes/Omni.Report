using System.Text.RegularExpressions;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// Resolves <c>{secret:NAME}</c> placeholders embedded in connection strings (and other
/// sensitive config) at runtime — so the <c>.repx</c> file never carries plaintext
/// passwords. Hosts register an implementation backed by environment variables, Azure
/// Key Vault, AWS Secrets Manager, HashiCorp Vault, .NET user-secrets, or any custom store.
/// </summary>
/// <remarks>
/// <para>Convention: placeholder syntax is <c>{secret:NAME}</c>. Names are case-sensitive
/// to the underlying store; case-insensitive lookups should normalize inside the resolver.</para>
///
/// <para>Example connection string in <c>.repx</c>:</para>
/// <code>Host=db.internal;Username=app;Password={secret:OMNIREPORT_DB_PASSWORD};Database=erp</code>
///
/// <para>The default <see cref="EnvironmentSecretResolver"/> resolves to environment variables
/// — fine for containers / 12-factor apps. Production deployments should swap to a vault-backed
/// implementation.</para>
/// </remarks>
public interface ISecretResolver
{
    /// <summary>Resolves a secret by name. Returns <c>null</c> when the secret is unknown —
    /// callers may fall back, warn, or fail closed depending on policy.</summary>
    Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default);
}

/// <summary>Default resolver — reads from environment variables (or <c>null</c> if missing).
/// Suitable for containerized deployments, 12-factor apps, local dev. For production secret
/// stores (Key Vault, Vault, Secrets Manager), implement <see cref="ISecretResolver"/>
/// directly.</summary>
public sealed class EnvironmentSecretResolver : ISecretResolver
{
    public Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName)) return Task.FromResult<string?>(null);
        return Task.FromResult(Environment.GetEnvironmentVariable(secretName));
    }
}

/// <summary>Resolver that always returns the value as-is (no substitution). Used when no
/// resolver is registered — placeholders survive verbatim, useful in design-time previews
/// where the user wants to see the raw template.</summary>
internal sealed class NullSecretResolver : ISecretResolver
{
    public static readonly NullSecretResolver Instance = new();
    public Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}

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
