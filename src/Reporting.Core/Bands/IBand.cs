using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Bands;

/// <summary>Common surface for any band.</summary>
public interface IBand
{
    /// <summary>The role this band plays in the layout.</summary>
    BandKind Kind { get; }

    /// <summary>Declared height. May grow or shrink at render when the band allows it.</summary>
    Unit Height { get; }

    /// <summary>Static visibility. False hides the band without evaluating any expression.</summary>
    bool Visible { get; }

    /// <summary>Optional expression evaluated per emission; when it returns false the band is skipped.
    /// Null means "use <see cref="Visible"/> alone".</summary>
    string? VisibleExpression { get; }

    /// <summary>The elements drawn inside the band, positioned relative to its top-left corner.</summary>
    EquatableArray<ReportElement> Elements { get; }
}
