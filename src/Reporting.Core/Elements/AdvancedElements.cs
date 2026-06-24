using Reporting.Common;
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
    public bool RepeatColumnHeaders { get; init; } = true;

    /// <summary>Keeps the whole matrix together on one page instead of splitting it across pages — the opt-out of
    /// row-level pagination (DevExpress <c>ContentSplitting</c> / SSRS <c>KeepTogether</c>). Default <c>false</c>
    /// (a tall matrix paginates). When <c>true</c>, a matrix taller than the page overflows rather than splitting.</summary>
    public bool KeepTogether { get; init; }
}

/// <summary>One axis of a Tablix grouping — name + group-key expression + sort.</summary>
public sealed record TablixGroup(
    string Name,
    string? GroupExpression = null,
    string? SortExpression = null,
    bool SortDescending = false);

/// <summary>A single body cell of a Tablix. <paramref name="ColumnSpan"/>/<paramref name="RowSpan"/> merge the
/// cell across adjacent columns/rows (RDL ColSpan/RowSpan); 1 = a normal 1×1 cell. A merged cell occupies the
/// covered columns/rows, which the renderer then skips.</summary>
public sealed record TablixCell(int RowIndex, int ColumnIndex, ReportElement? Content,
    int ColumnSpan = 1, int RowSpan = 1);

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Code</c> element — a block of custom C# (originally VB.NET in SSRS) that
/// declares helper functions reachable from expressions as <c>Code.MethodName(...)</c>.
/// We keep the source text alongside the optional language tag (default C#) so the
/// .repx round-trips lossless independently of whether code execution is enabled.
/// </summary>
/// <remarks>
/// Source is preserved verbatim and round-trips losslessly. Execution is OPT-IN via the Roslyn
/// code resolver (<c>Reporting.Expressions.Roslyn</c>) — RCE by design, disabled by default; enable
/// only for trusted report sources. Without a resolver configured, <c>Code.X</c> calls are unavailable.
/// </remarks>
public sealed record CodeElement : ReportElement
{
    /// <summary>Source text of the code block (typically a few helper methods).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Source language. Defaults to <c>CSharp</c>; legacy <c>VB</c> kept for
    /// SSRS interop.</summary>
    public CodeLanguage Language { get; init; } = CodeLanguage.CSharp;
}

public enum CodeLanguage { CSharp, VisualBasic }

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Map</c> — geographic visualisation. Renders a Web-Mercator view with an optional
/// vector basemap (GeoJSON shapes and/or a lat/long graticule) and a marker layer plotting each
/// row's latitude/longitude. Online tile basemaps (OpenStreetMap) are an opt-in layer via a tile resolver.
/// </summary>
public sealed record MapElement : ReportElement
{
    /// <summary>Tile / basemap provider (e.g. "OpenStreetMap", "None"). Drives the online tile layer
    /// when a tile resolver is configured; the offline vector basemap is driven by the fields below.</summary>
    [PropertyGrid(Category = "Mapa", Order = 1, Label = "Basemap (tiles)", Placeholder = "OpenStreetMap / (nenhum)")]
    public string? Basemap { get; init; }

    /// <summary>Optional data source providing geo-coded points / polygons.</summary>
    [PropertyGrid(Category = "Mapa", Order = 2, Label = "Fonte", Placeholder = "(fonte primária)")]
    public string? DataSetName { get; init; }

    /// <summary>Expression resolving to the latitude of each row's point.</summary>
    [PropertyGrid(Category = "Mapa", Order = 3, Label = "Latitude", Placeholder = "Fields.lat")]
    public string? LatitudeExpression { get; init; }

    /// <summary>Expression resolving to the longitude of each row's point.</summary>
    [PropertyGrid(Category = "Mapa", Order = 4, Label = "Longitude", Placeholder = "Fields.lon")]
    public string? LongitudeExpression { get; init; }

    // ── Vector basemap (offline) ──────────────────────────────────────────────────

    /// <summary>Inline GeoJSON (FeatureCollection / Feature / Geometry) drawn as the vector
    /// basemap — Polygon/MultiPolygon are filled; LineString/MultiLineString are stroked. Takes
    /// precedence over <see cref="ShapeSet"/> when both are set.</summary>
    [PropertyGrid(Category = "Mapa", Order = 6, Label = "GeoJSON inline", Editor = "textarea")]
    public string? ShapesGeoJson { get; init; }

    /// <summary>Name of a built-in shape set resolved at render time via the map shape registry
    /// (e.g. "brazil", "south-america"). Lets a report reference bundled shapes without inlining
    /// the GeoJSON. Ignored when <see cref="ShapesGeoJson"/> is provided.</summary>
    [PropertyGrid(Category = "Mapa", Order = 5, Label = "Conjunto de shapes", Placeholder = "brazil, south-america")]
    public string? ShapeSet { get; init; }

    /// <summary>Draws a latitude/longitude graticule (grid + degree ticks) behind the data so the
    /// plot reads as a map even without shapes. Default <c>false</c>.</summary>
    [PropertyGrid(Category = "Mapa", Order = 7, Label = "Graticule")]
    public bool ShowGraticule { get; init; }

    /// <summary>Fill colour (hex) for shape polygons — the "land" colour.</summary>
    [PropertyGrid(Category = "Mapa", Order = 8, Label = "Preenchimento", Editor = "color-hex")]
    public string ShapeFill { get; init; } = "#E8EDE4";

