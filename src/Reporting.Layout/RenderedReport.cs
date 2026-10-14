using Reporting.Common;
using Reporting.Layout.Primitives;
using Reporting.Paper;

namespace Reporting.Layout;

/// <summary>The full output of the paginator: an ordered set of <see cref="RenderedPage"/>.</summary>
public sealed record RenderedReport(
    string Name,
    EquatableArray<RenderedPage> Pages)
{
    /// <summary>Unsupported definition features encountered by pagination, including nested reports.</summary>
    public EquatableArray<PaginationDiagnostic> Diagnostics { get; init; } = EquatableArray<PaginationDiagnostic>.Empty;

    /// <summary>Number of pages produced. Zero when the report rendered nothing at all.</summary>
    public int PageCount => Pages.Count;
}
