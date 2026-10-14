namespace Reporting.Barcode;

/// <summary>The complete vector geometry of an encoded 1D barcode.</summary>
/// <param name="Bars">Every dark bar as a rectangle in module units.</param>
/// <param name="ViewBoxWidth">Total width (modules) including quiet zone on both sides.</param>
/// <param name="ViewBoxHeight">Total height (modules) — typically the bar height.</param>
/// <param name="Checksum">Computed check digit/character, or an empty string when the
/// symbology has no checksum. Useful for displaying the human-readable text strip.</param>
public sealed record BarcodeGeometry(
    IReadOnlyList<BarcodeRect> Bars,
    double ViewBoxWidth,
    double ViewBoxHeight,
    string Checksum);