    /// <summary>Stroke colour (hex) for shape outlines / graticule lines.</summary>
    [PropertyGrid(Category = "Mapa", Order = 9, Label = "Traço", Editor = "color-hex")]
    public string ShapeStroke { get; init; } = "#9CA3AF";
}

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Gauge</c> — radial / linear gauge showing a single value against a range.
/// Captures Min/Max/Value expressions + gauge kind. Rendered by <c>KpiRenderer</c> (radial arc / linear track).
/// </summary>
public sealed record GaugeElement : ReportElement
{
    [PropertyGrid(Category = "Medidor", Order = 1, Label = "Tipo")]
    public GaugeKind Kind { get; init; } = GaugeKind.Radial;
    [PropertyGrid(Category = "Medidor", Order = 3, Label = "Mínimo")]
    public string MinimumExpression { get; init; } = "0";
    [PropertyGrid(Category = "Medidor", Order = 4, Label = "Máximo")]
    public string MaximumExpression { get; init; } = "100";
    [PropertyGrid(Category = "Medidor", Order = 2, Label = "Valor", Placeholder = "Fields.Velocidade")]
    public string ValueExpression { get; init; } = "0";
    /// <summary>Optional banded ranges (red/yellow/green zones). Each entry is
    /// (start expr, end expr, color hex).</summary>
    [PropertyGrid(Category = "Medidor", Order = 5, Label = "Faixas", Editor = "list")]
    public EquatableArray<GaugeRange> Ranges { get; init; } = EquatableArray<GaugeRange>.Empty;
}

public enum GaugeKind { Radial, Linear }

public sealed record GaugeRange(
    [property: PropertyGrid(Order = 1, Label = "Início")] string StartExpression,
    [property: PropertyGrid(Order = 2, Label = "Fim")] string EndExpression,
    [property: PropertyGrid(Order = 3, Label = "Cor", Editor = "color-hex")] string ColorHex);

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>DataBar</c> — horizontal bar that fills proportionally to <see cref="ValueExpression"/>.
/// Typically appears inside a Tablix cell to highlight a metric. Rendered by <c>KpiRenderer</c>.
/// </summary>
public sealed record DataBarElement : ReportElement
{
    [PropertyGrid(Category = "Barra de dados", Order = 1, Label = "Valor", Placeholder = "Fields.Total")]
    public string ValueExpression { get; init; } = "0";
    [PropertyGrid(Category = "Barra de dados", Order = 2, Label = "Mínimo")]
    public string MinimumExpression { get; init; } = "0";
    [PropertyGrid(Category = "Barra de dados", Order = 3, Label = "Máximo")]
    public string MaximumExpression { get; init; } = "100";
    /// <summary>Fill colour expression (hex literal or expression returning hex).</summary>
    [PropertyGrid(Category = "Barra de dados", Order = 4, Label = "Preenchimento", Editor = "color-hex")]
    public string FillColor { get; init; } = "#C2410C";
}

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Sparkline</c> — miniature line / bar chart embedded in a cell. Trades full
/// chart fidelity for compactness. Rendered by <c>KpiRenderer</c>.
/// </summary>
public sealed record SparklineElement : ReportElement
{
    [PropertyGrid(Category = "Mini-gráfico", Order = 1, Label = "Tipo")]
    public SparklineKind Kind { get; init; } = SparklineKind.Line;
    /// <summary>Data source providing the trend series. Each row contributes one point.</summary>
    [PropertyGrid(Category = "Mini-gráfico", Order = 4, Label = "Fonte", Placeholder = "(fonte primária)")]
    public string? DataSetName { get; init; }
    [PropertyGrid(Category = "Mini-gráfico", Order = 2, Label = "Valor", Placeholder = "Fields.Total")]
    public string ValueExpression { get; init; } = "Fields.Value";
    [PropertyGrid(Category = "Mini-gráfico", Order = 3, Label = "Categoria")]
    public string? CategoryExpression { get; init; }
}

public enum SparklineKind { Line, Column, Area }

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Indicator</c> — KPI icon (up arrow / star / signal bars) that swaps
/// visualisation based on which "state" range the value falls into. Rendered by <c>KpiRenderer</c>.
/// </summary>
public sealed record IndicatorElement : ReportElement
{
    [PropertyGrid(Category = "Indicador", Order = 1, Label = "Tipo")]
    public IndicatorKind Kind { get; init; } = IndicatorKind.DirectionalArrow;
    [PropertyGrid(Category = "Indicador", Order = 2, Label = "Valor", Placeholder = "Fields.Meta")]
    public string ValueExpression { get; init; } = "0";
    /// <summary>State boundaries — each (start, end, iconName).</summary>
    [PropertyGrid(Category = "Indicador", Order = 3, Label = "Estados", Editor = "list")]
    public EquatableArray<IndicatorState> States { get; init; } = EquatableArray<IndicatorState>.Empty;
}

public enum IndicatorKind { DirectionalArrow, Shape, RatingBar, Symbol }

public sealed record IndicatorState(
    [property: PropertyGrid(Order = 1, Label = "Início")] string StartExpression,
    [property: PropertyGrid(Order = 2, Label = "Fim")] string EndExpression,
    [property: PropertyGrid(Order = 3, Label = "Ícone")] string IconName);
