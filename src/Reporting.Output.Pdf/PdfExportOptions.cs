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

    /// <summary>Optional creation date stamp. Defaults to <see cref="DateTimeOffset.UtcNow"/>.</summary>
    public DateTime? CreationDate { get; init; }

    /// <summary>If true, raster images embedded into the PDF will be compressed.</summary>
    public bool CompressImages { get; init; } = true;

    /// <summary>Embeds only the glyphs the report uses from each font instead of the whole font program. Defaults
    /// to <see langword="true"/>: a one-page report otherwise carries every font file it touches — over a megabyte
    /// for two Arial weights, tens of megabytes for a CJK font.</summary>
    /// <remarks>Turn it off only when the recipient must edit text in the PDF with characters the report does not
    /// contain. A font that cannot be subsetted (license flags, variable fonts, platform without the native
    /// subsetter) is embedded whole regardless of this setting.</remarks>
    public bool SubsetFonts { get; init; } = true;

    public static readonly PdfExportOptions Default = new();
}
