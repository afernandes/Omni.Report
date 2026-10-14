using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>A straight line. <see cref="ReportElement.Bounds"/> defines the bounding box;
/// the line is drawn between two of its corners as indicated by <see cref="LineDirection"/>.</summary>
public sealed record LineElement : ReportElement
{
    /// <summary>Which pair of corners (or centre axis) of the bounds the line connects.</summary>
    [PropertyGrid(Category = "Linha", Order = 1, Label = "Orientação", Bindable = true)]
    public LineDirection Direction { get; init; } = LineDirection.TopLeftToBottomRight;

    /// <summary>Stroke used to draw the line — style, thickness and colour. Defaults to a hairline black solid.</summary>
    public BorderSide Pen { get; init; } = new(BorderLineStyle.Solid, Unit.FromPoint(0.5), Color.Black);
}
