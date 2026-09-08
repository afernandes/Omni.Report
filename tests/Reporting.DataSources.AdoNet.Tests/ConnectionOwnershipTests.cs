using FluentAssertions;
using Xunit;
namespace Reporting.DataSources.AdoNet.Tests;

public sealed class ConnectionOwnershipTests
{
    [Fact]
    public async Task ReadAsync_CloseAsyncFalha_DescartaConexaoMesmoAssim()
    {
        var connection = new CloseFailureConnection();
        var source = new AdoNetDataSource("Dados", () => connection, "SELECT 1 AS Valor");
        var action = async () => { await foreach (var row in source.ReadAsync()) { row["Valor"].Should().Be(1L); } };
        await action.Should().ThrowAsync<IOException>().WithMessage("Falha simulada*");
        connection.WasDisposed.Should().BeTrue();
    }
}
