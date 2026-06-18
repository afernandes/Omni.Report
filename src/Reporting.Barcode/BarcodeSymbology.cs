namespace Reporting.Barcode;

/// <summary>Supported 1D barcode symbologies. (QR is handled separately via
/// <see cref="QrEncoder"/> because its module geometry is 2D and the API surface
/// differs.)</summary>
public enum BarcodeSymbology
{
    /// <summary>Code 128 subset B — alphanumeric, ASCII 32..127. Most versatile
    /// general-purpose 1D symbology; what shipping labels and most internal SKU
    /// codes use.</summary>
    Code128,

    /// <summary>Code 39 — uppercase + digits + a few symbols. Common in industrial
    /// inventory; trades density for simplicity (no checksum required).</summary>
    Code39,

    /// <summary>Codabar — digits + a few symbols + A/B/C/D start/stop sentinels.
    /// Used in libraries, blood banks, photo labs.</summary>
    Codabar,

    /// <summary>Interleaved 2 of 5 — digits only, must be even-length (the encoder
    /// auto-pads with a leading zero). Dense numeric, popular in logistics.</summary>
    Itf,

    /// <summary>EAN-13 — retail product code, 13 digits. The 13th is the check digit:
    /// pass 12 digits to compute, or 13 to validate.</summary>
    Ean13,

    /// <summary>EAN-8 — short retail product code, 8 digits. Same auto-check
    /// behavior as EAN-13.</summary>
    Ean8,

    /// <summary>UPC-A — North-American retail product code, 12 digits. Numerically
    /// a special-case EAN-13 with a leading zero.</summary>
    UpcA,

    /// <summary>ISBN-10 or ISBN-13 encoded as EAN-13 (with the 978 prefix for
    /// ISBN-10).</summary>
    Isbn,

    /// <summary>ISSN encoded as EAN-13 with the 977 prefix.</summary>
    Issn,
}
