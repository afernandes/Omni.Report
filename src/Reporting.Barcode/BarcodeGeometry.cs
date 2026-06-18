namespace Reporting.Barcode;

/// <summary>
/// A single dark rectangle in a barcode's geometry. Coordinates are in **module units**:
/// the encoder doesn't know your physical size — the renderer multiplies by a chosen
/// module width (e.g. 0.33 mm per module for a typical retail symbol). Y=0 is the top
/// of the symbol, X grows to the right.
/// </summary>
public readonly struct BarcodeRect(double x, double y, double width, double height)
{
    /// <summary>Left edge in module units.</summary>
    public readonly double X = x;
    /// <summary>Top edge in module units.</summary>
    public readonly double Y = y;
    /// <summary>Width in module units (≥ 1 for a normal bar; can be wider for "wide" bars).</summary>
    public readonly double Width = width;
    /// <summary>Height in module units (full height for most 1D symbols).</summary>
    public readonly double Height = height;
}

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
