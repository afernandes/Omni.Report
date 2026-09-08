using FluentAssertions;
using Reporting.DataSources.Json;
using Xunit;
namespace Reporting.DataSources.Providers.Tests;

public sealed class DecimalPrecisionTests
{
    [Fact]
    public async Task ReadAsync_ValorCom28Digitos_PreservaDecimalExato()
    {
        var source = new JsonDataSource("Valores", new JsonDataSourceOptions { InlineJson = "[{\"valor\":12345678901234567890.12345678,\"codigo\":\"00123\"}]" });
        var rows = await source.ReadAsync().ToListAsync();
        rows.Single()["valor"].Should().Be(12345678901234567890.12345678m);
        rows.Single()["codigo"].Should().Be("00123");
    }
}
