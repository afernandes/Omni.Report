using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class GroupKeepTogetherTests
{
    [Fact]
    public async Task PaginateAsync_GrupoCabeEmPaginaNova_MoveCabecalhoDetalhesERodapeJuntos()
    {
        var definition = Definition();
        Venda[] rows = [.. Enumerable.Repeat(new Venda("A", "Produto", 1m), 5),
            new("B", "Produto", 2m), new("B", "Produto", 3m)];
        var result = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, rows));
        result.PageCount.Should().Be(2);
        result.Pages[0].Primitives.OfType<DrawTextPrimitive>().Should().NotContain(text => text.Text == "B");
        result.Pages[1].Primitives.OfType<DrawTextPrimitive>().Count(text => text.Text == "B").Should().Be(3);
        result.Pages[1].Primitives.OfType<DrawTextPrimitive>()
            .Single(text => text.SourceElementId == "group-footer").SemanticValue.Should().Be(5m);
    }

    [Fact]
    public async Task PaginateAsync_GrupoMaiorQuePagina_ProgrideRepeteCabecalhoEPreservaTotal()
    {
        var definition = Definition();
        Venda[] rows = [new("A", "Produto", 1m), .. Enumerable.Repeat(new Venda("B", "Produto", 2m), 12)];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, rows), cancellation.Token);
        var texts = result.Pages.SelectMany(page => page.Primitives).OfType<DrawTextPrimitive>().ToArray();
        texts.Count(text => text.SourceElementId == "detail").Should().Be(13);
        texts.Count(text => text.SourceElementId == "group-header" && text.Text == "B").Should().BeGreaterThan(1);
        texts.Last(text => text.SourceElementId == "group-footer").SemanticValue.Should().Be(24m);
        result.PageCount.Should().BeInRange(2, 4);
    }

    [Fact]
    public async Task PaginateAsync_GrupoAninhado_MedeBandasInternasSemDuplicarTotais()
    {
        var definition = Definition();
        var inner = new GroupBand("Produto", "Fields.Produto",
            new ReportBand(BandKind.GroupHeader, 5.Mm(), []),
            new ReportBand(BandKind.GroupFooter, 5.Mm(), []));
        definition = definition with { Groups = EquatableArray.Create(definition.Groups[0], inner) };
        Venda[] rows = [.. Enumerable.Repeat(new Venda("A", "Produto", 1m), 5),
            new("B", "Um", 2m), new("B", "Dois", 3m)];
        var result = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, rows));
        result.Pages[0].Primitives.OfType<DrawTextPrimitive>().Should().NotContain(text => text.Text == "B");
        result.Pages[1].Primitives.OfType<DrawTextPrimitive>().Count(text => text.Text == "B").Should().Be(3);
    }

    private static ReportDefinition Definition()
    {
        var definition = TestData.GroupedReport(40);
        return definition with
        {
            Groups = EquatableArray.Create(definition.Groups[0] with { KeepTogether = true, RepeatHeaderOnNewPage = true }),
            Detail = new DetailBand(40.Mm(), EquatableArray.Create<ReportElement>(new TextBoxElement
            { Id = "detail", Bounds = new(0.Mm(), 0.Mm(), 100.Mm(), 6.Mm()), Expression = "Fields.Cliente" }))
        };
    }
}
