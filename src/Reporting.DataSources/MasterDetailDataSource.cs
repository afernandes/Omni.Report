using System.Runtime.CompilerServices;

namespace Reporting.DataSources;

/// <summary>
/// A <em>filtered view</em> of a child data source that emits only the rows whose
/// <see cref="ChildField"/> equals a given parent value. Used to model classic
/// master-detail relationships (one customer → many orders, one order → many lines).
/// </summary>
/// <remarks>
/// <para>The filter value is supplied per-iteration via <see cref="WithParentValue(object?)"/>
/// — this lets the paginator iterate the parent source, and for each parent row drive a
/// fresh enumeration of the child source bound to that parent's key.</para>
///
/// <para>Implementation is simple-and-correct: it pulls every child row and filters in
/// memory. For very large child sources prefer pushing the predicate down to SQL via a
/// parameterized query — this class is a fallback for non-SQL sources (e.g. enumerables,
/// REST responses) or when convenience trumps throughput.</para>
/// </remarks>
public sealed class MasterDetailDataSource : IReportDataSource
{
    private readonly IReportDataSource _child;
    private readonly string _childField;
    private object? _parentValue;

    public MasterDetailDataSource(string name, IReportDataSource child, string childField)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentException.ThrowIfNullOrWhiteSpace(childField);
        Name = name;
        _child = child;
        _childField = childField;
        ChildField = childField;
    }

    public string Name { get; }
    public IReportRecordSchema Schema => _child.Schema;
    public string ChildField { get; }

    /// <summary>Rebinds the filter to a new parent key. The next <see cref="ReadAsync"/> call
    /// will only emit children whose <see cref="ChildField"/> equals <paramref name="parentValue"/>.</summary>
    public MasterDetailDataSource WithParentValue(object? parentValue)
    {
        _parentValue = parentValue;
        return this;
    }

    public async IAsyncEnumerable<IReportRecord> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Snapshot the filter value at iteration start — concurrent re-binding via
        // WithParentValue should not affect an already-running enumeration.
        var key = _parentValue;
        await foreach (var record in _child.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actual = record[_childField];
            if (KeysMatch(key, actual))
            {
                yield return record;
            }
        }
    }

    /// <summary>Equality with a few common-sense coercions: numbers compare by value
    /// regardless of int vs long vs decimal; string compare is ordinal; null matches null.</summary>
    private static bool KeysMatch(object? a, object? b)
    {
        if (a is null) return b is null;
        if (b is null) return false;
        if (a.Equals(b)) return true;
        // Try numeric coercion.
        if (a is IConvertible && b is IConvertible &&
            (IsNumber(a) && IsNumber(b)))
        {
            try { return Convert.ToDecimal(a) == Convert.ToDecimal(b); }
            catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException) { /* not decimal-comparable → fall through to string compare */ }
        }
        // Try string compare (handles e.g. Guid → string).
        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static bool IsNumber(object o) => o is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
