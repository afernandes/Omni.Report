using System.Text;

namespace Reporting.Barcode;

/// <summary>QR error-correction level — higher = bigger symbol, more damage tolerance.</summary>
public enum QrErrorCorrection
{
    /// <summary>~7% recovery. Smallest symbol; OK for clean printing.</summary>
    Low,
    /// <summary>~15% recovery. Default — balance between size and resilience.</summary>
    Medium,
    /// <summary>~25% recovery. Good for printed receipts that get crumpled.</summary>
    Quartile,
    /// <summary>~30% recovery. Use when embedding a logo or for severely
    /// damaged printing surfaces.</summary>
    High,
}
