using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>Error-correction level for QR codes. Ignored for 1D symbologies.</summary>
public enum QrEccLevel
{
    /// <summary>~7% recovery. Smallest symbol.</summary>
    Low,
    /// <summary>~15% recovery (default).</summary>
    Medium,
    /// <summary>~25% recovery.</summary>
    Quartile,
    /// <summary>~30% recovery. Use when embedding a logo.</summary>
    High,
}
