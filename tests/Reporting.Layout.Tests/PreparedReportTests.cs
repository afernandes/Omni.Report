using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class PreparedReportTests
{
    [Fact]
    public async Task PrepareAsync_ImagemFormasEGrafico_PreservaTodosOsTiposDePrimitiva()
    {
        var bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 40.Mm());
        var definition = TestData.GroupedReport() with
        {
            Detail = new DetailBand(50.Mm(), EquatableArray.Create<ReportElement>(
                new LabelElement { Id = "texto", Text = "Conteúdo", Bounds = bounds },
                new LineElement { Id = "linha", Bounds = bounds },
                new RectangleElement { Id = "retangulo", Bounds = bounds, FillColor = Reporting.Styling.Color.Red },
                new EllipseElement { Id = "elipse", Bounds = bounds, FillColor = Reporting.Styling.Color.Red },
                new ImageElement { Id = "imagem", Bounds = bounds, Source = ImageSourceKind.Inline, InlineData = new byte[] { 137, 80, 78, 71 } },
                new ChartElement
                {
                    Id = "grafico", Bounds = bounds, Kind = ChartKind.Line,
                    Series = EquatableArray.Create(new ChartSeries("Valores", "Fields.Produto", "Fields.Total"))
                }))
        };
        var request = TestData.BuildRequest(definition, TestData.ThreeRows());
        var paginator = new ReportPaginator();
        var expected = await paginator.PaginateAsync(request);
        expected.Pages.SelectMany(page => page.Primitives).Select(primitive => primitive.GetType()).Distinct()
            .Should().HaveCount(6);
        using var prepared = await paginator.PrepareAsync(request);
        prepared.PageCount.Should().Be(expected.PageCount);
        for (int index = 0; index < expected.PageCount; index++)
        {
            prepared.GetPage(index).Should().Be(expected.Pages[index]);
        }
    }

    [Fact]
    public async Task StreamPagesAsync_ConsumidorParaNaPrimeiraPagina_CancelaTrabalhoPendente()
    {
        var baseline = TestData.BuildRequest(TestData.GroupedReport(100), TestData.ManyRows(100));
        var request = new PaginationRequest
        {
            Definition = baseline.Definition,
            DataSources = baseline.DataSources,
            Limits = new() { MaxPages = 10 }
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var page in new ReportPaginator().StreamPagesAsync(request, timeout.Token))
        {
            page.PageNumber.Should().Be(1);
            break;
        }
        timeout.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task StreamPagesAsync_DocumentoCompleto_PreservaSequencia()
    {
        var request = TestData.BuildRequest(TestData.GroupedReport(100), TestData.ManyRows(15));
        var expected = await new ReportPaginator().PaginateAsync(request);
        var pages = new List<RenderedPage>();
        await foreach (var page in new ReportPaginator().StreamPagesAsync(request)) pages.Add(page);
        pages.Should().Equal(expected.Pages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrepareAsync_RelatorioComValoresDecimais_PreservaPaginasEPrimitivas(bool totalPages)
    {
        var definition = TestData.GroupedReport(90) with
        {
            Detail = new DetailBand(90.Mm(), EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Bounds = new(0.Mm(), 0.Mm(), 100.Mm(), 6.Mm()),
                Expression = totalPages ? "Page.Total" : "Fields.Total"
            }))
        };
        var request = TestData.BuildRequest(definition, TestData.ManyRows(20));
        var paginator = new ReportPaginator();
        var expected = await paginator.PaginateAsync(request);
        using var prepared = await paginator.PrepareAsync(request);
        prepared.PageCount.Should().Be(expected.PageCount);
        for (int i = prepared.PageCount - 1; i >= 0; i--)
            prepared.GetPage(i).Should().Be(expected.Pages[i]);
        prepared.StorageBytes.Should().BeGreaterThan(0);
    }

    public sealed record Medicao(double Valor);

    [Fact]
    public async Task PrepareAsync_CampoDoubleNaoFinito_PreservaValorSemantico()
    {
        var definition = ReportDefinition.Empty("Medicoes") with
        {
            DataSources = EquatableArray.Create(new Reporting.Data.DataSourceDefinition("Medicoes")),
            Detail = new DetailBand(6.Mm(), EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Bounds = new(0.Mm(), 0.Mm(), 100.Mm(), 6.Mm()),
                Expression = "Fields.Valor"
            }))
        };
        var registry = new Reporting.DataSources.DataSourceRegistry();
        registry.Register(new Reporting.DataSources.Enumerable.EnumerableDataSource<Medicao>("Medicoes",
            [new(double.NaN), new(double.PositiveInfinity), new(double.NegativeInfinity)]));
        var request = new PaginationRequest { Definition = definition, DataSources = registry };

        using var prepared = await new ReportPaginator().PrepareAsync(request);

        var valores = prepared.GetPage(0).Primitives.OfType<Reporting.Layout.Primitives.DrawTextPrimitive>()
            .Select(p => p.SemanticValue).OfType<double>().ToList();
        valores.Should().Equal(double.NaN, double.PositiveInfinity, double.NegativeInfinity);
    }

    [Fact]
    public async Task PrepareAsync_LimiteDeArmazenamento_RemoveArquivoParcial()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var request = TestData.BuildRequest(TestData.GroupedReport(), TestData.ThreeRows());
            var action = () => new ReportPaginator().PrepareAsync(request,
                new() { TemporaryDirectory = directory, MaxStorageBytes = 1 });
            await action.Should().ThrowAsync<PaginationLimitExceededException>();
            Directory.GetFiles(directory).Should().BeEmpty();
        }
        finally { Directory.Delete(directory); }
    }

    [Fact]
    public async Task Dispose_DocumentoPreparado_ImpedeLeituraERemoveArquivo()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var prepared = await new ReportPaginator().PrepareAsync(
                TestData.BuildRequest(TestData.GroupedReport(), TestData.ThreeRows()),
                new() { TemporaryDirectory = directory });
            Directory.GetFiles(directory).Should().ContainSingle();
            prepared.Dispose();
            prepared.Invoking(p => p.GetPage(0)).Should().Throw<ObjectDisposedException>();
            Directory.GetFiles(directory).Should().BeEmpty();
        }
        finally { Directory.Delete(directory); }
    }
}
