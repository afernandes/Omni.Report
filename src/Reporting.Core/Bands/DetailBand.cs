using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Bands;

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
    /// <summary>Always <see cref="BandKind.Detail"/>.</summary>
    public BandKind Kind => BandKind.Detail;

    /// <summary>A zero-height detail band with no elements.</summary>
    public static readonly DetailBand Empty = new(Unit.Zero, EquatableArray<ReportElement>.Empty);
}
