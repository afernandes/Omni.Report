using Reporting.Layout;

namespace Reporting.Printing;

/// <summary>
/// Platform-agnostic printer driver. Implementations target a specific physical or virtual
/// device family — the Windows spooler, ESC/POS thermal printers, the Android Print Framework,
/// etc. Consumers pick an implementation via DI based on their host environment.
/// </summary>
public interface IReportPrinter
{
    /// <summary>Stable identifier of this driver family (e.g. <c>"windows-spooler"</c>).</summary>
    string Driver { get; }

    /// <summary>Enumerates printers reachable to this driver.</summary>
    Task<IReadOnlyList<PrinterInfo>> ListPrintersAsync(CancellationToken cancellationToken = default);

    /// <summary>Inspects the capabilities of a specific printer (papers, bins, duplex, …).</summary>
    Task<PrinterCapabilities> GetCapabilitiesAsync(string printerName, CancellationToken cancellationToken = default);

    /// <summary>Sends a paginated <see cref="RenderedReport"/> to a printer (or to a file when
    /// <see cref="PrintOptions.OutputFile"/> is set and the driver supports redirection).</summary>
    Task<PrintResult> PrintAsync(RenderedReport report, PrintOptions options, CancellationToken cancellationToken = default);
}
