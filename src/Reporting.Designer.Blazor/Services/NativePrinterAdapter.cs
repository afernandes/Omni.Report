using Reporting.Layout;
using Reporting.Printing;

namespace Reporting.Designer.Blazor.Services;

/// <summary>
/// Bridges <see cref="IDesignerPrintService"/> to the platform-agnostic
/// <see cref="IReportPrinter"/> drivers (Windows Spooler, ESC/POS, Android Print Framework).
/// Register this when the host has a physical print driver and the user wants the
/// designer's print button to bypass the browser dialog.
/// </summary>
/// <remarks>
/// <para>Typical wiring (MAUI Blazor on Windows):</para>
/// <code>
/// services.AddSingleton&lt;IReportPrinter, WindowsSpoolerPrinter&gt;();
/// services.AddSingleton&lt;IDesignerPrintService, NativePrinterAdapter&gt;();
/// </code>
///
/// <para>When <see cref="PrintRequest.OutputMode"/> is
/// <see cref="PrintOutputMode.BrowserDialog"/> or <see cref="PrintOutputMode.SaveAsPdf"/>,
/// this adapter forwards to <see cref="BrowserPrintService"/> instead (passed via the
/// constructor as a fallback) so the user still gets browser-based PDF download / preview
/// when they pick those modes from the dialog.</para>
/// </remarks>
public sealed class NativePrinterAdapter : IDesignerPrintService
{
    private readonly IReportPrinter _printer;
    private readonly BrowserPrintService _browser;

    public NativePrinterAdapter(IReportPrinter printer, BrowserPrintService browserFallback)
    {
        _printer = printer;
        _browser = browserFallback;
    }

    /// <inheritdoc/>
    public bool SupportsDirectPrint => true;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DesignerPrinter>> ListPrintersAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _printer.ListPrintersAsync(cancellationToken).ConfigureAwait(false);
        return raw.Select(p => new DesignerPrinter(
            Name: p.Name,
            DisplayName: p.Name,
            IsDefault: p.IsDefault,
            Kind: p.Driver))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task PrintAsync(RenderedReport report, PrintRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        // Browser dialog / Save-as-PDF go through the universal path. Only SystemSpooler
        // talks to the native driver — that's the whole point of this adapter.
        if (request.OutputMode != PrintOutputMode.SystemSpooler)
        {
            await _browser.PrintAsync(report, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            throw new InvalidOperationException(
                "PrintRequest.OutputMode = SystemSpooler requires PrinterName.");
        }

        var options = new PrintOptions(request.PrinterName)
        {
            Copies = Math.Max(1, request.Copies),
            Collate = request.Collate,
            // bool? → DuplexMode mapping: true = long-edge bind (most common A4 default),
            // false = explicit simplex, null = honor the driver's saved default.
            Duplex = request.Duplex switch
            {
                true  => DuplexMode.Vertical,
                false => DuplexMode.Simplex,
                _     => DuplexMode.Default,
            },
            PaperSize = request.PaperSize,
            DocumentName = report.Name,
        };
        await _printer.PrintAsync(report, options, cancellationToken).ConfigureAwait(false);
    }
}
