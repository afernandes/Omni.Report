using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

// ─── Advanced RDL data regions ─────────────────────────────────────────────────────
//
// The richer RDL element kinds beyond the basic report items. Each is a sealed record with the
// minimum fields the RDL spec needs plus the OmniReport common shape (Id / Bounds / Style inherited
// from ReportElement), so a .repx/.rdl authored in SSRS or a third-party tool round-trips losslessly.
//
// Rendering is REAL (Reporting.Layout): Chart → ChartRenderer; Gauge/DataBar/Sparkline/Indicator →
// KpiRenderer; Tablix → TablixRenderer; Map → MapRenderer. Code (custom helpers) executes via the
// opt-in Roslyn resolver — RCE by design, disabled by default, trusted sources only. Adding fields
// stays additive — the wire format and the .repx schema don't change.

/// <summary>
/// RDL <c>Tablix</c> — unified Table + Matrix + List data region. The shape is a
/// nested grid of rows × columns where any axis can be statically defined OR group
/// dynamically (by an expression). The body cells hold child report items.
/// </summary>
/// <remarks>
/// Captures <see cref="RowGroups"/>/<see cref="ColumnGroups"/> + a flat list of <see cref="Cells"/>
/// (each cell carries its row/column indices + the child element). <c>TablixRenderer</c> lays out
/// the full grid (groups, subtotals, no-rows message); the band adapts to its grown height.
/// </remarks>
public sealed record TablixElement : ReportElement
{
    /// <summary>Dataset name that drives the row iteration — matches the parent
    /// <c>DataSourceDefinition.Name</c>.</summary>
    public string? DataSetName { get; init; }

    /// <summary>Hierarchical row groups (innermost → outermost). Empty for a pure
    /// matrix that pivots only on columns.</summary>
    public EquatableArray<TablixGroup> RowGroups { get; init; } = EquatableArray<TablixGroup>.Empty;

    /// <summary>Hierarchical column groups (innermost → outermost). Empty for a pure
    /// table that grows only vertically.</summary>
    public EquatableArray<TablixGroup> ColumnGroups { get; init; } = EquatableArray<TablixGroup>.Empty;

    /// <summary>Body cells — each one references the (row, column) coordinate and
    /// the child element (typically a TextBox). <c>TablixRenderer</c> iterates these to fill the grid.</summary>
    public EquatableArray<TablixCell> Cells { get; init; } = EquatableArray<TablixCell>.Empty;

    /// <summary>Optional relative column widths (weights). Empty → every column is equal. When
    /// set, each entry is a positive weight and the renderer distributes the element's width
    /// proportionally — e.g. <c>[1, 2, 1]</c> makes the middle column twice as wide as its
    /// neighbours. A partial list (or zero entries) falls back to the average weight per column.</summary>
    public EquatableArray<double> ColumnWidths { get; init; } = EquatableArray<double>.Empty;

    /// <summary>When <c>true</c>, the matrix renders a <b>subtotal row</b> at the end of each outer row
    /// group (summing the inner leaves per column) plus a <b>grand total</b> row at the bottom — SSRS-style
    /// group totals. No-op for a flat single-level row group beyond the grand total. Default <c>false</c>.
    /// The extra rows grow the element's rendered height (the band adapts).</summary>
    public bool RowSubtotals { get; init; }

    /// <summary>When <c>true</c>, the matrix renders a <b>subtotal column</b> after each outer column group
    /// (summing the inner leaves per row) plus a <b>grand total</b> column at the right — the column-axis
    /// mirror of <see cref="RowSubtotals"/>. No-op for a flat single-level column group beyond the grand
    /// total. Default <c>false</c>. The extra columns shrink the per-column width (the element width is
    /// fixed). Combines with <see cref="RowSubtotals"/> — the grand-total row × grand-total column cell is
    /// the overall sum.</summary>
    public bool ColumnSubtotals { get; init; }

    /// <summary>Label for a <b>group subtotal</b> row/column. The placeholder <c>{0}</c> is replaced by the
    /// group's value (e.g. <c>"Total {0}"</c> → "Total Sul"). When null, the default <c>"Total {0}"</c> is
    /// used. Applies to both row and column subtotals.</summary>
    public string? SubtotalLabel { get; init; }

    /// <summary>Label for the <b>grand total</b> row/column. When null, the default <c>"Total geral"</c> is
    /// used. Applies to both row and column grand totals.</summary>
    public string? GrandTotalLabel { get; init; }

    /// <summary>Message rendered (centred, in place of the grid) when the bound dataset yields no rows —
    /// RDL <c>&lt;NoRowsMessage&gt;</c>. When null, an empty dataset renders nothing.</summary>
    public string? NoRowsMessage { get; init; }

    /// <summary>When the matrix is taller than the page, it splits across pages by row (SSRS/XtraReports style)
    /// and reprints the column header at the top of each continuation page. Default <c>true</c>. Set
    /// <c>false</c> to print the column header only on the first page. Has no effect unless the matrix paginates.</summary>
    [PropertyGrid(Category = "Paginação", Order = 1, Label = "Repetir cabeçalho de coluna")]
    public bool RepeatColumnHeaders { get; init; } = true;

    /// <summary>Keeps the whole matrix together on one page instead of splitting it across pages — the opt-out of
    /// row-level pagination (DevExpress <c>ContentSplitting</c> / SSRS <c>KeepTogether</c>). Default <c>false</c>
    /// (a tall matrix paginates). When <c>true</c>, a matrix taller than the page overflows rather than splitting.</summary>
    [PropertyGrid(Category = "Paginação", Order = 2, Label = "Manter junto (não paginar)")]
    public bool KeepTogether { get; init; }

    /// <summary>Minimum width per column. Default <see cref="Unit.Zero"/> keeps the classic behaviour: every column
    /// shares the declared width equally (a wide matrix squeezes its columns thinner). When set, each column is at
    /// least this wide and, if the columns no longer fit the declared width, the matrix paginates HORIZONTALLY —
    /// splitting its columns across page tiles (SSRS "Across then Down"), repeating the row headers on each tile.</summary>
    [PropertyGrid(Category = "Paginação", Order = 3, Label = "Largura mín. de coluna")]
    public Unit MinColumnWidth { get; init; } = Unit.Zero;
}
