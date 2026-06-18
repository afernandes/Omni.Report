using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>A straight line. <see cref="ReportElement.Bounds"/> defines the bounding box;
/// the line is drawn between two of its corners as indicated by <see cref="LineDirection"/>.</summary>
public sealed record LineElement : ReportElement
{
    public LineDirection Direction { get; init; } = LineDirection.TopLeftToBottomRight;
    public BorderSide Pen { get; init; } = new(BorderLineStyle.Solid, Unit.FromPoint(0.5), Color.Black);
}

public enum LineDirection
{
    /// <summary>Horizontal line at the vertical center of <see cref="ReportElement.Bounds"/>.</summary>
    Horizontal,

    /// <summary>Vertical line at the horizontal center of <see cref="ReportElement.Bounds"/>.</summary>
    Vertical,

    TopLeftToBottomRight,
    BottomLeftToTopRight,
}

public sealed record RectangleElement : ReportElement
{
    public Color? FillColor { get; init; }
    public Unit CornerRadius { get; init; } = Unit.Zero;
}

public sealed record EllipseElement : ReportElement
{
    public Color? FillColor { get; init; }
}
