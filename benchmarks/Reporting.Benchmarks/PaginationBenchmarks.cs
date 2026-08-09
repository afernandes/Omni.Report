using BenchmarkDotNet.Attributes;
using Reporting.CodeFirst;
using Reporting.Layout;

namespace Reporting.Benchmarks;

/// <summary>
/// The core engine benchmark: how pagination scales with row count, in both time and allocation.
///
/// <para>This is the measurement the roadmap's streaming item (15) waits on. The paginator materialises every
/// data source into memory before laying out; whether replacing that with real streaming is worth the
/// restructuring depends on how the allocation actually grows here — <c>[MemoryDiagnoser]</c> reports
/// allocated bytes per operation, so "memory is proportional to the dataset" stops being a claim and becomes
/// a number.</para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)] // enough signal without a 20-minute run; raise for a real study
public class PaginationBenchmarks
{
    private sealed record Venda(string Cliente, string Produto, decimal Total, DateTime Data);

    [Params(1_000, 10_000, 100_000)]
    public int Rows { get; set; }

    private Report _report = null!;

    [GlobalSetup]
    public void Setup()
    {
        var data = Enumerable.Range(0, Rows)
            .Select(i => new Venda($"Cliente {i % 500:000}", $"Produto {i % 40:00}", (i % 997) * 1.5m,
                new DateTime(2026, 1, 1).AddDays(i % 365)))
            .ToArray();

        _report = ReportBuilder.Create("Bench")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", data)
            .Detail(d => d.Height(6)
                .Text("{Fields.Cliente}").At(0, 0).Size(60, 5)
                .Text("{Fields.Produto}").At(62, 0).Size(50, 5)
                .Text("{Fields.Total:C}").At(114, 0).Size(30, 5).AlignRight())
            .Build();
    }

    /// <summary>Straight pagination — one pass, no Page.Total, no grouping.</summary>
    [Benchmark(Baseline = true)]
    public async Task<int> Paginate()
    {
        var rendered = await _report.PaginateAsync();
        return rendered.Pages.Count;
    }
}

/// <summary>
/// Isolates the cost of the SECOND layout pass. <c>Page.Total</c> (and last-page gating) force the paginator
/// to run the whole layout twice, because the page count is only known after the first pass. Comparing this
/// with <see cref="PaginationBenchmarks.Paginate"/> shows what that convenience costs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
public class TwoPassBenchmarks
{
    private sealed record Linha(string Nome);

    [Params(10_000)]
    public int Rows { get; set; }

    private Report _onePass = null!;
    private Report _twoPass = null!;

    [GlobalSetup]
    public void Setup()
    {
        var data = Enumerable.Range(0, Rows).Select(i => new Linha($"Item {i}")).ToArray();

        _onePass = Build(data, withPageTotal: false);
        _twoPass = Build(data, withPageTotal: true);
    }

    private static Report Build(Linha[] data, bool withPageTotal)
    {
        var b = ReportBuilder.Create("Bench")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", data)
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 5));

        return withPageTotal
            ? b.PageFooter(f => f.Height(8).Text("{Page.Number} de {Page.Total}").At(0, 1).Size(80, 5)).Build()
            : b.Build();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> SinglePass() => (await _onePass.PaginateAsync()).Pages.Count;

    /// <summary>Page.Total forces a full second layout pass.</summary>
    [Benchmark]
    public async Task<int> TwoPasses() => (await _twoPass.PaginateAsync()).Pages.Count;
}
