using System.Text.Json;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Output.Json;
using Xunit;

namespace Reporting.Output.Tests;

/// <summary>
/// "Páginas 3 a 7" works with every exporter through one contract: a <see cref="PageRange"/> on
/// <c>ExportAsync</c>, for a paginated report or a prepared one — and from a prepared one only the requested pages
/// are read back.
/// </summary>
public sealed class PageRangeExportTests
{
    private sealed record Linha(string Nome);

    private static Report BigReport() =>
        ReportBuilder.Create("Faixa")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Enumerable.Range(0, 400).Select(i => new Linha($"Item {i}")).ToArray())
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 6))
            .Build();

    [Fact]
    public async Task ExportAsync_RelatorioPaginadoComFaixa_ExportaSoAsPaginasPedidas()
    {
        var report = await BigReport().PaginateAsync();
        report.PageCount.Should().BeGreaterThan(7);
        using var output = new MemoryStream();

        await new JsonExporter().ExportAsync(report, output, new PageRange(3, 7));

        PageNumbersIn(output).Should().Equal(3, 4, 5, 6, 7);
    }

    [Fact]
    public async Task ExportAsync_RelatorioPreparadoComFaixa_ExportaSoAsPaginasPedidas()
    {
        var built = BigReport();
        using var prepared = await new ReportPaginator().PrepareAsync(
            new PaginationRequest { Definition = built.Definition, DataSources = built.DataSources });
        using var output = new MemoryStream();

        await new JsonExporter().ExportAsync(prepared, output, PageRange.Parse("2-3"));

        PageNumbersIn(output).Should().Equal(2, 3);
    }

    [Fact]
    public async Task ExportAsync_RelatorioPreparadoSemFaixa_ExportaTodasAsPaginas()
    {
        var built = BigReport();
        using var prepared = await new ReportPaginator().PrepareAsync(
            new PaginationRequest { Definition = built.Definition, DataSources = built.DataSources });
        using var output = new MemoryStream();

        await new JsonExporter().ExportAsync(prepared, output);

        PageNumbersIn(output).Should().HaveCount(prepared.PageCount);
    }

    [Fact]
    public async Task ExportAsync_RelatorioPreparadoDescartado_FalhaEmVezDeExportarVazio()
    {
        var built = BigReport();
        var prepared = await new ReportPaginator().PrepareAsync(
            new PaginationRequest { Definition = built.Definition, DataSources = built.DataSources });
        prepared.Dispose();
        using var output = new MemoryStream();

        var act = () => new JsonExporter().ExportAsync(prepared, output);

        await act.Should().ThrowAsync<ObjectDisposedException>();
        output.Length.Should().Be(0);
    }

    [Fact]
    public async Task ExportAsync_FaixaAlemDaUltimaPagina_FalhaDizendoQuantasPaginasHa()
    {
        var report = await BigReport().PaginateAsync();
        var last = report.PageCount;

        var act = () => new JsonExporter().ExportAsync(report, Stream.Null, new PageRange(last, last + 1));

        (await act.Should().ThrowAsync<ArgumentOutOfRangeException>()).WithMessage($"*{last} página(s)*");
    }

    [Fact]
    public async Task ExportAsync_FaixaNaoInicializada_FalhaExplicando()
    {
        var report = await BigReport().PaginateAsync();

        var act = () => new JsonExporter().ExportAsync(report, Stream.Null, default(PageRange));

        (await act.Should().ThrowAsync<ArgumentOutOfRangeException>()).WithMessage("*não foi inicializada*");
    }

    [Theory]
    [InlineData("5", 5, 5)]
    [InlineData("3-7", 3, 7)]
    [InlineData(" 3 - 7 ", 3, 7)]
    public void TryParse_PaginaOuFaixa_Interpreta(string text, int from, int to)
    {
        PageRange.TryParse(text, out var range).Should().BeTrue();
        range.Should().Be(new PageRange(from, to));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("7-3")]
    [InlineData("3-")]
    [InlineData("a-b")]
    [InlineData("1-2-3")]
    public void TryParse_TextoInvalido_Recusa(string text)
        => PageRange.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void Parse_TextoInvalido_ExplicaOFormato()
        => FluentActions.Invoking(() => PageRange.Parse("x")).Should().Throw<FormatException>().WithMessage("*\"3-7\"*");

    // The JSON exporter writes each page with its number; reading them back says exactly which pages were exported.
    private static List<int> PageNumbersIn(MemoryStream output)
    {
        using var json = JsonDocument.Parse(output.ToArray());
        return json.RootElement.GetProperty("pages").EnumerateArray()
            .Select(page => page.GetProperty("pageNumber").GetInt32()).ToList();
    }
}
