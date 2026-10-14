using Reporting.Common;
using Reporting.Paper;

namespace Reporting.Printing;

/// <summary>Static capabilities of a printer reported by the driver.</summary>
public sealed record PrinterCapabilities(
    string PrinterName,
    EquatableArray<PaperSize> SupportedPapers,
    EquatableArray<string> PaperBins,
    bool SupportsDuplex,
    bool SupportsColor,
    int MinCopies = 1,
    int MaxCopies = 999);
