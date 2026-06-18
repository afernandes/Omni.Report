using Reporting.Layout;

namespace Reporting.Designer.Blazor.Services;

/// <summary>
/// Abstraction that lets the designer print a paginated report. Concrete implementations
/// are picked per host:
/// <list type="bullet">
/// <item><b>Browser hosts (Blazor Server, Blazor WebAssembly, MAUI Blazor):</b>
///   <c>BrowserPrintService</c> renders the report to PDF and triggers <c>window.print()</c>
///   on the WebView. The user sees the OS print dialog through the browser shell. This is
///   the same approach DevExpress, Telerik and FastReport web designers use.</item>
/// <item><b>Native opt-in (any host with <c>Reporting.Printing.IReportPrinter</c> registered):</b>
///   <c>NativePrinterAdapter</c> bypasses the browser and sends straight to a system
///   printer (Windows spooler / ESC/POS thermal / Android Print Framework). Useful for
///   kiosk apps, NF-e thermal printers, silent printing without user prompts.</item>
/// </list>
/// </summary>
public interface IDesignerPrintService
{
    /// <summary>True when this service can route to a specific named printer without a
    /// user-facing OS dialog. Drives the PrintDialog: the "Printer" dropdown only renders
    /// when this returns true; otherwise the dialog shows "Sistema padrão (diálogo do
    /// navegador)" and hands the choice off to the browser/OS.</summary>
    bool SupportsDirectPrint { get; }

    /// <summary>Enumerates printers reachable to this service. Browser-only services
    /// return an empty list — the browser's own dialog populates the printer list at
    /// the OS level instead.</summary>
    Task<IReadOnlyList<DesignerPrinter>> ListPrintersAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends <paramref name="report"/> to a printer (or PDF download) according to
    /// <paramref name="request"/>. Throws when the request is incompatible with the service
    /// (e.g. asking for SystemSpooler when <see cref="SupportsDirectPrint"/> is false).</summary>
    Task PrintAsync(RenderedReport report, PrintRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Lightweight printer descriptor — what the dialog needs to render a usable
/// dropdown. Kept independent of <c>Reporting.Printing.PrinterInfo</c> so the designer
/// package doesn't drag the Printing assembly in browser-only scenarios.</summary>
/// <param name="Name">Stable identifier — passed back as <see cref="PrintRequest.PrinterName"/>.</param>
/// <param name="DisplayName">Human label shown in the dropdown. Often matches <paramref name="Name"/>.</param>
/// <param name="IsDefault">True for the OS default printer — pre-selected in the dialog.</param>
/// <param name="Kind">Driver hint (Spooler / EscPos / Pdf …). Drives the icon next to the name.</param>
public sealed record DesignerPrinter(
    string Name,
    string DisplayName,
    bool IsDefault = false,
    string? Kind = null);
