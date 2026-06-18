using Reporting.Barcode;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Rendering;
using Reporting.Styling;
using CoreBarcodeSymbology = Reporting.Elements.BarcodeSymbology;
using EncoderSymbology = Reporting.Barcode.BarcodeSymbology;

namespace Reporting.Layout.Internal;

/// <summary>
/// Vector renderer for <see cref="BarcodeElement"/>: takes the element bounds, runs the
/// encoder, and emits one <see cref="DrawRectanglePrimitive"/> per bar/module. The output
/// is pure vectors — PDFs scale lossless and even thermal printers receive
/// resolution-independent geometry.
/// </summary>
internal static class BarcodeRenderer
{
    /// <summary>Encodes <paramref name="value"/> and emits primitives that fill the element's
    /// declared rectangle. Sets <paramref name="emitted"/> to true when geometry was produced;
    /// false when the value is empty or the encoder rejected the input (the caller can then
    /// log/fallback without crashing the entire report).</summary>
    public static IEnumerable<LayoutPrimitive> Render(
        BarcodeElement element,
        Rectangle bounds,
        string value,
        Color foreColor,
        Reporting.Rendering.TextStyle? textStrip,
        out bool emitted)
    {
        var list = new List<LayoutPrimitive>(64);
        emitted = false;
        if (string.IsNullOrEmpty(value)) return list;

        try
        {
            if (element.Symbology == CoreBarcodeSymbology.QrCode)
            {
                RenderQr(element, bounds, value, foreColor, list);
            }
            else
            {
                Render1D(element, bounds, value, foreColor, textStrip, list);
            }
            emitted = list.Count > 0;
        }
        catch (ArgumentException)
        {
            // Encoder rejected the input (wrong digit count, invalid character, etc.).
            // Emit nothing — the band's CanGrow/visibility logic handles the empty slot,
            // and the designer's expression-error UI will surface the problem.
        }

        return list;
    }

    private static void Render1D(BarcodeElement element, Rectangle bounds, string value, Color foreColor, Reporting.Rendering.TextStyle? textStrip, List<LayoutPrimitive> list)
    {
        var symbology = Map(element.Symbology);
        // Reserve ~15% of the height for the human-readable text strip (Crystal/SSRS default).
        // Skip the reservation when ShowText is false or when there's no vertical room.
        var totalHeightMm = bounds.Height.ToMm();
        double textStripMm = element.ShowText ? Math.Min(3.5, totalHeightMm * 0.18) : 0;
        double barHeightMm = Math.Max(1, totalHeightMm - textStripMm);

        // Encode in module units — the encoder doesn't know our physical size.
        // We then scale to fit the element's bounds horizontally.
        var geometry = Barcode1DEncoder.Encode(symbology, value, barHeight: 50, quietZoneModules: 10);
        var scaleX = bounds.Width.ToMm() / geometry.ViewBoxWidth;
        var scaleY = barHeightMm / geometry.ViewBoxHeight;

        var fill = new BrushStyle(foreColor);

        foreach (var bar in geometry.Bars)
        {
            var x = Unit.FromMm(bounds.X.ToMm() + bar.X * scaleX);
            var y = Unit.FromMm(bounds.Y.ToMm() + bar.Y * scaleY);
            var w = Unit.FromMm(bar.Width * scaleX);
            var h = Unit.FromMm(bar.Height * scaleY);
            list.Add(new DrawRectanglePrimitive
            {
                Bounds = new Rectangle(x, y, w, h),
                SourceElementId = element.Id,
                Fill = fill,
                Pen = null,
            });
        }

        if (element.ShowText && textStripMm > 0.5 && textStrip is not null)
        {
            // Build the readable text strip: <value><checksum> when the encoder produced a
            // checksum (EAN/UPC), otherwise just <value>. EAN/UPC convention is to also show
            // the leading digit slightly outside the symbol, but a single centered string is
            // a pragmatic default the user can override by adding a LabelElement below.
            var display = geometry.Checksum.Length > 0 ? value + geometry.Checksum : value;
            list.Add(new DrawTextPrimitive
            {
                Text = display,
                Bounds = new Rectangle(
                    bounds.X,
                    Unit.FromMm(bounds.Y.ToMm() + barHeightMm),
                    bounds.Width,
                    Unit.FromMm(textStripMm)),
                Style = textStrip,
                SourceElementId = element.Id,
            });
        }
    }

    private static void RenderQr(BarcodeElement element, Rectangle bounds, string value, Color foreColor, List<LayoutPrimitive> list)
    {
        var matrix = QrEncoder.EncodeUtf8(value, MapEcc(element.QrEcc));
        var geometry = QrEncoder.ToGeometry(matrix, quietZoneModules: 4);

        // QR is square — fit to the SMALLER of the element's dimensions and center.
        var sideMm = Math.Min(bounds.Width.ToMm(), bounds.Height.ToMm());
        var scale = sideMm / geometry.ViewBoxWidth;
        var offsetX = bounds.X.ToMm() + (bounds.Width.ToMm() - sideMm) / 2;
        var offsetY = bounds.Y.ToMm() + (bounds.Height.ToMm() - sideMm) / 2;

        var fill = new BrushStyle(foreColor);

        foreach (var module in geometry.Bars)
        {
            // Each QR "bar" is a single dark module — width = height = 1 module unit.
            var x = Unit.FromMm(offsetX + module.X * scale);
            var y = Unit.FromMm(offsetY + module.Y * scale);
            var s = Unit.FromMm(scale);
            list.Add(new DrawRectanglePrimitive
            {
                Bounds = new Rectangle(x, y, s, s),
                SourceElementId = element.Id,
                Fill = fill,
                Pen = null,
            });
        }
    }

    private static EncoderSymbology Map(CoreBarcodeSymbology s) => s switch
    {
        CoreBarcodeSymbology.Code128 => EncoderSymbology.Code128,
        CoreBarcodeSymbology.Code39  => EncoderSymbology.Code39,
        CoreBarcodeSymbology.Codabar => EncoderSymbology.Codabar,
        CoreBarcodeSymbology.Itf     => EncoderSymbology.Itf,
        CoreBarcodeSymbology.Ean13   => EncoderSymbology.Ean13,
        CoreBarcodeSymbology.Ean8    => EncoderSymbology.Ean8,
        CoreBarcodeSymbology.UpcA    => EncoderSymbology.UpcA,
        CoreBarcodeSymbology.Isbn    => EncoderSymbology.Isbn,
        CoreBarcodeSymbology.Issn    => EncoderSymbology.Issn,
        // QR is handled separately — never reaches this map.
        _ => throw new ArgumentException($"1D path doesn't handle {s}; QR uses RenderQr."),
    };

    private static QrErrorCorrection MapEcc(QrEccLevel e) => e switch
    {
        QrEccLevel.Low      => QrErrorCorrection.Low,
        QrEccLevel.Medium   => QrErrorCorrection.Medium,
        QrEccLevel.Quartile => QrErrorCorrection.Quartile,
        QrEccLevel.High     => QrErrorCorrection.High,
        _ => QrErrorCorrection.Medium,
    };
}
