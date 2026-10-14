using FluentAssertions;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class PaginationLimitsTests
{
    [Theory]
    [InlineData("MaxSourceRows")]
    [InlineData("MaxIterationRows")]
    [InlineData("MaxPages")]
    [InlineData("MaxPrimitives")]
    public async Task PaginateAsync_OrcamentoEsgotado_IdentificaLimiteSemContaminarProximaExecucao(string budget)
    {
        var baseline = TestData.BuildRequest(TestData.GroupedReport(), TestData.ThreeRows());
        var limits = new PaginationLimits();
        typeof(PaginationLimits).GetProperty(budget)!.SetValue(limits, 0L);
        var request = new PaginationRequest
        {
            Definition = baseline.Definition,
            DataSources = baseline.DataSources,
            Limits = limits
        };
        var paginator = new ReportPaginator();
        var action = () => paginator.PaginateAsync(request);
        var failure = await action.Should().ThrowAsync<PaginationLimitExceededException>();
        failure.Which.Budget.Should().Be(budget);
        (await paginator.PaginateAsync(baseline)).PageCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PaginateAsync_OrcamentoNegativo_RejeitaConfiguracao()
    {
        var baseline = TestData.BuildRequest(TestData.GroupedReport(), TestData.ThreeRows());
        var action = () => new ReportPaginator().PaginateAsync(new PaginationRequest
        {
            Definition = baseline.Definition,
            DataSources = baseline.DataSources,
            Limits = new() { MaxSourceRows = -1 }
        });
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
