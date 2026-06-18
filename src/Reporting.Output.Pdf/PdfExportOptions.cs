namespace Reporting.Output.Pdf;

/// <summary>Optional metadata embedded into the PDF (XMP / Document Information dictionary).</summary>
public sealed record PdfExportOptions
{
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Subject { get; init; }
    public string? Keywords { get; init; }

    /// <summary>Producer string — typically the library version. Defaults to "OmniReport".</summary>
    public string Producer { get; init; } = "OmniReport";

    /// <summary>Creator application string (e.g. "OmniReport Designer 0.1").</summary>
    public string? Creator { get; init; }

    /// <summary>Optional creation date stamp. Defaults to <see cref="DateTime.Now"/>.</summary>
    public DateTime? CreationDate { get; init; }

    /// <summary>If true, raster images embedded into the PDF will be compressed.</summary>
    public bool CompressImages { get; init; } = true;

    public static readonly PdfExportOptions Default = new();
}
