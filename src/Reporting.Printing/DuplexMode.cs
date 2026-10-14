using Reporting.Common;
using Reporting.Paper;

namespace Reporting.Printing;

public enum DuplexMode
{
    /// <summary>Use the printer's default setting.</summary>
    Default,

    /// <summary>Single-sided printing.</summary>
    Simplex,

    /// <summary>Duplex with long-edge binding (portrait orientation).</summary>
    Vertical,

    /// <summary>Duplex with short-edge binding (landscape-friendly).</summary>
    Horizontal,
}
