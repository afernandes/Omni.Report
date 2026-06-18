namespace Reporting.Data;

/// <summary>RDL-compatible sort descriptor. Mirrors RDL's <c>&lt;SortBy&gt;</c> with an
/// <c>&lt;Expression&gt;</c> + <c>Direction</c> attribute.</summary>
/// <remarks>
/// <para>Multiple descriptors form a composite sort — the paginator stable-sorts rows
/// first by the first descriptor, then ties broken by the next, and so on. Expressions
/// are evaluated once per row before sorting; the comparator falls back to
/// <see cref="System.Collections.Comparer.Default"/> when types match, otherwise compares
/// by <c>ToString()</c>.</para>
///
/// <para>Used at three levels:
/// <list type="bullet">
/// <item>Data source (sorts before any region consumes rows).</item>
/// <item>Data region (Detail, SubDetail) — local sort applied after the data source sort.</item>
/// <item>Group — orders group instances; the rows inside each group keep the inner sort.</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Expression">Expression evaluated per row.</param>
/// <param name="Direction">Ascending (default) or Descending.</param>
public sealed record SortDescriptor(string Expression, SortDirection Direction = SortDirection.Ascending);

public enum SortDirection
{
    Ascending,
    Descending,
}
