using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Designer.Blazor.Services;

/// <summary>How the user wants page numbers narrowed for printing.</summary>
public enum PrintPageRangeMode
{
    /// <summary>Every page in the rendered report.</summary>
    All,
    /// <summary>Just the page the viewer is currently showing.</summary>
    Current,
    /// <summary>An ad-hoc text range like "1-3,5,7-9".</summary>
    Range,
}

/// <summary>Color mode hint. The browser print dialog always offers its own toggle on top
/// of this — we use it for the PDF metadata + native printers that respect it.</summary>
public enum PrintColorMode
{
    /// <summary>Full color (default).</summary>
    Color,
    /// <summary>Force grayscale.</summary>
    Grayscale,
    /// <summary>Force black & white (1-bit) — for low-end thermal / receipt printers.</summary>
    Monochrome,
}

/// <summary>How we actually push the bytes to a printer.</summary>
public enum PrintOutputMode
{
    /// <summary>Open the rendered PDF in a new browser tab/window and call
    /// <c>window.print()</c>. The user picks printer + driver options in the
    /// browser/OS dialog. Universal — works in Server, WebAssembly and MAUI Blazor.</summary>
    BrowserDialog,
    /// <summary>Send directly to a named printer via <c>IReportPrinter</c> (Windows spooler,
    /// ESC/POS, Android Print). Requires a native print service registered in DI; the
    /// dialog hides this option when no such service is available.</summary>
    SystemSpooler,
    /// <summary>Generate the PDF and trigger a file download. Useful when the user wants
    /// to email/keep it rather than send to a physical device.</summary>
    SaveAsPdf,
}

/// <summary>
/// Everything the print pipeline needs to know in one immutable record. The
/// <see cref="PrintDialog"/> builds one of these from the user's choices and hands it
/// to <see cref="IDesignerPrintService.PrintAsync"/>.
/// </summary>
/// <param name="PaperSize">Override the report's paper size (e.g. send an A4 report to a
/// Letter printer). Use <c>null</c> to keep the original.</param>
/// <param name="Orientation">Override portrait/landscape. <c>null</c> = keep original.</param>
/// <param name="Margins">Override the four-side margin in millimeters. <c>null</c> = keep
/// the report's own margins. Tipped values negative or out-of-range fall back silently.</param>
/// <param name="RangeMode">Which subset of pages to print.</param>
/// <param name="RangeText">When <paramref name="RangeMode"/> is
/// <see cref="PrintPageRangeMode.Range"/>: a string like "1-3,5,7-9" with 1-based indices.
/// Trim/empty falls back to All.</param>
/// <param name="Copies">Number of copies (browser dialog usually exposes its own counter
/// too — both stack multiplicatively, so most users set this here and leave the dialog
/// at 1).</param>
/// <param name="Collate">When <see cref="Copies"/> &gt; 1: emit each copy in full
/// (true, the dialog default) vs. emit all copies of page 1, then page 2, etc.</param>
/// <param name="ColorMode">Color / grayscale / monochrome hint.</param>
/// <param name="OutputMode">Browser dialog / system spooler / save-as-pdf.</param>
/// <param name="PrinterName">Required when <paramref name="OutputMode"/> is
/// <see cref="PrintOutputMode.SystemSpooler"/>: the destination printer's name from
/// <see cref="IDesignerPrintService.ListPrintersAsync"/>. Ignored otherwise.</param>
/// <param name="Duplex">Duplex flag for native printers (browser dialog has its own).
/// <c>null</c> = printer default.</param>
public sealed record PrintRequest(
    PaperSize? PaperSize = null,
    Orientation? Orientation = null,
    Thickness? Margins = null,
    PrintPageRangeMode RangeMode = PrintPageRangeMode.All,
    string? RangeText = null,
    int Copies = 1,
    bool Collate = true,
    PrintColorMode ColorMode = PrintColorMode.Color,
    PrintOutputMode OutputMode = PrintOutputMode.BrowserDialog,
    string? PrinterName = null,
    bool? Duplex = null)
{
    /// <summary>Sensible defaults: respect the report's own page setup, all pages,
    /// one copy, color, browser print dialog. Matches what most users want with zero
    /// configuration.</summary>
    public static PrintRequest Default { get; } = new();

    /// <summary>Parses <see cref="RangeText"/> into 1-based page indices. Invalid tokens
    /// are skipped silently — the worst case is "user typed garbage" → empty range → no
    /// pages printed, which surfaces in the preview rather than crashing the pipeline.</summary>
    /// <param name="totalPages">Used to clamp open-ended ranges like "5-" → "5-{totalPages}".</param>
    public IEnumerable<int> EnumeratePageNumbers(int totalPages)
    {
        if (totalPages <= 0) yield break;
        if (RangeMode == PrintPageRangeMode.All)
        {
            for (int i = 1; i <= totalPages; i++) yield return i;
            yield break;
        }
        if (string.IsNullOrWhiteSpace(RangeText)) yield break;

        foreach (var token in RangeText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = token.IndexOf('-');
            if (dash < 0)
            {
                if (int.TryParse(token, out var single) && single >= 1 && single <= totalPages)
                    yield return single;
                continue;
            }
            // Range "a-b" — open ends ("-3", "5-") clamp to the document.
            var leftRaw = token[..dash];
            var rightRaw = token[(dash + 1)..];
            int from = int.TryParse(leftRaw, out var l) ? l : 1;
            int to   = int.TryParse(rightRaw, out var r) ? r : totalPages;
            if (from > to) (from, to) = (to, from);
            from = Math.Max(1, from);
            to = Math.Min(totalPages, to);
            for (int i = from; i <= to; i++) yield return i;
        }
    }
}
