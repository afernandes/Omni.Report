using System.Drawing.Printing;
using System.Runtime.Versioning;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Printing;
using Reporting.Rendering;
using Reporting.Rendering.Gdi;
using PaperSize = Reporting.Paper.PaperSize;
using GdiPaperSize = System.Drawing.Printing.PaperSize;

namespace Reporting.Printing.WindowsSpooler;

/// <summary>
/// <see cref="IReportPrinter"/> backed by the Windows print spooler. Wraps a
/// <see cref="PrintDocument"/>: each <see cref="PrintDocument.PrintPage"/> event hosts a
/// <see cref="GdiRenderingContext"/> that replays the corresponding page's primitives onto
/// <see cref="PrintPageEventArgs.Graphics"/> — vectorial all the way through, so text stays
/// crisp and selectable at any spool resolution.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSpoolerPrinter : IReportPrinter
{
    public string Driver => "windows-spooler";

    public Task<IReadOnlyList<PrinterInfo>> ListPrintersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var defaultName = new PrinterSettings().PrinterName;
        var printers = new List<PrinterInfo>();
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            var settings = new PrinterSettings { PrinterName = name };
            printers.Add(new PrinterInfo(
                Name: name,
                IsDefault: string.Equals(name, defaultName, StringComparison.Ordinal),
                PortName: TryGetPort(settings),
                Status: settings.IsValid ? "Ready" : "Unknown"));
        }
        return Task.FromResult<IReadOnlyList<PrinterInfo>>(printers);
    }

    public Task<PrinterCapabilities> GetCapabilitiesAsync(string printerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        cancellationToken.ThrowIfCancellationRequested();
        var settings = new PrinterSettings { PrinterName = printerName };
        if (!settings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{printerName}' is not installed or not valid.");
        }

        var papers = new List<PaperSize>();
        foreach (GdiPaperSize ps in settings.PaperSizes)
        {
            // Hundredths of an inch → mils ( ×10 ).
            papers.Add(new PaperSize(ps.PaperName, new Unit(ps.Width * 10), new Unit(ps.Height * 10)));
        }
        var bins = new List<string>();
        foreach (PaperSource src in settings.PaperSources)
        {
            bins.Add(src.SourceName);
        }
        return Task.FromResult(new PrinterCapabilities(
            PrinterName: printerName,
            SupportedPapers: new EquatableArray<PaperSize>(papers),
            PaperBins: new EquatableArray<string>(bins),
            SupportsDuplex: settings.CanDuplex,
            SupportsColor: settings.SupportsColor,
            MinCopies: 1,
            MaxCopies: settings.MaximumCopies <= 0 ? 999 : settings.MaximumCopies));
    }

    public Task<PrintResult> PrintAsync(RenderedReport report, PrintOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PrinterName);
        cancellationToken.ThrowIfCancellationRequested();

        var selected = PrintPageSelection.Enumerate(report.Pages.Count, options, includeCopies: false).ToArray();
        if (selected.Length == 0) return Task.FromResult(new PrintResult(true, 0));

        try
        {
            using var document = new PrintDocument
            {
                DocumentName = options.DocumentName ?? report.Name,
                PrinterSettings =
                {
                    PrinterName = options.PrinterName,
                    Copies = (short)Math.Max(1, options.Copies),
                    Collate = options.Collate,
                },
            };

            if (!document.PrinterSettings.IsValid)
            {
                return Task.FromResult(new PrintResult(
                    Succeeded: false,
                    PagesPrinted: 0,
                    ErrorMessage: $"Printer '{options.PrinterName}' is not installed or invalid."));
            }

            // Apply duplex
            if (options.Duplex != DuplexMode.Default && document.PrinterSettings.CanDuplex)
            {
                document.PrinterSettings.Duplex = options.Duplex switch
                {
                    DuplexMode.Simplex => Duplex.Simplex,
                    DuplexMode.Vertical => Duplex.Vertical,
                    DuplexMode.Horizontal => Duplex.Horizontal,
                    _ => Duplex.Default,
                };
            }

            // Page range
            // Pages are selected by the callback; the driver receives the resulting complete job.
            document.PrinterSettings.PrintRange = PrintRange.AllPages;

            // PrintToFile (Microsoft Print to PDF / XPS Document Writer)
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                document.PrinterSettings.PrintToFile = true;
                document.PrinterSettings.PrintFileName = options.OutputFile;
                // Some virtual printers fail with the regular print controller in headless contexts
                // — switch to the standard one explicitly.
                document.PrintController = new StandardPrintController();
            }

            // Paper bin
            if (!string.IsNullOrEmpty(options.PaperBin))
            {
                foreach (PaperSource src in document.PrinterSettings.PaperSources)
                {
                    if (string.Equals(src.SourceName, options.PaperBin, StringComparison.OrdinalIgnoreCase))
                    {
                        document.DefaultPageSettings.PaperSource = src;
                        break;
                    }
                }
            }

            int pagesPrinted = 0;
            int pageIndex = 0;

            document.QueryPageSettings += (_, qe) =>
            {
                if (pageIndex < selected.Length)
                {
                    ApplyPageSettings(qe.PageSettings, report.Pages[selected[pageIndex]].PageSetup, options.PaperSize);
                }
            };

            document.PrintPage += (_, pe) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pageIndex >= selected.Length)
                {
                    pe.HasMorePages = false;
                    return;
                }
                var page = report.Pages[selected[pageIndex]];
                using var ctx = new GdiRenderingContext(pe.Graphics!);
                RenderedReportPlayer.PlayPage(page, ctx);
                pageIndex++;
                pagesPrinted++;
                pe.HasMorePages = pageIndex < selected.Length;
            };

            document.Print();

            return Task.FromResult(new PrintResult(
                Succeeded: true,
                PagesPrinted: pagesPrinted,
                OutputPath: options.OutputFile));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new PrintResult(
                Succeeded: false,
                PagesPrinted: 0,
                ErrorMessage: ex.Message,
                Exception: ex));
        }
    }

    private static void ApplyPageSettings(PageSettings pageSettings, Reporting.Paper.PageSetup pageSetup, PaperSize? overridePaper)
    {
        var paper = overridePaper ?? pageSetup.Paper;
        // PaperSize uses hundredths of an inch; mils → ×0.1.
        var widthHundredths = (int)Math.Round(paper.Width.Mils / 10.0);
        var heightHundredths = (int)Math.Round(paper.Height.Mils / 10.0);
        pageSettings.PaperSize = new GdiPaperSize(paper.Name, widthHundredths, heightHundredths);
        pageSettings.Landscape = pageSetup.Orientation == Reporting.Paper.Orientation.Landscape;
        // System.Drawing.Printing margins are also in hundredths of an inch.
        pageSettings.Margins = new Margins(
            (int)Math.Round(pageSetup.Margins.Left.Mils / 10.0),
            (int)Math.Round(pageSetup.Margins.Right.Mils / 10.0),
            (int)Math.Round(pageSetup.Margins.Top.Mils / 10.0),
            (int)Math.Round(pageSetup.Margins.Bottom.Mils / 10.0));
    }

    private static string? TryGetPort(PrinterSettings settings)
    {
        try
        {
            return settings.PrintFileName;
        }
        catch
        {
            return null;
        }
    }
}
