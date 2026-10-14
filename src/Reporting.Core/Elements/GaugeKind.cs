using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>Visual form of a <see cref="GaugeElement"/> — a radial dial arc or a linear track.</summary>
public enum GaugeKind
{
    /// <summary>A circular dial with a needle.</summary>
    Radial,

    /// <summary>A straight track with a marker.</summary>
    Linear,
}
