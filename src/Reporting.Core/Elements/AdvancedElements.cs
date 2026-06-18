using Reporting.Common;

namespace Reporting.Elements;

// ─── RDL F2 scaffold ─────────────────────────────────────────────────────────────
//
// These records carry every RDL element kind that the renderer doesn't yet draw
// natively. The goal is **lossless round-trip**: a .repx authored against SSRS or
// edited in a third-party tool that places, say, a Tablix or a Gauge, can still be
// loaded by our pipeline, edited, saved, and reopened without losing the element's
// configuration. The renderer treats them as placeholder rectangles (a labelled
// box at the element's Bounds) until a future iteration implements the dedicated
// drawing pipelines.
//
// Each type is a sealed record with the minimum fields required by the RDL spec
// plus the OmniReport common shape (Id / Bounds / Style / etc. inherited from
// ReportElement). Adding render support later is purely additive — the wire
// format and the .repx schema stay the same.

/// <summary>
/// RDL <c>Tablix</c> — unified Table + Matrix + List data region. The shape is a
/// nested grid of rows × columns where any axis can be statically defined OR group
/// dynamically (by an expression). The body cells hold child report items.
/// </summary>
/// <remarks>
/// For Phase 2 scaffold we capture <see cref="RowGroups"/>/<see cref="ColumnGroups"/>
/// + a flat list of <see cref="Cells"/> (each cell carries its row/column indices
/// + the child element). Render strategy: paginator emits a placeholder rectangle
/// with "Tablix" label; full grid layout planned for Phase 3.
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
    /// the child element (typically a TextBox). The renderer will iterate this when
    /// full Tablix support lands.</summary>
    public EquatableArray<TablixCell> Cells { get; init; } = EquatableArray<TablixCell>.Empty;

    /// <summary>Optional relative column widths (weights). Empty → every column is equal. When
    /// set, each entry is a positive weight and the renderer distributes the element's width
    /// proportionally — e.g. <c>[1, 2, 1]</c> makes the middle column twice as wide as its
    /// neighbours. A partial list (or zero entries) falls back to the average weight per column.</summary>
    public EquatableArray<double> ColumnWidths { get; init; } = EquatableArray<double>.Empty;
}

/// <summary>One axis of a Tablix grouping — name + group-key expression + sort.</summary>
public sealed record TablixGroup(
    string Name,
    string? GroupExpression = null,
    string? SortExpression = null,
    bool SortDescending = false);

/// <summary>A single body cell of a Tablix.</summary>
public sealed record TablixCell(int RowIndex, int ColumnIndex, ReportElement? Content);

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Code</c> element — a block of custom C# (originally VB.NET in SSRS) that
/// declares helper functions reachable from expressions as <c>Code.MethodName(...)</c>.
/// We keep the source text alongside the optional language tag (default C#) so the
/// .repx round-trips lossless even before a Roslyn-based compile/load path lands.
/// </summary>
/// <remarks>
/// Phase 2 scaffold: source code is preserved verbatim; calls to <c>Code.X</c> in
/// expressions currently throw <c>NotImplementedException</c>. Phase 3 plugs in
/// Roslyn-script compilation (cached AssemblyLoadContext per report).
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
/// row's latitude/longitude. Tile basemaps (OpenStreetMap/Bing) are a future opt-in layer.
/// </summary>
public sealed record MapElement : ReportElement
{
    /// <summary>Tile / basemap provider (e.g. "BingMaps", "OpenStreetMap", "None"). Reserved for
    /// the future online tile layer; the offline vector basemap is driven by the fields below.</summary>
    public string? Basemap { get; init; }

    /// <summary>Optional data source providing geo-coded points / polygons.</summary>
    public string? DataSetName { get; init; }

    /// <summary>Expression resolving to the latitude of each row's point.</summary>
    public string? LatitudeExpression { get; init; }

    /// <summary>Expression resolving to the longitude of each row's point.</summary>
    public string? LongitudeExpression { get; init; }

    // ── Vector basemap (offline) ──────────────────────────────────────────────────

