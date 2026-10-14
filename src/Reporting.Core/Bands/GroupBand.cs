using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Bands;

/// <summary>A grouping band — emits header before the first row of the group and footer after the last.</summary>
/// <remarks>
/// <para>RDL adds <see cref="PageBreak"/> as the modern unified break control; the legacy
/// <see cref="NewPageBefore"/>/<see cref="NewPageAfter"/> bools are kept for backwards
/// compatibility and unified at paginator time (a non-<see cref="PageBreak.None"/> value
/// overrides the bools).</para>
///
/// <para><see cref="FilterExpression"/>/<see cref="SortExpressions"/> apply to group
/// instances; sort orders which group instance renders first, filter omits groups whose
/// rows don't pass the predicate.</para>
/// </remarks>
public sealed record GroupBand(
    string Name,
    string GroupExpression,
    ReportBand? Header = null,
    ReportBand? Footer = null,
    bool KeepTogether = false,
    bool NewPageBefore = false,
    bool NewPageAfter = false,
    bool RepeatHeaderOnNewPage = false,
    bool Visible = true,
    string? VisibleExpression = null,
    PageBreak PageBreak = PageBreak.None,
    string? FilterExpression = null,
    EquatableArray<SortDescriptor> SortExpressions = default,
    EquatableArray<Parameters.ReportVariable> Variables = default)
    : IBand
{
    /// <summary>Always <see cref="BandKind.GroupHeader"/>; the footer travels with the same band.</summary>
    public BandKind Kind => BandKind.GroupHeader;

    /// <summary>Combined height of the header and footer sections, either of which may be absent.</summary>
    public Unit Height => (Header?.Height ?? Unit.Zero) + (Footer?.Height ?? Unit.Zero);

    /// <summary>Header elements followed by footer elements. Exposed to satisfy <see cref="IBand"/>;
    /// the paginator draws the two sections separately.</summary>
    public EquatableArray<ReportElement> Elements
        => new(((Header?.Elements ?? EquatableArray<ReportElement>.Empty))
              .Concat(Footer?.Elements ?? EquatableArray<ReportElement>.Empty));

    /// <summary>Computes the effective page-break, unifying the legacy bool pair and the
    /// modern enum: enum wins if non-None; else falls back to a synthesized value from
    /// the bools (which matches the pre-RDL behaviour).</summary>
    public PageBreak EffectivePageBreak()
    {
        if (PageBreak != PageBreak.None) return PageBreak;
        var before = NewPageBefore;
        var after = NewPageAfter;
        return (before, after) switch
        {
            (true, true) => PageBreak.StartAndEnd,
            (true, false) => PageBreak.Start,
            (false, true) => PageBreak.End,
            _ => PageBreak.None,
        };
    }
}
