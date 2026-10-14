using System.Text.RegularExpressions;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Resolver that always returns the value as-is (no substitution). Used when no
/// resolver is registered — placeholders survive verbatim, useful in design-time previews
/// where the user wants to see the raw template.</summary>
internal sealed class NullSecretResolver : ISecretResolver
{
    public static readonly NullSecretResolver Instance = new();
    public Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
}
