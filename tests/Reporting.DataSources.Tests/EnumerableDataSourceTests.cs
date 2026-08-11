using FluentAssertions;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Xunit;

namespace Reporting.DataSources.Tests;

public sealed record Venda(string Cliente, string Produto, int Quantidade, decimal Preco)
{
    public decimal Total => Quantidade * Preco;
}

public class EnumerableDataSourceTests
{
    private static IReadOnlyList<Venda> Sample() => new[]
    {
        new Venda("Ana", "Caneta", 10, 2.50m),
        new Venda("Ana", "Caderno", 1, 27.40m),
        new Venda("Beto", "Caneta", 3, 2.50m),
    };

    [Fact]
    public void Exposes_schema_from_properties()
    {
        var ds = new EnumerableDataSource<Venda>("Vendas", Sample());
        ds.Name.Should().Be("Vendas");
        ds.Schema.Fields.Select(f => f.Name).Should().BeEquivalentTo(
            ["Cliente", "Produto", "Quantidade", "Preco", "Total"]);
        ds.Schema.IndexOf("Total").Should().BeGreaterThanOrEqualTo(0);
        ds.Schema.IndexOf("missing").Should().Be(-1);
    }

    [Fact]
    public async Task Iterates_records_and_reads_by_name()
    {
        var ds = new EnumerableDataSource<Venda>("Vendas", Sample());
        var records = new List<IReportRecord>();
        await foreach (var r in ds.ReadAsync())
        {
            records.Add(r);
        }
        records.Should().HaveCount(3);
        records[0]["Cliente"].Should().Be("Ana");
        records[0]["Produto"].Should().Be("Caneta");
        records[0]["Total"].Should().Be(25.0m);
    }

    [Fact]
    public async Task Reads_by_ordinal()
    {
        var ds = new EnumerableDataSource<Venda>("Vendas", Sample());
        await using var en = ds.ReadAsync().GetAsyncEnumerator();
        (await en.MoveNextAsync()).Should().BeTrue();
        en.Current[0].Should().Be("Ana");
    }

    [Fact]
    public async Task Records_export_key_value_pairs()
    {
        var ds = new EnumerableDataSource<Venda>("Vendas", Sample());
        var first = (await ds.ReadAsync().GetAsyncEnumerator().NextAsync())!;
        var kvs = first.ToKeyValuePairs().ToDictionary(p => p.Key, p => p.Value);
        kvs["Quantidade"].Should().Be(10);
        kvs["Preco"].Should().Be(2.50m);
    }

    [Fact]
    public async Task Unknown_field_yields_null()
    {
        var ds = new EnumerableDataSource<Venda>("Vendas", Sample());
        var first = (await ds.ReadAsync().GetAsyncEnumerator().NextAsync())!;
        first["nonexistent"].Should().BeNull();
    }

    /// <summary>
    /// 100 mil registros iteram por completo, com os campos legíveis do início ao fim.
    /// </summary>
    /// <remarks>
    /// Este teste tinha uma asserção de wall-clock (&lt; 2 s) que rodava <b>apenas fora do CI</b>, para
    /// escapar da variância dos runners compartilhados. O desenho estava invertido: dispensava o runner
    /// e cobrava o orçamento justamente da máquina do dev, que costuma estar no meio de um build — a
    /// suíte ficava vermelha localmente e verde no CI, o pior dos dois mundos. Um <c>Stopwatch</c> em
    /// volta de uma execução fria também não controla JIT nem warmup, então nunca mediu o que dizia
    /// medir. O orçamento passou para <c>DataSourceBenchmarks</c>, em <c>benchmarks/</c>, onde há
    /// instrumentação para isso. Mesmo tratamento dado ao teste equivalente do CodeFirst no #233 —
    /// este era o último da classe.
    /// </remarks>
    [Fact]
    public async Task Iterates_a_large_collection_end_to_end()
    {
        var items = System.Linq.Enumerable.Range(0, 100_000)
            .Select(i => new Venda("c" + (i % 50), "p", i % 7, 1.99m));
        var ds = new EnumerableDataSource<Venda>("V", items);

        int count = 0;
        await foreach (var r in ds.ReadAsync())
        {
            // Lê um campo em toda linha: uma fonte que quebrasse a projeção no meio da sequência
            // passaria numa simples contagem.
            r["Total"].Should().NotBeNull();
            count++;
        }

        count.Should().Be(100_000);
    }
}

internal static class AsyncEnumeratorExtensions
{
    public static async Task<T?> NextAsync<T>(this IAsyncEnumerator<T> en)
        => await en.MoveNextAsync() ? en.Current : default;
}
