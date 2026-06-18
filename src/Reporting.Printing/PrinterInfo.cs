using Reporting.Common;
using Reporting.Paper;

namespace Reporting.Printing;

/// <summary>Lightweight summary of a printer surfaced by <see cref="IReportPrinter.ListPrintersAsync"/>.</summary>
public sealed record PrinterInfo(
    string Name,
    bool IsDefault = false,
    string? PortName = null,
    string? Status = null,
    string? Driver = null);

/// <summary>Static capabilities of a printer reported by the driver.</summary>
public sealed record PrinterCapabilities(
    string PrinterName,
    EquatableArray<PaperSize> SupportedPapers,
    EquatableArray<string> PaperBins,
    bool SupportsDuplex,
    bool SupportsColor,
    int MinCopies = 1,
    int MaxCopies = 999);

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
