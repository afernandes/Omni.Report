using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;
namespace Reporting.Designer.Blazor.DataConnect.Tests;

public sealed class SecretBoundaryTests
{
    [Fact]
    public async Task TestConnectionAsync_SegredoAmbienteNaoAutorizado_NaoExpandeNemExpoeValor()
    {
        string name = "OMNIREPORT_AUDIT_" + Guid.NewGuid().ToString("N");
        string value = "segredo-sentinela-nao-expor";
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            var result = await new DesignerDataConnect().TestConnectionAsync(DataConnectionKind.Sqlite, "Data Source={secret:" + name + "}");
            result.Success.Should().BeFalse();
            result.Message.Should().NotContain(value).And.NotContain(name);
        }
        finally { Environment.SetEnvironmentVariable(name, null); }
    }
}
