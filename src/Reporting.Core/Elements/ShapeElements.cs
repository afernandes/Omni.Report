using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>A straight line. <see cref="ReportElement.Bounds"/> defines the bounding box;
/// the line is drawn between two of its corners as indicated by <see cref="LineDirection"/>.</summary>
public sealed record LineElement : ReportElement
{
    [PropertyGrid(Category = "Linha", Order = 1, Label = "Orientação", Bindable = true)]
    public LineDirection Direction { get; init; } = LineDirection.TopLeftToBottomRight;

    public BorderSide Pen { get; init; } = new(BorderLineStyle.Solid, Unit.FromPoint(0.5), Color.Black);
}

/// <summary>Which corners (or center axis) of a <see cref="LineElement"/>'s bounds the line is drawn between.</summary>
public enum LineDirection
{
    /// <summary>Horizontal line at the vertical center of <see cref="ReportElement.Bounds"/>.</summary>
    Horizontal,

    /// <summary>Vertical line at the horizontal center of <see cref="ReportElement.Bounds"/>.</summary>
    Vertical,

    TopLeftToBottomRight,
    BottomLeftToTopRight,
}

/// <summary>A filled rectangle that doubles as a container — draws its optional fill (with corner radius) and then
/// lays out nested <see cref="Children"/> positioned relative to its top-left.</summary>
public sealed record RectangleElement : ReportElement
{
    [PropertyGrid(Category = "Forma", Order = 1, Label = "Preenchimento", Bindable = true)]
    public Color? FillColor { get; init; }

    [PropertyGrid(Category = "Forma", Order = 2, Label = "Raio do canto", Bindable = true)]
    public Unit CornerRadius { get; init; } = Unit.Zero;

    /// <summary>Nested report items, positioned by <see cref="ReportElement.Bounds"/> RELATIVE to this
    /// rectangle's top-left. The rectangle acts as a container (grouping/background): it draws its fill first,
    /// then its children on top. Children that overflow the rectangle are NOT clipped (visual parity with the
    /// flattened legacy behaviour); real clipping is a follow-up.</summary>
    public EquatableArray<ReportElement> Children { get; init; } = EquatableArray<ReportElement>.Empty;
}

/// <summary>An ellipse (or circle) drawn to fill <see cref="ReportElement.Bounds"/>, with an optional fill colour.</summary>
public sealed record EllipseElement : ReportElement
{
    [PropertyGrid(Category = "Forma", Order = 1, Label = "Preenchimento", Bindable = true)]
    public Color? FillColor { get; init; }
}
