using Reporting.Common;
using Reporting.Layout.Primitives;
using Reporting.Paper;

namespace Reporting.Layout;

/// <summary>One physical page of a rendered report — a flat list of positioned primitives.</summary>
public sealed record RenderedPage(
    int PageNumber,
    PageSetup PageSetup,
    EquatableArray<LayoutPrimitive> Primitives);

/// <summary>The full output of the paginator: an ordered set of <see cref="RenderedPage"/>.</summary>
public sealed record RenderedReport(
    string Name,
    EquatableArray<RenderedPage> Pages)
{
    /// <summary>Number of pages produced. Zero when the report rendered nothing at all.</summary>
    public int PageCount => Pages.Count;
}
