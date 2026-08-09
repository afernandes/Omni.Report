using BenchmarkDotNet.Attributes;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Output.Excel;
using Reporting.Output.Pdf;

namespace Reporting.Benchmarks;

/// <summary>
/// Export cost with pagination already done, so the numbers isolate the exporter rather than the engine.
/// PDF is vector output via Skia; Excel builds a text grid and hands it to ClosedXML — two very different
/// shapes, and the pair is what a host actually offers a user.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
public class ExportBenchmarks
{
    private sealed record Linha(string Nome, decimal Valor);

    [Params(5_000)]
    public int Rows { get; set; }

    private RenderedReport _rendered = null!;
    private readonly SkiaPdfExporter _pdf = new();
    private readonly ExcelExporter _excel = new();

    [GlobalSetup]
    public void Setup()
    {
        var data = Enumerable.Range(0, Rows).Select(i => new Linha($"Item {i}", i * 1.25m)).ToArray();
        var report = ReportBuilder.Create("Bench")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", data)
            .Detail(d => d.Height(6)
                .Text("{Fields.Nome}").At(0, 0).Size(80, 5)
                .Text("{Fields.Valor:C}").At(84, 0).Size(40, 5).AlignRight())
            .Build();

        _rendered = report.PaginateAsync().GetAwaiter().GetResult(); // setup, not measured
    }

    [Benchmark(Baseline = true)]
    public int ExportPdf() => _pdf.ExportToBytes(_rendered).Length;

    [Benchmark]
    public int ExportExcel() => _excel.ExportToBytes(_rendered).Length;
}
