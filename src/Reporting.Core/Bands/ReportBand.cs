using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Bands;

/// <summary>A non-grouping band (report/page header or footer, or a section attached to a group).</summary>
/// <remarks>
/// <para>The <see cref="PageBreak"/> property is the RDL-idiomatic page-break control;
/// <see cref="PrintOnFirstPage"/>/<see cref="PrintOnLastPage"/> remain for the legacy
/// header/footer-suppression semantics that don't map cleanly to RDL's <c>PageBreak</c>.</para>
/// </remarks>
public sealed record ReportBand(
    BandKind Kind,
    Unit Height,
    EquatableArray<ReportElement> Elements,
    bool Visible = true,
    string? VisibleExpression = null,
    bool PrintOnFirstPage = true,
    bool PrintOnLastPage = true,
    PageBreak PageBreak = PageBreak.None)
    : IBand
{
    /// <summary>A zero-height band of the given kind with no elements — a placeholder that renders nothing.</summary>
    public static ReportBand Empty(BandKind kind) => new(kind, Unit.Zero, EquatableArray<ReportElement>.Empty);
}
