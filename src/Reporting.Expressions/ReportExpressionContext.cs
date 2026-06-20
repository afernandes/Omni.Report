using System.Globalization;
using Reporting.Aggregates;

namespace Reporting.Expressions;

/// <summary>
/// Default mutable implementation of <see cref="IReportExpressionContext"/>. Holds the
/// current row, plus all rows of the current group and report scope so that <see cref="EvaluateAggregate"/>
/// can compute sums/averages/etc. without re-walking the data source.
/// </summary>
public sealed class ReportExpressionContext : IReportExpressionContext
{
    private readonly ExpressionEvaluator _evaluator;
    private readonly List<DictionaryLookup> _reportRows = [];
    private readonly List<DictionaryLookup> _groupRows = [];
    private readonly List<DictionaryLookup> _pageRows = [];
    /// <summary>When set, supersedes <see cref="_reportRows"/> for <see cref="AggregateScope.Report"/>
    /// so report-scoped aggregates yield the dataset grand total in <em>any</em> band — including
    /// those (ReportHeader, PageHeader) that render before the detail loop has accumulated rows.
    /// Null until <see cref="PrimeReportScope"/> runs, so the incremental-accumulation contract
    /// used by direct-context unit tests stays intact.</summary>
    private List<DictionaryLookup>? _reportScopeOverride;
    private readonly DictionaryLookup _fieldsLookup = new();
    /// <summary>Per-data-source current-row lookup. Populated by the paginator so qualified
    /// field references <c>Fields.SourceName.X</c> can resolve to <em>any</em> source's
    /// current record, not just the iterated one.</summary>
    private readonly Dictionary<string, DictionaryLookup> _sources = new(StringComparer.OrdinalIgnoreCase);
    // All rows of each named dataset, for cross-dataset Lookup/LookupSet (SSRS-style). Distinct from
    // _sources, which holds only the current row of each source for qualified field references.
    private readonly Dictionary<string, List<DictionaryLookup>> _datasets = new(StringComparer.OrdinalIgnoreCase);

    public ReportExpressionContext(ExpressionEvaluator? evaluator = null, CultureInfo? culture = null)
    {
        _evaluator = evaluator ?? new ExpressionEvaluator();
        Culture = culture ?? CultureInfo.GetCultureInfo("pt-BR");
        ParametersStore = new DictionaryLookup();
        VariablesStore = new DictionaryLookup();
        Now = DateTime.Now;
        UserName = Environment.UserName ?? "anonymous";
    }

    public DictionaryLookup ParametersStore { get; }
    public DictionaryLookup VariablesStore { get; }

    public IValueLookup Fields => _fieldsLookup;
    public IValueLookup Parameters => ParametersStore;
    public IValueLookup Variables => VariablesStore;

    public object? GroupKey { get; set; }
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public DateTime Now { get; set; }
    public DateTime Today => Now.Date;
    public string UserName { get; set; }
    public CultureInfo Culture { get; }

    /// <summary>Sets the field values for the current row and pushes a snapshot into the scope buffers.</summary>
    public void SetCurrentRow(IEnumerable<KeyValuePair<string, object?>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        // Reset and refill the live lookup
        foreach (var key in _fieldsLookup.Keys.ToArray())
        {
            _fieldsLookup.Remove(key);
        }
        var snapshot = new DictionaryLookup();
        foreach (var kv in fields)
        {
            _fieldsLookup.Set(kv.Key, kv.Value);
            snapshot.Set(kv.Key, kv.Value);
        }
        _reportRows.Add(snapshot);
        _groupRows.Add(snapshot);
        _pageRows.Add(snapshot);
    }

    /// <summary>Pushes a single calculated-field value into the live <see cref="Fields"/>
    /// lookup, additive — does NOT clear any existing field. Used by the paginator to
    /// inject RDL-style calculated fields (<c>&lt;CalculatedField&gt;</c>) after the raw
    /// row has been published, so subsequent expressions see <c>Fields.{CalcName}</c>.
    /// </summary>
    /// <remarks>
    /// Calculated fields can reference earlier calculated fields by name, provided the
    /// paginator evaluates them in declaration order. We don't enforce a cycle check
    /// here — the evaluator throws naturally on infinite recursion via NCalc's stack.
    /// </remarks>
    public void SetCalculatedField(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _fieldsLookup.Set(name, value);
    }