    /// <summary>Inline GeoJSON (FeatureCollection / Feature / Geometry) drawn as the vector
    /// basemap — Polygon/MultiPolygon are filled; LineString/MultiLineString are stroked. Takes
    /// precedence over <see cref="ShapeSet"/> when both are set.</summary>
    public string? ShapesGeoJson { get; init; }

    /// <summary>Name of a built-in shape set resolved at render time via the map shape registry
    /// (e.g. "brazil", "south-america"). Lets a report reference bundled shapes without inlining
    /// the GeoJSON. Ignored when <see cref="ShapesGeoJson"/> is provided.</summary>
    public string? ShapeSet { get; init; }

    /// <summary>Draws a latitude/longitude graticule (grid + degree ticks) behind the data so the
    /// plot reads as a map even without shapes. Default <c>false</c>.</summary>
    public bool ShowGraticule { get; init; }

    /// <summary>Fill colour (hex) for shape polygons — the "land" colour.</summary>
    public string ShapeFill { get; init; } = "#E8EDE4";

    /// <summary>Stroke colour (hex) for shape outlines / graticule lines.</summary>
    public string ShapeStroke { get; init; } = "#9CA3AF";
}

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Gauge</c> — radial / linear gauge showing a single value against a range.
/// Captures Min/Max/Value expressions + gauge kind. Renderer placeholder for now.
/// </summary>
public sealed record GaugeElement : ReportElement
{
    public GaugeKind Kind { get; init; } = GaugeKind.Radial;
    public string MinimumExpression { get; init; } = "0";
    public string MaximumExpression { get; init; } = "100";
    public string ValueExpression { get; init; } = "0";
    /// <summary>Optional banded ranges (red/yellow/green zones). Each entry is
    /// (start expr, end expr, color hex).</summary>
    public EquatableArray<GaugeRange> Ranges { get; init; } = EquatableArray<GaugeRange>.Empty;
}

public enum GaugeKind { Radial, Linear }

public sealed record GaugeRange(string StartExpression, string EndExpression, string ColorHex);

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>DataBar</c> — horizontal bar that fills proportionally to <see cref="ValueExpression"/>.
/// Typically appears inside a Tablix cell to highlight a metric. Scaffold: round-trip
/// only; renderer placeholder.
/// </summary>
public sealed record DataBarElement : ReportElement
{
    public string ValueExpression { get; init; } = "0";
    public string MinimumExpression { get; init; } = "0";
    public string MaximumExpression { get; init; } = "100";
    /// <summary>Fill colour expression (hex literal or expression returning hex).</summary>
    public string FillColor { get; init; } = "#C2410C";
}

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Sparkline</c> — miniature line / bar chart embedded in a cell. Trades full
/// chart fidelity for compactness. Scaffold: round-trip + future renderer.
/// </summary>
public sealed record SparklineElement : ReportElement
{
    public SparklineKind Kind { get; init; } = SparklineKind.Line;
    /// <summary>Data source providing the trend series. Each row contributes one point.</summary>
    public string? DataSetName { get; init; }
    public string ValueExpression { get; init; } = "Fields.Value";
    public string? CategoryExpression { get; init; }
}

public enum SparklineKind { Line, Column, Area }

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// RDL <c>Indicator</c> — KPI icon (up arrow / star / signal bars) that swaps
/// visualisation based on which "state" range the value falls into. Scaffold: round-trip;
/// the renderer draws a placeholder labelled with <see cref="ValueExpression"/>.
/// </summary>
public sealed record IndicatorElement : ReportElement
{
    public IndicatorKind Kind { get; init; } = IndicatorKind.DirectionalArrow;
    public string ValueExpression { get; init; } = "0";
    /// <summary>State boundaries — each (start, end, iconName).</summary>
    public EquatableArray<IndicatorState> States { get; init; } = EquatableArray<IndicatorState>.Empty;
}

public enum IndicatorKind { DirectionalArrow, Shape, RatingBar, Symbol }

public sealed record IndicatorState(string StartExpression, string EndExpression, string IconName);
