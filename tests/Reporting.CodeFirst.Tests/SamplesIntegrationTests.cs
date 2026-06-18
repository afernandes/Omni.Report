using System.Diagnostics;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Rendering.Skia;
using Reporting.Samples.CodeFirst.Reports;
using Xunit;
using SampleVenda = Reporting.Samples.CodeFirst.Data.Venda;

namespace Reporting.CodeFirst.Tests;

public class SamplesIntegrationTests
{
    [Fact]
    public async Task Sample01_paginates_and_renders_to_pdf()
    {
        var report = Sample01_VendasPorCliente.Build();
        var rendered = await report.PaginateAsync();
        rendered.Pages.Should().NotBeEmpty();

        using var ctx = new SkiaRenderingContext();
        RenderedReportPlayer.Play(rendered, ctx);
        var pdf = ctx.ToPdfBytes();
        AssertPdfMagic(pdf);
    }

    [Fact]
    public async Task Sample02_paginates_and_renders_to_pdf()
    {
        var report = Sample02_EspelhoProdutos.Build();
        var rendered = await report.PaginateAsync();
        rendered.Pages.Should().NotBeEmpty();

        using var ctx = new SkiaRenderingContext();
        RenderedReportPlayer.Play(rendered, ctx);
        AssertPdfMagic(ctx.ToPdfBytes());
    }

    [Fact]
    public async Task Sample03_paginates_with_multiple_groups()
    {
        var report = Sample03_RelatorioCaixa.Build();
        var rendered = await report.PaginateAsync();
        rendered.Pages.Should().NotBeEmpty();

        using var ctx = new SkiaRenderingContext();
        RenderedReportPlayer.Play(rendered, ctx);
        AssertPdfMagic(ctx.ToPdfBytes());
    }

    [Fact]
    public async Task Sample01_with_10k_rows_finishes_under_2_seconds()
    {
        var rows = SyntheticVendas(10_000).ToList();
        var report = Sample01_VendasPorCliente.Build(rows);

        var sw = Stopwatch.StartNew();
        var rendered = await report.PaginateAsync();
        sw.Stop();

        rendered.Pages.Count.Should().BeGreaterThan(1);
        // Orçamento de performance: generoso no CI (runners compartilhados/mais lentos),
        // apertado numa máquina de dev. GitHub Actions define CI=true automaticamente.
        var budgetSeconds = Environment.GetEnvironmentVariable("CI") is null ? 2.0 : 10.0;
        sw.Elapsed.TotalSeconds.Should().BeLessThan(budgetSeconds,
            because: $"10k linhas levaram {sw.Elapsed.TotalSeconds:F2}s; alvo < {budgetSeconds}s.");
    }

    private static IEnumerable<SampleVenda> SyntheticVendas(int n)
    {
        var clientes = new[] { "Ana", "Beto", "Carla", "Daniel", "Eva", "Fábio" };
        var produtos = new[] { "Caneta", "Caderno", "Lápis", "Borracha", "Régua" };
        var rnd = new Random(42);
        var raw = new List<SampleVenda>(n);
        for (int i = 0; i < n; i++)
        {
            var cliente = clientes[i % clientes.Length];
            var produto = produtos[rnd.Next(produtos.Length)];
            raw.Add(new SampleVenda(cliente, produto, rnd.Next(1, 20), 1.5m + (decimal)rnd.NextDouble() * 50m,
                new DateTime(2026, 5, 1).AddMinutes(i)));
        }
        // Pre-sort by Cliente so grouping forms contiguous blocks.
        return raw.OrderBy(v => v.Cliente);
    }

    private static void AssertPdfMagic(byte[] pdf)
    {
        pdf.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }
}
