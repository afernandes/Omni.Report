using Reporting.Geometry;

namespace Reporting.Paper;

/// <summary>Page orientation — whether the paper is taller than wide (portrait) or wider than tall (landscape).</summary>
public enum Orientation
{
    /// <summary>Taller than wide — the paper is used in its declared orientation.</summary>
    Portrait,

    /// <summary>Wider than tall — width and height are swapped.</summary>
    Landscape,
}
