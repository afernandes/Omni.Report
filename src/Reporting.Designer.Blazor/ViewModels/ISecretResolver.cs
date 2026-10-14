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
