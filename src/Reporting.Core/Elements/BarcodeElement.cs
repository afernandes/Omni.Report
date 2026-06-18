namespace Reporting.Elements;

/// <summary>Barcode symbologies recognized by the renderer. Kept independent of
/// the encoder package (Reporting.Barcode) so Core stays dependency-free; the
/// renderer maps between the two enums.</summary>
public enum BarcodeSymbology
{
    /// <summary>Code 128 subset B — alphanumeric, ASCII 32..127 (default).</summary>
    Code128,
    /// <summary>Code 39 — uppercase + digits + a few symbols.</summary>
    Code39,
    /// <summary>Codabar — digits + a few symbols, library/medical staple.</summary>
    Codabar,
    /// <summary>Interleaved 2 of 5 — digits only, dense, logistics-friendly.</summary>
    Itf,
    /// <summary>EAN-13 retail product code.</summary>
    Ean13,
    /// <summary>EAN-8 short retail code.</summary>
    Ean8,
    /// <summary>UPC-A 12-digit US retail.</summary>
    UpcA,
    /// <summary>ISBN encoded as EAN-13 (with 978/979 prefix).</summary>
    Isbn,
    /// <summary>ISSN encoded as EAN-13 (with 977 prefix).</summary>
    Issn,
    /// <summary>2D QR Code (versions 1–40, ECC L/M/Q/H).</summary>
    QrCode,
}

/// <summary>Error-correction level for QR codes. Ignored for 1D symbologies.</summary>
public enum QrEccLevel
{
    /// <summary>~7% recovery. Smallest symbol.</summary>
    Low,
    /// <summary>~15% recovery (default).</summary>
    Medium,
    /// <summary>~25% recovery.</summary>
    Quartile,
    /// <summary>~30% recovery. Use when embedding a logo.</summary>
    High,
}

/// <summary>Renders a barcode whose value comes from the evaluated <see cref="Expression"/>.
/// Vector output — the encoder produces module-unit geometry; the renderer scales it
/// into the element bounds.</summary>
public sealed record BarcodeElement : ReportElement
{
    /// <summary>Which symbology to render.</summary>
    public BarcodeSymbology Symbology { get; init; } = BarcodeSymbology.Code128;

    /// <summary>Expression yielding the barcode value (digits, alphanumeric, URL, …).</summary>
    public required string Expression { get; init; }

    /// <summary>If true (default), draw the human-readable text below the symbol.
    /// 1D only — QR ignores this flag (the data is the data).</summary>
    public bool ShowText { get; init; } = true;

    /// <summary>For QR only: error-correction level. Higher = larger symbol but more
    /// damage tolerance. Default <see cref="QrEccLevel.Medium"/> is the sweet spot.</summary>
    public QrEccLevel QrEcc { get; init; } = QrEccLevel.Medium;
}
