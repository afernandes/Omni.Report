using Reporting.Paper;

namespace Reporting.Printing;

/// <summary>Options passed to <see cref="IReportPrinter.PrintAsync"/>.</summary>
public sealed record PrintOptions(string PrinterName)
{
    public int Copies { get; init; } = 1;
    public bool Collate { get; init; } = true;
    public DuplexMode Duplex { get; init; } = DuplexMode.Default;

    /// <summary>Override paper. When null, the printer's currently selected paper is used —
    /// usually inherited from the report's <see cref="PaperSize"/>.</summary>
    public PaperSize? PaperSize { get; init; }

    /// <summary>Override paper bin (e.g. "Tray 1", "Manual feed"). Null = printer default.</summary>
    public string? PaperBin { get; init; }

    /// <summary>If set AND the printer supports "print to file" (e.g. Microsoft Print to PDF /
    /// XPS Document Writer), output is redirected to this file path instead of the device.</summary>
    public string? OutputFile { get; init; }

    /// <summary>Optional 1-based range of pages to print (inclusive). Null = all pages.</summary>
    public (int From, int To)? PageRange { get; init; }

    /// <summary>Document title for the spool entry (shown in the print queue UI).</summary>
    public string? DocumentName { get; init; }
}

/// <summary>Outcome of a print operation.</summary>
public sealed record PrintResult(
    bool Succeeded,
    int PagesPrinted,
    string? OutputPath = null,
    string? ErrorMessage = null,
    Exception? Exception = null);
