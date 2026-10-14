using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Designer.Blazor.Services;

/// <summary>Color mode hint. The browser print dialog always offers its own toggle on top
/// of this — we use it for the PDF metadata + native printers that respect it.</summary>
public enum PrintColorMode
{
    /// <summary>Full color (default).</summary>
    Color,
    /// <summary>Force grayscale.</summary>
    Grayscale,
    /// <summary>Force black &amp; white (1-bit) — for low-end thermal / receipt printers.</summary>
    Monochrome,
}
