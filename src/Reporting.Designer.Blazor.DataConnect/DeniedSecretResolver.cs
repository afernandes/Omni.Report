using Reporting.Designer.Blazor.ViewModels;

namespace Reporting.Designer.Blazor.DataConnect;

internal sealed class DeniedSecretResolver : ISecretResolver
{
    public Task<string?> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("O host deve autorizar a resolução de segredos para esta conexão.");
    }
}
