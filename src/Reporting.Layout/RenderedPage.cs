using Reporting.Common;
using Reporting.Layout.Primitives;
using Reporting.Paper;

namespace Reporting.Layout;

/// <summary>One physical page of a rendered report — a flat list of positioned primitives.</summary>
public sealed record RenderedPage(
    int PageNumber,
    PageSetup PageSetup,
    EquatableArray<LayoutPrimitive> Primitives);
