using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>Renders a barcode whose value comes from the evaluated <see cref="Expression"/>.
/// Vector output — the encoder produces module-unit geometry; the renderer scales it
/// into the element bounds.</summary>
[TextStyled]
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