    /// <summary>Resets the group accumulator (called on group boundary).</summary>
    public void ResetGroup() => _groupRows.Clear();

    /// <summary>Resets the page accumulator (called on page break).</summary>
    public void ResetPage() => _pageRows.Clear();

    /// <summary>Resets all accumulators (called when re-running the report for two-pass paging).</summary>
    public void ResetAll()
    {
        _reportRows.Clear();
        _groupRows.Clear();
        _pageRows.Clear();
        _reportScopeOverride = null;
        foreach (var key in _fieldsLookup.Keys.ToArray())
        {
            _fieldsLookup.Remove(key);
        }
    }

    /// <summary>Pre-populates the Report aggregate scope with the full iteration row set so that
    /// report-scoped aggregates (e.g. <c>Sum(Fields.Total)</c> with no explicit scope) evaluate to
    /// the dataset grand total in any band — notably the ReportHeader and PageHeader, which render
    /// before the detail loop has committed any rows. This mirrors SSRS, where a report-scoped
    /// aggregate is the dataset total everywhere rather than a position-dependent running partial.
    /// The paginator calls this once per pass; direct-context callers that never prime keep the
    /// original incremental-history behaviour.</summary>
    public void PrimeReportScope(IEnumerable<IEnumerable<KeyValuePair<string, object?>>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var buffer = new List<DictionaryLookup>();
        foreach (var row in rows)
        {
            var snapshot = new DictionaryLookup();
            foreach (var kv in row)
            {
                snapshot.Set(kv.Key, kv.Value);
            }
            buffer.Add(snapshot);
        }
        _reportScopeOverride = buffer;
    }

    /// <summary>Replaces the live <see cref="Fields"/> values without touching the accumulators.
    /// Used internally by aggregate evaluation to walk historical rows, and externally by the
    /// paginator to probe row-derived expressions (e.g. group keys) without polluting aggregates.</summary>
    internal void SetCurrentRowNoSnapshot(DictionaryLookup row)
    {
        foreach (var key in _fieldsLookup.Keys.ToArray())
        {
            _fieldsLookup.Remove(key);
        }
        foreach (var k in row.Keys)
        {
            _fieldsLookup.Set(k, row[k]);
        }
    }

    /// <summary>Replaces the live <see cref="Fields"/> values from a key/value list — non-committing
    /// counterpart of <see cref="SetCurrentRow"/>.</summary>
    public void SetCurrentRowNoSnapshot(IEnumerable<KeyValuePair<string, object?>> row)
    {
        foreach (var key in _fieldsLookup.Keys.ToArray())
        {
            _fieldsLookup.Remove(key);
        }
        foreach (var kv in row)
        {
            _fieldsLookup.Set(kv.Key, kv.Value);
        }
    }

    /// <summary>Sets the current-row lookup of a named data source so qualified references
    /// (<c>Fields.SourceName.X</c>) can resolve. Call this whenever the paginator advances a
    /// data source (parent or child in a master-detail relationship).</summary>
    public void SetSourceCurrentRow(string sourceName, IEnumerable<KeyValuePair<string, object?>> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(fields);
        if (!_sources.TryGetValue(sourceName, out var lookup))
        {
            lookup = new DictionaryLookup();
            _sources[sourceName] = lookup;
        }
        else
        {
            foreach (var k in lookup.Keys.ToArray()) lookup.Remove(k);
        }
        foreach (var kv in fields) lookup.Set(kv.Key, kv.Value);
    }

    /// <summary>Removes the current-row record for a named source (e.g. when the source's
    /// enumeration ends and no row is in scope anymore).</summary>
    public void ClearSourceCurrentRow(string sourceName)
    {
        if (_sources.TryGetValue(sourceName, out var lookup))
        {
            foreach (var k in lookup.Keys.ToArray()) lookup.Remove(k);
        }
    }

