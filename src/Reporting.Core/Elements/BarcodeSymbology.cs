using Reporting.Metadata;

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
