using Reporting.Geometry;
using Reporting.Layout.Primitives;

namespace Reporting.Layout.Tabular;

/// <summary>One row in a <see cref="LayoutPrimitiveGrid"/>, addressed by column index.</summary>
public sealed class GridRow
{
    /// <summary>Cell text keyed by column index. Sparse — missing keys = blank cells.</summary>
    public Dictionary<int, string> Cells { get; } = new();

    /// <summary>Source primitives retain scalar types and explicitly supplied formulas.</summary>
    public Dictionary<int, DrawTextPrimitive> Sources { get; } = new();

    /// <summary>The absolute Y coordinate (across pages) of the source text cluster — used by
    /// the quantizer for clustering, exposed for ordering / debugging.</summary>
    public Unit Y { get; set; }

    /// <summary>Row classification — drives header bold, total formulas, group header color etc.</summary>
    public RowKind Kind { get; set; } = RowKind.Detail;
}
