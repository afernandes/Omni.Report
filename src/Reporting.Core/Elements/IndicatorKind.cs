using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>Icon family of an <see cref="IndicatorElement"/> — directional arrow, traffic-light shape, rating bar, or symbol.</summary>
public enum IndicatorKind
{
    /// <summary>Arrows pointing up, sideways or down.</summary>
    DirectionalArrow,

    /// <summary>Traffic-light style shapes.</summary>
    Shape,

    /// <summary>A filled rating bar, e.g. signal strength.</summary>
    RatingBar,

    /// <summary>Assorted symbols such as check marks and crosses.</summary>
    Symbol,
}
