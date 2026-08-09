using BenchmarkDotNet.Attributes;
using Reporting.Expressions;

namespace Reporting.Benchmarks;

/// <summary>
/// Expression throughput. Every field on every band instance goes through the evaluator, so this is the
/// hottest inner loop in the engine: a 100k-row report with 3 bound fields evaluates 300k expressions.
///
/// <para>The compiler caches parsed expressions in a <c>ConcurrentDictionary</c> (that cache is exactly why
/// <c>ReportPaginator</c> shares one compiler across runs). <see cref="EvaluateCached"/> measures the hot
/// path; <see cref="ParseAndEvaluateUncached"/> measures a cache miss, so the gap shows what the cache buys.</para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
public class ExpressionBenchmarks
{
    private ExpressionEvaluator _evaluator = null!;
    private TestContext _context = null!;
    private int _uniqueCounter;

    [GlobalSetup]
    public void Setup()
    {
        _evaluator = new ExpressionEvaluator(new ExpressionCompiler());
        _context = new TestContext();
        _evaluator.Evaluate("Fields.Total * 2", _context); // warm the parse cache
    }

    /// <summary>The steady state: same expression text, already parsed.</summary>
    [Benchmark(Baseline = true)]
    public object? EvaluateCached() => _evaluator.Evaluate("Fields.Total * 2", _context);

    /// <summary>A cache miss every call — the cost the cache is avoiding.</summary>
    [Benchmark]
    public object? ParseAndEvaluateUncached()
        => _evaluator.Evaluate($"Fields.Total * {++_uniqueCounter}", _context);

    /// <summary>Minimal context: only what the evaluator touches for these expressions.</summary>
    private sealed class TestContext : IReportExpressionContext
    {
        private readonly Lookup _fields = new(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Total"] = 123.45m,
            ["Cliente"] = "Fulano",
        });

        public IValueLookup Fields => _fields;
        public IValueLookup Parameters { get; } = new Lookup(new Dictionary<string, object?>());
        public IValueLookup Variables { get; } = new Lookup(new Dictionary<string, object?>());
        public object? GroupKey => null;
        public int PageNumber => 1;
        public int TotalPages => 1;
        public DateTime Now { get; } = new(2026, 1, 1);
        public DateTime Today { get; } = new(2026, 1, 1);
        public string UserName => "bench";
        public string ReportName => "bench";
        public System.Globalization.CultureInfo Culture { get; } = System.Globalization.CultureInfo.InvariantCulture;

        public object? EvaluateAggregate(string function, string expression, Reporting.Aggregates.AggregateScope scope) => null;
        public object? EvaluateLookup(object? source, string dest, string result, string dataset, bool all) => null;
        public object? EvaluatePositional(string function, string expression, Reporting.Aggregates.AggregateScope scope) => null;
        public IValueLookup? GetSource(string sourceName) => null;
        public object? GetReportItem(string name) => null;
        public void SetReportItem(string name, object? value) { }

        public bool TryResolveUnqualifiedField(string fieldName, out object? value)
        {
            if (_fields.Contains(fieldName)) { value = _fields[fieldName]; return true; }
            value = null;
            return false;
        }

        private sealed class Lookup(Dictionary<string, object?> map) : IValueLookup
        {
            public object? this[string key] => map.TryGetValue(key, out var v) ? v : null;
            public bool Contains(string key) => map.ContainsKey(key);
            public IEnumerable<string> Keys => map.Keys;
        }
    }
}
