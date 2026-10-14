using System.Text.RegularExpressions;

namespace Reporting.Designer.Blazor.ViewModels;

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
