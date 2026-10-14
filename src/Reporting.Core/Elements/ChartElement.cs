using Reporting.Common;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>RDL <c>Chart</c> — a data visualisation of one or more <see cref="ChartSeries"/> rendered by
/// <c>ChartRenderer</c>, with a configurable <see cref="Kind"/>, optional title, and legend.</summary>
public sealed record ChartElement : ReportElement
{
    /// <summary>The plot type. Determines which of the series expressions are honoured.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 1, Label = "Tipo")]
    public ChartKind Kind { get; init; } = ChartKind.Bar;

    /// <summary>Optional caption drawn above the plot area. Null draws no title.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 2, Label = "Título")]
    public string? Title { get; init; }

    /// <summary>Whether to draw the series legend.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 3, Label = "Legenda")]
    public bool ShowLegend { get; init; } = true;

    /// <summary>The series plotted. An empty collection renders an empty plot area.</summary>
    [PropertyGrid(Category = "Gráfico", Order = 4, Label = "Séries", Editor = "list")]
    public EquatableArray<ChartSeries> Series { get; init; } = EquatableArray<ChartSeries>.Empty;
}
