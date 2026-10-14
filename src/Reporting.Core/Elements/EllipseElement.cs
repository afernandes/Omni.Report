using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>An ellipse (or circle) drawn to fill <see cref="ReportElement.Bounds"/>, with an optional fill colour.</summary>
public sealed record EllipseElement : ReportElement
{
    /// <summary>Interior fill. Null leaves the ellipse unfilled, showing whatever is beneath it.</summary>
    [PropertyGrid(Category = "Forma", Order = 1, Label = "Preenchimento", Bindable = true)]
    public Color? FillColor { get; init; }
}
