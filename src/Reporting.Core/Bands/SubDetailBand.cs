using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Bands;

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
    /// <summary>A zero-height sub-detail band bound to <paramref name="dataMember"/>, with no elements.</summary>
    public static SubDetailBand Empty(string name, string dataMember)
        => new(name, dataMember, Unit.Zero, EquatableArray<ReportElement>.Empty);
}