    public IValueLookup? GetSource(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName)) return null;
        return _sources.TryGetValue(sourceName, out var lookup) ? lookup : null;
    }

    /// <inheritdoc/>
    /// <remarks>This is the master-detail "fallback" resolution: in a parent→child iteration,
    /// the paginator sets the live <see cref="Fields"/> to the child row, but users naturally
    /// drop parent fields into the detail band without qualifying them (Crystal/SSRS/FastReport
    /// designers all do this implicitly). We search the live Fields first — that's the iteration
    /// scope and therefore the most specific match — and only fall back to other sources when
    /// the field genuinely doesn't exist there.</remarks>
    public bool TryResolveUnqualifiedField(string fieldName, out object? value)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            value = null;
            return false;
        }
        if (_fieldsLookup.Contains(fieldName))
        {
            value = _fieldsLookup[fieldName];
            return true;
        }
        foreach (var lookup in _sources.Values)
        {
            if (lookup.Contains(fieldName))
            {
                value = lookup[fieldName];
                return true;
            }
        }
        value = null;
        return false;
    }

    public object? EvaluateAggregate(string function, string expression, AggregateScope scope)
    {
        var rows = scope switch
        {
            AggregateScope.Report => _reportScopeOverride ?? _reportRows,
            AggregateScope.Group => _groupRows,
            AggregateScope.Page => _pageRows,
            AggregateScope.Running => _groupRows,
            _ => _reportScopeOverride ?? _reportRows,
        };
        return AggregateCalculator.Calculate(function, expression, rows, _evaluator, this);
    }

    /// <summary>Registers all rows of a named dataset so cross-dataset <c>Lookup</c>/<c>LookupSet</c> can
    /// scan them. Snapshots each row (decoupled from the live source). Re-registering replaces.</summary>
    public void RegisterDataset(string name, IEnumerable<IEnumerable<KeyValuePair<string, object?>>> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);
        var buffer = new List<DictionaryLookup>();
        foreach (var row in rows)
        {
            var snapshot = new DictionaryLookup();
            foreach (var kv in row)
            {
                snapshot.Set(kv.Key, kv.Value);
            }
            buffer.Add(snapshot);
        }
        _datasets[name] = buffer;
    }

    public object? EvaluateLookup(object? source, string destExpression, string resultExpression, string datasetName, bool all)
    {
        if (!_datasets.TryGetValue(datasetName, out var rows) || rows.Count == 0)
        {
            return all ? Array.Empty<object?>() : null;
        }
        // Swap the live Fields with each target row to evaluate dest/result in its scope, then restore —
        // the same row-walking contract aggregates use, so it nests safely inside an outer evaluation.
        var live = _fieldsLookup.Keys.Select(k => new KeyValuePair<string, object?>(k, _fieldsLookup[k])).ToList();
        var matches = all ? new List<object?>() : null;
        try
        {
            foreach (var row in rows)
            {
                SetCurrentRowNoSnapshot(row);
                object? dest;
                try { dest = _evaluator.Evaluate(destExpression, this); }
                catch { continue; }
                if (!LookupKeyEquals(source, dest))
                {
                    continue;
                }
                object? result;
                try { result = _evaluator.Evaluate(resultExpression, this); }
                catch { result = null; }
                if (!all)
                {
                    return result;
                }
                matches!.Add(result);
            }
        }
        finally
        {
            SetCurrentRowNoSnapshot(live);
        }
        return all ? matches!.ToArray() : null;
    }

    // Lookup keys match on value equality first. The cross-type fallback is restricted to NUMERIC pairs
    // (number/number across int↔double, or number↔numeric-string) so an int id in one dataset matches a
    // string id in another — the documented case — WITHOUT bool/DateTime/etc. false-matching via ToString
    // (e.g. true vs "True", or two distinct dates that format alike). null never matches.
    private static bool LookupKeyEquals(object? a, object? b)
    {
        if (a is null || b is null)
        {
            return false;
        }
        if (Equals(a, b))
        {
            return true;
        }
        return TryAsLookupNumber(a, out var na) && TryAsLookupNumber(b, out var nb) && na == nb;
    }

    private static bool TryAsLookupNumber(object value, out decimal number)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case string s:
                return decimal.TryParse(s, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out number);
            default:
                number = 0m;
                return false;
        }
    }
}
