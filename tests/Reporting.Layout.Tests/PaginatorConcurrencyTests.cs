using System.Runtime.CompilerServices;
using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// Guards that a SINGLE <see cref="ReportPaginator"/> instance can serve concurrent reports without
/// leaking state between them. This is not academic: <c>AddReporting</c> registers the paginator as a
/// SINGLETON (<c>ReportingBuilder.AddSingleton&lt;IReportPaginator, ReportPaginator&gt;</c>), so in an
/// ASP.NET Core host every simultaneous HTTP request shares one instance.
/// </summary>
/// <remarks>
/// <para>The tests are deterministic, not probabilistic. Each report's data source parks at a shared
/// rendezvous and only yields once ALL participants have arrived. Because the paginator applies the
/// request's <c>CodeFunctionResolver</c> BEFORE materialising data, the barrier guarantees every task
/// has published its resolver before any of them evaluates an expression — so a shared mutable field
/// would be observed with the wrong (last-writer) value by every task but one, every run.</para>
/// <para>Both were confirmed to fail against the pre-fix code, each with a distinct symptom:
/// the resolver test rendered another request's marker (report 0 printing <c>marca-7</c>), and the
/// repeat-header test threw <c>InvalidOperationException: Collection was modified</c> from the
/// <c>foreach</c> over the shared list in <c>BreakPage</c>.</para>
/// <para>The grouped report must set <c>RepeatHeaderOnNewPage</c> (off by default) — without it the
/// repeat-header list is never populated and the test passes vacuously.</para>
/// </remarks>
public class PaginatorConcurrencyTests
{
    private const int Participants = 8;

    [Fact]
    public async Task Concurrent_reports_do_not_leak_the_Code_resolver_across_requests()
    {
        var paginator = new ReportPaginator(); // ONE instance, as the DI singleton would be
        var gate = new Rendezvous(Participants);

        // Task i renders `Code.Marca()`, whose resolver returns the caller's own marker.
        var tasks = Enumerable.Range(0, Participants).Select(async i =>
        {
            var request = new PaginationRequest
            {
                Definition = CodeMarkerReport($"relatorio-{i}"),
                DataSources = GatedRegistry(gate),
                CodeFunctionResolver = (_, _) => $"marca-{i}",
            };
            var rendered = await paginator.PaginateAsync(request);
            return (Expected: $"marca-{i}", Actual: TextOf(rendered));
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (expected, actual) in results)
        {
            actual.Should().Be(expected,
                "each report must see ITS OWN Code resolver — a shared evaluator would serve another request's code");
        }
    }

    [Fact]
    public async Task Concurrent_reports_with_repeating_group_headers_stay_intact()
    {
        // Exercises the other piece of per-run state: the repeat-header list, mutated by
        // OpenGroup/CloseGroup and replayed on every page break. Concurrent mutation of a plain
        // List<T> corrupts it (wrong headers replayed, or an outright collection-modified throw).
        var paginator = new ReportPaginator();
        var gate = new Rendezvous(Participants);

        var tasks = Enumerable.Range(0, Participants).Select(async i =>
        {
            var request = new PaginationRequest
            {
                Definition = RepeatingHeaderReport(),
                DataSources = GatedRegistry(gate, TestData.ManyRows(120)),
            };
            return await paginator.PaginateAsync(request);
        });

        var reports = await Task.WhenAll(tasks);

        // Every run saw identical input, so every run must produce byte-identical output. Comparing the
        // full rendered text (not just the page count) is what catches a group header replayed on the
        // wrong page — the visible symptom of a concurrently-mutated repeat-header list.
        reports.Select(TextOf).Distinct().Should().ContainSingle(
            "identical concurrent runs must render identically — divergence means shared state bled between them");
        reports.Select(r => r.Pages.Count).Distinct().Should().ContainSingle();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Async barrier: releases only once <c>participants</c> callers have arrived.</summary>
    private sealed class Rendezvous(int participants)
    {
        // Never let a missing participant hang the CI run: if one task dies before reaching the gate,
        // the others surface a TimeoutException (a red test) instead of blocking the agent forever.
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public Task SignalAndWaitAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= participants)
            {
                _gate.TrySetResult();
            }
            return _gate.Task.WaitAsync(Timeout);
        }
    }

    /// <summary>Data source that parks at the rendezvous before yielding its first record, so all
    /// concurrent paginations reach the evaluation phase together.</summary>
    private sealed class GatedDataSource(string name, Rendezvous gate, IReadOnlyList<Venda> rows) : IReportDataSource
    {
        public string Name => name;

        public IReportRecordSchema Schema { get; } = new ReportRecordSchema(
        [
            new ReportField("Cliente", typeof(string)),
            new ReportField("Produto", typeof(string)),
            new ReportField("Total", typeof(decimal)),
        ]);

        public async IAsyncEnumerable<IReportRecord> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await gate.SignalAndWaitAsync().ConfigureAwait(false);
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new DictionaryRecord(Schema, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Cliente"] = row.Cliente,
                    ["Produto"] = row.Produto,
                    ["Total"] = row.Total,
                });
            }
        }
    }

    private static DataSourceRegistry GatedRegistry(Rendezvous gate, IReadOnlyList<Venda>? rows = null)
    {
        var registry = new DataSourceRegistry();
        registry.Register(new GatedDataSource("Vendas", gate, rows ?? TestData.ThreeRows()));
        return registry;
    }

    /// <summary>Grouped report whose group header REPRINTS on every continuation page — the only
    /// configuration that populates the paginator's repeat-header list (default is off).</summary>
    private static ReportDefinition RepeatingHeaderReport()
    {
        var def = TestData.GroupedReport();
        return def with
        {
            Groups = EquatableArray.Create(def.Groups[0] with { RepeatHeaderOnNewPage = true }),
        };
    }

    /// <summary>Single-row report whose only element renders <c>Code.Marca()</c>.</summary>
    private static ReportDefinition CodeMarkerReport(string name) =>
        ReportDefinition.Empty(name) with
        {
            DataSources = EquatableArray.Create(new Reporting.Data.DataSourceDefinition("Vendas")),
            ReportHeader = new ReportBand(
                BandKind.ReportHeader,
                20.Mm(),
                EquatableArray.Create<ReportElement>(
                    new TextBoxElement
                    {
                        Id = "marca",
                        Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 10.Mm()),
                        Expression = "Code.Marca()",
                    })),
        };

    private static string TextOf(RenderedReport report) => string.Concat(
        report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text));
}
