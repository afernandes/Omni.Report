using Microsoft.JSInterop;
using Reporting.Common;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;
using Reporting.Paper;

namespace Reporting.Designer.Blazor.Services;

/// <summary>
/// Default <see cref="IDesignerPrintService"/> for browser hosts. The recipe:
/// <list type="number">
/// <item>Render the (optionally filtered/re-paginated) report to PDF via
/// <see cref="SkiaPdfExporter"/>.</item>
/// <item>For <see cref="PrintOutputMode.BrowserDialog"/>: ship the bytes to JS, where
/// <c>omniDesignerPrint.printPdfBlob</c> opens the file in a hidden iframe and calls
/// <c>iframe.contentWindow.print()</c>. The user gets their familiar OS print dialog
/// (Windows/macOS native, no extra UI we'd have to build).</item>
/// <item>For <see cref="PrintOutputMode.SaveAsPdf"/>: hand the bytes off to the existing
/// <c>omniViewer.download</c> helper — same path as the "Exportar PDF" toolbar button.</item>
/// </list>
///
/// <para>Works identically in Blazor Server (SignalR carries the bytes), Blazor WebAssembly
/// (no roundtrip), and MAUI Blazor Hybrid (WebView's <c>window.print()</c> opens the
/// platform's native print sheet). One implementation, three hosts. This is the same
/// pattern DevExpress / Telerik / FastReport use for their web designers.</para>
///
/// <para>Does NOT support <see cref="PrintOutputMode.SystemSpooler"/>; for direct-to-spooler
/// printing register <c>NativePrinterAdapter</c> (which wraps
/// <c>Reporting.Printing.IReportPrinter</c>) in DI instead.</para>
/// </summary>
public sealed class BrowserPrintService : IDesignerPrintService
{
    private readonly IJSRuntime _js;

    public BrowserPrintService(IJSRuntime js)
    {
        _js = js;
    }

    /// <inheritdoc/>
    public bool SupportsDirectPrint => false;

    /// <inheritdoc/>
    /// <remarks>The browser dialog populates the printer list itself; we have nothing to
    /// list. Returning empty is the signal to the <c>PrintDialog</c> that it should hide
    /// the printer dropdown and label the destination "Sistema padrão (browser)".</remarks>
    public Task<IReadOnlyList<DesignerPrinter>> ListPrintersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DesignerPrinter>>(Array.Empty<DesignerPrinter>());

    /// <inheritdoc/>
    public async Task PrintAsync(RenderedReport report, PrintRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        if (request.OutputMode == PrintOutputMode.SystemSpooler)
        {
            throw new NotSupportedException(
                "BrowserPrintService doesn't route to a system printer. Register " +
                "NativePrinterAdapter (or another IDesignerPrintService) for direct-to-spooler printing.");
        }

        // Apply the user's overrides (page size / orientation / margins / range) as a
        // re-paginated subset before rendering. The renderer doesn't know about ranges —
        // we filter the page list here so the PDF only contains what should be printed.
        var filtered = ApplyRequestToReport(report, request);

        var pdf = new SkiaPdfExporter(new PdfExportOptions { Title = report.Name })
            .ExportToBytes(filtered);

        switch (request.OutputMode)
        {
            case PrintOutputMode.BrowserDialog:
                // The JS module is responsible for the actual window.print() — see
                // designer-print.js for the iframe choreography. Copies + collate are
                // forwarded as hints; the browser dialog has the final word.
                await _js.InvokeVoidAsync("omniDesignerPrint.printPdfBlob", cancellationToken,
                    pdf,
                    new
                    {
                        title = report.Name,
                        copies = Math.Max(1, request.Copies),
                        // We could pre-suggest grayscale to the browser via CSS print
                        // media queries on the host page, but most browsers ignore it
                        // when printing a PDF — leaving this as a hint only.
                        colorMode = request.ColorMode.ToString().ToLowerInvariant(),
                    });
                break;

            case PrintOutputMode.SaveAsPdf:
                var fileName = SafeFileName(report.Name) + ".pdf";
                await _js.InvokeVoidAsync("omniViewer.download", cancellationToken,
                    fileName, "application/pdf", pdf);
                break;
        }
    }

    /// <summary>Applies the request's page-setup overrides and page-range filter to produce
    /// the PDF input. Returns the original report unmodified when no override applies — the
    /// fast path for "default printing" so we don't allocate a clone for the common case.</summary>
    private static RenderedReport ApplyRequestToReport(RenderedReport report, PrintRequest request)
    {
        var pages = report.Pages;
        bool filterPages = request.RangeMode != PrintPageRangeMode.All;
        bool overridePageSetup = request.PaperSize is not null || request.Orientation is not null || request.Margins is not null;

        if (!filterPages && !overridePageSetup) return report;

        // Filter the page list when a range was specified.
        IEnumerable<RenderedPage> selected = pages;
        if (filterPages)
        {
            var keep = new HashSet<int>(request.EnumeratePageNumbers(pages.Count));
            selected = pages.Where(p => keep.Contains(p.PageNumber));
        }

        // Apply page-setup override: each page carries its own PageSetup (lets the engine
        // do mixed-size reports, e.g. a cover page in A4 + content in Letter). We rewrite
        // every page so the entire print job uses the override.
        if (overridePageSetup)
        {
            selected = selected.Select(p =>
            {
                var setup = p.PageSetup;
                if (request.PaperSize is not null) setup = setup with { Paper = request.PaperSize };
                if (request.Orientation is not null) setup = setup with { Orientation = request.Orientation.Value };
                if (request.Margins is not null) setup = setup with { Margins = request.Margins.Value };
                return new RenderedPage(p.PageNumber, setup, p.Primitives);
            });
        }

        return new RenderedReport(report.Name, new EquatableArray<RenderedPage>(selected.ToArray()));
    }

    private static string SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "report";
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
