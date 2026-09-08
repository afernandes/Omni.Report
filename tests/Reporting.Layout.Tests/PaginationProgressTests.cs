using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class PaginationProgressTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task PaginateAsync_GeometriaSemAreaUtil_RejeitaAntesDeLerDados(int caso)
    {
        var setup = SmallPage();
        setup = caso switch
        {
            0 => setup with { Margins = new Thickness(Unit.Zero, new Unit(500), Unit.Zero, new Unit(500)) },
            1 => setup with { Margins = new Thickness(Unit.Zero, new Unit(600), Unit.Zero, new Unit(500)) },
            2 => setup with { Margins = new Thickness(new Unit(500), Unit.Zero, new Unit(500), Unit.Zero) },
            3 => setup with { Columns = 0 },
            4 => setup with { ColumnSpacing = new Unit(-1) },
            5 => setup with { Columns = 2, ColumnSpacing = new Unit(1000) },
            6 => setup with { Columns = int.MaxValue, ColumnSpacing = new Unit(int.MaxValue) },
            _ => setup with { Paper = new PaperSize("Inválido", new Unit(1000), new Unit(-1)) },
        };
        var leituras = 0;
        IEnumerable<Venda> LerDados()
        {
            leituras++;
            yield return new Venda("Cliente", "Produto", 1m);
        }
        var definition = new ReportDefinition("Sem área", setup, EmptyDetail(2000));
        var request = TestData.BuildRequest(definition, LerDados());

        var act = () => new ReportPaginator().PaginateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
        leituras.Should().Be(0);
    }

    [Fact]
    public async Task PaginateAsync_RodapeOcupaAreaUtil_RejeitaAntesDeLerDados()
    {
        var definition = new ReportDefinition("Rodapé", SmallPage(), EmptyDetail(2000))
        {
            PageFooter = new ReportBand(BandKind.PageFooter, new Unit(1000), EquatableArray<ReportElement>.Empty),
        };
        var act = () => new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(1)));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PaginateAsync_CabecalhoRepetidoOcupaAreaUtil_FalhaSemRepetirPaginas(bool grupo, bool comElemento)
    {
        var definition = new ReportDefinition("Cabeçalho", SmallPage(), comElemento ? LabelDetail(2000) : EmptyDetail(2000));
        if (grupo)
        {
            definition = definition with
            {
                Groups = EquatableArray.Create(new GroupBand("Cliente", "Fields.Cliente",
                    Header: new ReportBand(BandKind.GroupHeader, new Unit(1000), EquatableArray<ReportElement>.Empty),
                    RepeatHeaderOnNewPage: true)),
            };
        }
        else
        {
            definition = definition with
            {
                PageHeader = new ReportBand(BandKind.PageHeader, new Unit(1000), EquatableArray<ReportElement>.Empty),
            };
        }

        var act = () => new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(1)))
            .WaitAsync(TimeSpan.FromSeconds(5));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*área útil*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PaginateAsync_CabecalhoUnicoOcupaPrimeiraPagina_ContinuaNaProxima(int colunas)
    {
        var definition = new ReportDefinition("Cabeçalho único", SmallPage() with { Columns = colunas }, LabelDetail(100))
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, new Unit(1000), EquatableArray<ReportElement>.Empty),
        };

        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(1)));

        report.PageCount.Should().Be(2);
        report.Pages[1].Primitives.OfType<DrawTextPrimitive>().Should().ContainSingle()
            .Which.Text.Should().Be("Conteúdo");
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    public async Task PaginateAsync_BandaVaziaMaiorQuePagina_PreservaAlturaETermina(int colunas, int paginas)
    {
        var definition = new ReportDefinition("Banda alta", SmallPage() with { Columns = colunas }, EmptyDetail(2500));

        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(1)));

        report.PageCount.Should().Be(paginas);
    }

    [Fact]
    public async Task PaginateAsync_ElementoIndivisivelMaiorQuePagina_EmiteUmaVez()
    {
        var definition = new ReportDefinition("Elemento alto", SmallPage(), LabelDetail(2500));

        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(1)));

        report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Should().ContainSingle();
    }

    [Fact]
    public async Task PaginateAsync_PapelTermicoComAlturaZero_MantemPaginaContinua()
    {
        var definition = new ReportDefinition("Térmico", new PageSetup(PaperSize.Thermal80), LabelDetail(2500));

        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(3)));

        report.PageCount.Should().Be(1);
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Should().HaveCount(3);
    }

    [Fact]
    public async Task PaginateAsync_CabecalhoCresceAlemDaPagina_RejeitaFaltaDeProgresso()
    {
        var definition = new ReportDefinition("Cabeçalho crescente", SmallPage(), EmptyDetail(2000))
        {
            PageHeader = new ReportBand(BandKind.PageHeader, new Unit(100), EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Expression = string.Join('\n', Enumerable.Repeat("Texto", 50)),
                    CanGrow = true,
                    Bounds = new Rectangle(Unit.Zero, Unit.Zero, new Unit(500), new Unit(100)),
                })),
        };
        var act = () => new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(1)))
            .WaitAsync(TimeSpan.FromSeconds(5));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*área útil*");
    }

    [Fact]
    public async Task PaginateAsync_CabecalhoSoNaContinuacaoSemArea_FalhaNaQuebra()
    {
        var definition = new ReportDefinition("Continuação", SmallPage(), EmptyDetail(2000))
        {
            PageHeader = new ReportBand(BandKind.PageHeader, new Unit(1000), EquatableArray<ReportElement>.Empty,
                PrintOnFirstPage: false),
        };
        var act = () => new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(1)))
            .WaitAsync(TimeSpan.FromSeconds(5));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*área útil*");
    }

    [Fact]
    public async Task PaginateAsync_CancelaDuranteDivisaoDaBanda_InterrompeAntesDaProximaPagina()
    {
        using var cts = new CancellationTokenSource();
        var cabecalhos = 0;
        var definition = new ReportDefinition("Cancelamento", SmallPage(), EmptyDetail(5000))
        {
            PageHeader = new ReportBand(BandKind.PageHeader, new Unit(100), EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Expression = "{Code.Pagina()}",
                    Bounds = new Rectangle(Unit.Zero, Unit.Zero, new Unit(500), new Unit(100)),
                })),
        };
        var request = new PaginationRequest
        {
            Definition = definition,
            DataSources = TestData.BuildRequest(definition, TestData.ManyRows(1)).DataSources,
            CodeFunctionResolver = (_, _) =>
            {
                if (++cabecalhos == 2)
                {
                    cts.Cancel();
                }
                return cabecalhos;
            },
        };

        var act = () => new ReportPaginator().PaginateAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        cabecalhos.Should().Be(2);
    }

    private static PageSetup SmallPage() => new(new PaperSize("Teste", new Unit(1000), new Unit(1000)));

    private static DetailBand EmptyDetail(int altura) => new(new Unit(altura), EquatableArray<ReportElement>.Empty);

    private static DetailBand LabelDetail(int altura) => new(new Unit(altura), EquatableArray.Create<ReportElement>(
        new LabelElement { Text = "Conteúdo", Bounds = new Rectangle(Unit.Zero, Unit.Zero, new Unit(500), new Unit(altura)) }));
}
