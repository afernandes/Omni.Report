using Reporting.Common;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>The plot type of a <see cref="ChartElement"/> — bar, line, pie, area, scatter, radar, bubble, or stock.</summary>
public enum ChartKind
{
    /// <summary>Vertical bars, one per category and series.</summary>
    Bar,

    /// <summary>A polyline per series.</summary>
    Line,

    /// <summary>A single-series circle divided into proportional slices.</summary>
    Pie,
    /// <summary>Line chart with the area below each series filled (translucent).</summary>
    Area,
    /// <summary>Point/marker plot — an ellipse per (category, value), no connecting line.</summary>
    Scatter,
    /// <summary>Polar plot — categories on radial axes, value as radius; each series a closed web.</summary>
    Radar,
    /// <summary>Scatter with a third dimension — each marker is sized by <see cref="ChartSeries.SizeExpression"/>.</summary>
    Bubble,
    /// <summary>High-low(-close) range chart — a vertical bar from <see cref="ChartSeries.LowExpression"/>
    /// to <see cref="ChartSeries.HighExpression"/> per category, with a close tick at the value.</summary>
    Stock,
}
