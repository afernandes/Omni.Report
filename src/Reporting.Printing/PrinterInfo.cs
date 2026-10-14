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
