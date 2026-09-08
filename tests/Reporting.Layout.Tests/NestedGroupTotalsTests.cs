using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class NestedGroupTotalsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PaginateAsync_GruposAninhados_TotalizaCadaInstanciaSemContaminacao(bool duasPassagens, bool repetirCabecalho)
    {
        var definition = TestData.GroupedReport() with
        {
            PageSetup = new PageSetup(new PaperSize("Pequeno", 120.Mm(), 70.Mm()), Margins: Thickness.Uniform(5.Mm())),
            Groups = EquatableArray.Create(
                new GroupBand("Cliente", "Fields.Cliente",
                    Header: Band(BandKind.GroupHeader, "HC:{GroupKey}:{CountRows('Group')}:{CountRows('Cliente')}"),
                    Footer: Band(BandKind.GroupFooter, "FC:{GroupKey}:{Sum(Fields.Total, 'Group'):F2}:{Sum(Fields.Total, 'Cliente'):F2}"),
                    RepeatHeaderOnNewPage: repetirCabecalho),
                new GroupBand("Produto", "Fields.Produto",
                    Header: Band(BandKind.GroupHeader, "HP:{GroupKey}"),
                    Footer: Band(BandKind.GroupFooter, "FP:{GroupKey}:{Sum(Fields.Total, 'Group'):F2}"),
                    RepeatHeaderOnNewPage: repetirCabecalho)),
            PageFooter = duasPassagens ? Band(BandKind.PageFooter, "{Page.Number}/{Page.Total}") : null,
        };
        var rows = new[]
        {
            new Venda("Ana", "P1", 1.25m), new Venda("Ana", "P2", 1.25m), new Venda("Ana", "P3", 1.25m),
            new Venda("Bia", "P3", 2.50m), new Venda("Bia", "P3", 3.75m),
        };

        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, rows));
        var texts = rendered.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(p => p.Text).ToArray();

        rendered.Pages.Count.Should().BeGreaterThan(1);
        texts.Where(t => t.StartsWith("FC:")).Should().Equal("FC:Ana:3,75:3,75", "FC:Bia:6,25:6,25");
        texts.Where(t => t.StartsWith("FP:")).Should().Equal("FP:P1:1,25", "FP:P2:1,25", "FP:P3:1,25", "FP:P3:6,25");
        foreach (var text in texts.Where(t => t.StartsWith("HC:")))
        {
            var parts = text.Split(':');
            parts[1].Should().BeOneOf("Ana", "Bia");
            parts[2].Should().Be(parts[3], "o cabeçalho externo usa seu próprio escopo mesmo na reimpressão");
        }
    }

    [Fact]
    public async Task PaginateAsync_NomesDeGrupoRepetidos_SelecionaEscopoPelaProfundidadeDaBanda()
    {
        var definition = TestData.GroupedReport() with
        {
            Groups = EquatableArray.Create(
                new GroupBand("Grupo", "Fields.Cliente",
                    Header: Band(BandKind.GroupHeader, "HC:{GroupKey}"),
                    Footer: Band(BandKind.GroupFooter, "FC:{GroupKey}:{Sum(Fields.Total, 'Group'):F2}")),
                new GroupBand("Grupo", "Fields.Produto",
                    Footer: Band(BandKind.GroupFooter, "FP:{GroupKey}:{Sum(Fields.Total, 'Group'):F2}"))),
        };
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition,
            [new Venda("Ana", "P1", 1.25m), new Venda("Ana", "P2", 2.50m)]));
        var texts = rendered.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(p => p.Text);
        texts.Should().Contain("HC:Ana").And.Contain("FC:Ana:3,75")
            .And.Contain("FP:P1:1,25").And.Contain("FP:P2:2,50");
    }

    [Fact]
    public async Task PaginateAsync_DadosVazios_NaoEmiteGruposNemTotaisFicticios()
    {
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(TestData.GroupedReport(), []));
        rendered.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Should().BeEmpty();
    }

    private static ReportBand Band(BandKind kind, string expression) => new(kind, 5.Mm(),
        EquatableArray.Create<ReportElement>(new TextBoxElement
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, 110.Mm(), 5.Mm()),
            Expression = expression,
        }));
}
