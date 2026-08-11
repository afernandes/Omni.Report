using BenchmarkDotNet.Attributes;
using Reporting.DataSources.Enumerable;

namespace Reporting.Benchmarks;

/// <summary>
/// Throughput of the in-memory data source, which every code-first report goes through.
/// </summary>
/// <remarks>
/// <para><c>EnumerableDataSource&lt;T&gt;</c> projects each POCO into an <c>IReportRecord</c> via reflection,
/// once per row. That cost multiplies by the row count before any layout work happens, so it sets the floor
/// for how fast a report can possibly render.</para>
///
/// <para>This exists because the budget used to live in a unit test as a <c>Stopwatch</c> assertion that ran
/// only outside CI. A stopwatch around one cold run measures JIT and warmup as much as the code, and a
/// threshold that fires only on the developer's machine turns the local suite red for reasons unrelated to
/// the change. BenchmarkDotNet controls for warmup and reports a distribution, which is what a performance
/// budget actually needs.</para>
///
/// <para>Run it with:
/// <c>dotnet run -c Release --project benchmarks/Reporting.Benchmarks -- --filter "*DataSource*"</c></para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
public class DataSourceBenchmarks
{
    /// <summary>Row counts spanning a small report up to a bulk export.</summary>
    [Params(1_000, 10_000, 100_000)]
    public int Rows { get; set; }

    private IEnumerable<Venda> _items = null!;

    /// <summary>Builds the lazy source sequence. Kept lazy so the benchmark measures the projection,
    /// not the cost of allocating the input list.</summary>
    [GlobalSetup]
    public void Setup()
        => _items = Enumerable.Range(0, Rows).Select(i => new Venda("c" + (i % 50), "p", i % 7, 1.99m));

    /// <summary>Full enumeration reading one field per row — the shape the paginator drives.</summary>
    [Benchmark]
    public async Task<int> EnumerateAndReadField()
    {
        var ds = new EnumerableDataSource<Venda>("V", _items);
        int count = 0;
        await foreach (var r in ds.ReadAsync())
        {
            _ = r["Total"];
            count++;
        }
        return count;
    }

    /// <summary>A row of the synthetic dataset.</summary>
    /// <param name="Cliente">Grouping key, repeated across rows.</param>
    /// <param name="Produto">Constant text field.</param>
    /// <param name="Quantidade">Integer field.</param>
    /// <param name="Total">Decimal field — the one the benchmark reads.</param>
    public sealed record Venda(string Cliente, string Produto, int Quantidade, decimal Total);
}
