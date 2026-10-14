using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>Which corners (or center axis) of a <see cref="LineElement"/>'s bounds the line is drawn between.</summary>
public enum LineDirection
{
    /// <summary>Horizontal line at the vertical center of <see cref="ReportElement.Bounds"/>.</summary>
    Horizontal,

    /// <summary>Vertical line at the horizontal center of <see cref="ReportElement.Bounds"/>.</summary>
    Vertical,

    /// <summary>Diagonal from the top-left corner down to the bottom-right one.</summary>
    TopLeftToBottomRight,

    /// <summary>Diagonal from the bottom-left corner up to the top-right one.</summary>
    BottomLeftToTopRight,
}
