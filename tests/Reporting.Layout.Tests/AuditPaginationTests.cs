using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Parameters;
using Xunit;
namespace Reporting.Layout.Tests;

public sealed class AuditPaginationTests
{
    [Fact]
    public async Task PaginateAsync_SubrelatorioEmCadaLinha_LeFonteUmaVezEIgnoraFonteNaoUsada()
    {
        var source = new AuditCountingSource(new EnumerableDataSource<Venda>("Vendas", TestData.ThreeRows()));
        var unused = new AuditCountingSource(new EnumerableDataSource<Venda>("NaoUsada", []), true);
        var registry = new DataSourceRegistry(); registry.Register(source); registry.Register(unused);
        var child = TestData.GroupedReport() with { Groups = EquatableArray<GroupBand>.Empty };
        var parent = child with { Detail = new DetailBand(30.Mm(), EquatableArray.Create<ReportElement>(new SubreportElement
            { Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 30.Mm()), InlineDefinition = child })) };
        var rendered = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = parent, DataSources = registry });
        source.Reads.Should().Be(1);
        unused.Reads.Should().Be(0);
        Texts(rendered).Count(text => text == "Linha").Should().Be(9);
    }
    [Fact]
    public async Task PaginateAsync_VariaveisDependentesPorLinha_EfetuaCalculoTopologico()
    {
        var definition = TestData.GroupedReport() with
        {
            Groups = EquatableArray<GroupBand>.Empty,
            Variables = EquatableArray.Create(new ReportVariable("Final", "Variables.Base + Fields.Total"), new ReportVariable("Base", "1.25", VariableScope.Report)),
            Detail = new DetailBand(6.Mm(), EquatableArray.Create<ReportElement>(Text("{Variables.Final:F2}")))
        };
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ThreeRows()));
        Texts(rendered).Should().Equal("11,25", "26,25", "6,25");
    }
    [Fact]
    public async Task PaginateAsync_VariaveisCiclicas_FalhaComDiagnostico()
    {
        var definition = TestData.GroupedReport() with { Variables = EquatableArray.Create(new ReportVariable("A", "Variables.B"), new ReportVariable("B", "Variables.A")) };
        var action = () => new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ThreeRows()));
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cyclic*");
    }
    [Fact]
    public async Task PaginateAsync_EmpateEmOrdenacao_PreservaOrdemOriginal()
    {
        var definition = TestData.GroupedReport() with
        {
            Groups = EquatableArray<GroupBand>.Empty,
            Detail = new DetailBand(6.Mm(), EquatableArray.Create<ReportElement>(Text("{Fields.Produto}")))
                { SortExpressions = EquatableArray.Create(new Reporting.Data.SortDescriptor("Fields.Total")) }
        };
        var rows = Enumerable.Range(0, 40).Select(i => new Venda("A", "P" + i, 1m)).ToArray();
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, rows));
        Texts(rendered).Should().Equal(rows.Select(row => row.Produto));
    }
    [Fact]
    public async Task PaginateAsync_TotalPaginasDentroDeRetangulo_ExecutaSegundaPassagem()
    {
        var definition = TestData.GroupedReport() with
        {
            Groups = EquatableArray<GroupBand>.Empty,
            Detail = new DetailBand(100.Mm(), EquatableArray.Create<ReportElement>(new RectangleElement
                { Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 100.Mm()), Children = EquatableArray.Create<ReportElement>(Text("{Page.Total}")) }))
        };
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ManyRows(8)));
        rendered.PageCount.Should().BeGreaterThan(1);
        Texts(rendered).Should().OnlyContain(text => text == rendered.PageCount.ToString());
    }
    [Fact]
    public async Task PaginateAsync_PropriedadeDeGrupoNaoSuportada_EmiteDiagnosticoExplicito()
    {
        var definition = TestData.GroupedReport();
        definition = definition with { Groups = EquatableArray.Create(definition.Groups[0] with { FilterExpression = "false", KeepTogether = true }) };
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ThreeRows()));
        rendered.Diagnostics.Should().HaveCount(2).And.OnlyContain(d => d.Code == "ORL024");
    }
    [Fact]
    public async Task ReadAsync_ViewsDePaisConcorrentes_IsolaFiltroDeCadaEnumeracao()
    {
        var source = new MasterDetailDataSource("Filhos", new EnumerableDataSource<Venda>("Vendas", TestData.ThreeRows()), "Cliente");
        var views = new[] { source.WithParentValue("Ana"), source.WithParentValue("Beto"), source.WithParentValue("Ana") };
        var results = await Task.WhenAll(views.Select(async view =>
        {
            var names = new List<string>();
            await foreach (var row in view.ReadAsync()) { await Task.Yield(); names.Add((string)row["Cliente"]!); }
            return names;
        }));
        results[0].Should().Equal("Ana", "Ana"); results[1].Should().Equal("Beto"); results[2].Should().Equal("Ana", "Ana");
    }
    [Fact]
    public async Task PaginateAsync_VariaveisDeGrupo_RecalculaEmCadaInstancia()
    {
        var definition = TestData.GroupedReport();
        var group = definition.Groups[0] with
        {
            Variables = EquatableArray.Create(new ReportVariable("Imposto", "Fields.Total * 0.1", VariableScope.Group)),
            Header = new ReportBand(BandKind.GroupHeader, 6.Mm(), EquatableArray.Create<ReportElement>(Text("G:{Variables.Imposto:F2}")))
        };
        definition = definition with { Groups = EquatableArray.Create(group) };
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ThreeRows()));
        Texts(rendered).Where(text => text.StartsWith("G:")).Should().Equal("G:1,00", "G:0,50");
    }
    [Fact]
    public async Task PaginateAsync_CancelaDuranteExpressao_InterrompeProcessamento()
    {
        using var cancellation = new CancellationTokenSource();
        var definition = TestData.GroupedReport() with { Detail = new DetailBand(6.Mm(), EquatableArray.Create<ReportElement>(Text("Code.Cancelar()"))) };
        var sourceRequest = TestData.BuildRequest(definition, TestData.ThreeRows());
        var request = new PaginationRequest
        {
            Definition = definition, DataSources = sourceRequest.DataSources,
            CodeFunctionResolver = (_, _) => { cancellation.Cancel(); return 1; }
        };
        var action = () => new ReportPaginator().PaginateAsync(request, cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaginateAsync_SubdetailComFiltroEOrdenacao_AplicaPoliticasOuMensagemVazia(bool empty)
    {
        var sub = new SubDetailBand("Itens", "Filhos", 6.Mm(), EquatableArray.Create<ReportElement>(Text("{Fields.Produto}")),
            NoRowsMessage: "Sem itens", FilterExpression: empty ? "false" : "Fields.Total >= 10",
            SortExpressions: EquatableArray.Create(new Reporting.Data.SortDescriptor("Fields.Total", Reporting.Data.SortDirection.Descending)));
        var definition = TestData.GroupedReport() with
        {
            Groups = EquatableArray<GroupBand>.Empty,
            Detail = new DetailBand(1.Mm(), EquatableArray<ReportElement>.Empty, SubDetails: EquatableArray.Create(sub))
        };
        var request = TestData.BuildRequest(definition, [new Venda("Pai", "Pai", 0m)]);
        request.DataSources.Register(new EnumerableDataSource<Venda>("Filhos", TestData.ThreeRows()));
        var rendered = await new ReportPaginator().PaginateAsync(request);
        Texts(rendered).Should().Equal(empty ? ["Sem itens"] : ["Caderno", "Caneta"]);
    }
    [Fact]
    public async Task PaginateAsync_TextoLiteralEmVariavel_NaoCriaDependenciaFalsa()
    {
        var definition = TestData.GroupedReport() with
        {
            Groups = EquatableArray<GroupBand>.Empty,
            Variables = EquatableArray.Create(new ReportVariable("Texto", "'Variables.Texto'", VariableScope.Report)),
            Detail = new DetailBand(6.Mm(), EquatableArray.Create<ReportElement>(Text("{Variables.Texto}")))
        };
        var rendered = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ThreeRows()));
        Texts(rendered).Should().OnlyContain(text => text == "Variables.Texto");
    }
    [Fact]
    public async Task PaginateAsync_ExpressaoRetornaObjeto_NaoRetemGrafoNoValorTabular()
    {
        var graph = new object();
        var definition = TestData.GroupedReport() with { Groups = EquatableArray<GroupBand>.Empty,
            Detail = new DetailBand(6.Mm(), EquatableArray.Create<ReportElement>(Text("Code.Objeto()"))) };
        var source = TestData.BuildRequest(definition, TestData.ThreeRows());
        var rendered = await new ReportPaginator().PaginateAsync(new PaginationRequest { Definition = definition, DataSources = source.DataSources, CodeFunctionResolver = (_, _) => graph });
        rendered.Pages.SelectMany(page => page.Primitives).OfType<DrawTextPrimitive>().Should().OnlyContain(text => text.SemanticValue == null);
    }
    private static TextBoxElement Text(string expression) => new() { Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 6.Mm()), Expression = expression };
    private static IEnumerable<string> Texts(RenderedReport report) => report.Pages.SelectMany(page => page.Primitives).OfType<DrawTextPrimitive>().Select(text => text.Text);
}
