using FluentAssertions;
using Reporting.Common;
using Reporting.Data;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class GroupPlanningTests
{
    [Fact]
    public async Task PaginateAsync_FiltroDeAgregadoDeGrupo_RemoveInstanciaInteira()
    {
        var definition = TestData.GroupedReport();
        definition = definition with
        {
            Groups = EquatableArray.Create(definition.Groups[0] with
            { FilterExpression = "Sum(Fields.Total, 'Group') > 10" })
        };
        var result = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ThreeRows()));
        var texts = result.Pages.SelectMany(page => page.Primitives).OfType<DrawTextPrimitive>().Select(text => text.Text);
        texts.Should().Contain("Ana").And.NotContain("Beto");
        texts.Count(text => text == "Linha").Should().Be(2);
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task PaginateAsync_OrdenacaoPorAgregado_PreservaLinhasDoGrupoEOrdemDosEmpates()
    {
        var definition = TestData.GroupedReport();
        definition = definition with
        {
            Groups = EquatableArray.Create(definition.Groups[0] with
            { SortExpressions = EquatableArray.Create(new SortDescriptor("Sum(Fields.Total, 'Group')", SortDirection.Descending)) })
        };
        Venda[] rows = [new("A", "1", 1m), new("B", "2", 7m), new("A", "3", 2m), new("C", "4", 7m)];
        var result = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, rows));
        result.Pages.SelectMany(page => page.Primitives).OfType<DrawTextPrimitive>()
            .Where(text => text.SourceElementId == "group-header").Select(text => text.Text).Should().Equal("B", "C", "A", "A");
    }
}
