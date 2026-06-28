using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Bands;

/// <summary>Identifies the role a band plays in the report layout (header, footer, group, or detail).</summary>
public enum BandKind
{
    ReportHeader,
    PageHeader,
    GroupHeader,
    Detail,
    GroupFooter,
    PageFooter,
    ReportFooter,
}

/// <summary>Common surface for any band.</summary>
public interface IBand
{
    BandKind Kind { get; }
    Unit Height { get; }
    bool Visible { get; }
    string? VisibleExpression { get; }
    EquatableArray<ReportElement> Elements { get; }
}

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
    public static ReportBand Empty(BandKind kind) => new(kind, Unit.Zero, EquatableArray<ReportElement>.Empty);
}

/// <summary>The detail band — repeated once per data row.</summary>
/// <remarks>
/// <para>RDL-compatible additions:
/// <list type="bullet">
/// <item><see cref="NoRowsMessage"/> — text displayed centered when the bound data source is
/// empty. Mirrors RDL <c>&lt;NoRows&gt;</c>.</item>
/// <item><see cref="FilterExpression"/> — boolean expression evaluated per row; rows
/// where it is false are skipped. Mirrors RDL <c>&lt;Filters&gt;</c>.</item>
/// <item><see cref="SortExpressions"/> — stable sort applied to rows before iteration.
/// Mirrors RDL <c>&lt;SortExpressions&gt;</c>.</item>
/// <item><see cref="PageBreak"/> — break around the band's emission.</item>
/// </list>
/// </para>
/// </remarks>
public sealed record DetailBand(
    Unit Height,
    EquatableArray<ReportElement> Elements,
    bool Visible = true,
    string? VisibleExpression = null,
    bool CanGrow = false,
    bool CanShrink = false,
    EquatableArray<SubDetailBand> SubDetails = default,
    string? NoRowsMessage = null,
    string? FilterExpression = null,
    EquatableArray<SortDescriptor> SortExpressions = default,
    PageBreak PageBreak = PageBreak.None,
    // Dataset that drives the detail loop. null (default) → falls back to PrimaryDataSource then the first
    // declared source (historical behaviour unchanged). Unlike SubDetailBand.DataMember (relation-or-source),
    // this is a plain dataset name; if it matches no DataSourceDefinition the loop still iterates that
    // registered source, just without its relations/calculated fields/filter.
    string? DataSetName = null)
    : IBand
{
    public BandKind Kind => BandKind.Detail;

    public static readonly DetailBand Empty = new(Unit.Zero, EquatableArray<ReportElement>.Empty);
}

/// <summary>A nested detail band that iterates a child data source for each parent row.
/// Matches the DevExpress <c>DetailReportBand</c> / FastReport sub-band concept.</summary>
/// <param name="Name">Logical name (used by the designer + serialization).</param>
/// <param name="DataMember">Either a relation name (master-detail) or a data source name
/// (free-form sub-iteration). Resolution order: relation declared on parent → registered
/// data source → empty (no rows).</param>
/// <param name="Height">Sub-detail row height — repeated per child row.</param>
/// <param name="Elements">Elements drawn per child row.</param>
/// <param name="Header">Optional header band rendered once before the first child row.</param>
/// <param name="Footer">Optional footer band rendered once after the last child row.</param>
/// <param name="Visible">Whether the sub-band renders at all.</param>
/// <param name="VisibleExpression">Optional expression — when present and evaluates to <c>false</c>,
/// the sub-band is skipped for that parent row.</param>
/// <param name="PrintIfEmpty">When true, renders the header/footer even when the child source
/// has no matching rows. Default false (matches Crystal/DevExpress).</param>
/// <param name="NoRowsMessage">RDL-style message shown when the child source has zero rows.</param>
/// <param name="FilterExpression">Boolean expression — false rows skipped.</param>
/// <param name="SortExpressions">Local sort applied before iteration.</param>
public sealed record SubDetailBand(
    string Name,
    string DataMember,
    Unit Height,
    EquatableArray<ReportElement> Elements,
    ReportBand? Header = null,
    ReportBand? Footer = null,
    bool Visible = true,
    string? VisibleExpression = null,
    bool PrintIfEmpty = false,
    string? NoRowsMessage = null,
    string? FilterExpression = null,
    EquatableArray<SortDescriptor> SortExpressions = default)
{
    public static SubDetailBand Empty(string name, string dataMember)
        => new(name, dataMember, Unit.Zero, EquatableArray<ReportElement>.Empty);
}

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
    public BandKind Kind => BandKind.GroupHeader;
    public Unit Height => (Header?.Height ?? Unit.Zero) + (Footer?.Height ?? Unit.Zero);
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
            (true, true)   => PageBreak.StartAndEnd,
            (true, false)  => PageBreak.Start,
            (false, true)  => PageBreak.End,
            _              => PageBreak.None,
        };
    }
}
