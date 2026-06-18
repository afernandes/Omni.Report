using SkiaSharp;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering.Skia;

namespace Reporting.Printing.EscPos;

/// <summary>
/// <see cref="IReportPrinter"/> for ESC/POS thermal printers (Brazilian PDV staples:
/// Bematech, Daruma, Elgin, Epson TM-T*). Renders each page to a 1-bit Skia bitmap at the
/// device's native DPI (203 dpi for 58/80mm rolls), packs the pixels into <c>GS v 0</c>
/// raster commands, and ships them over the supplied <see cref="IEscPosTransport"/>.
/// </summary>
public sealed class EscPosPrinter : IReportPrinter
{
    /// <summary>Standard horizontal dot density for 58/80mm thermal rolls.</summary>
    public const float ThermalDpi = 203f;

    /// <summary>Printable width in dots for 58mm rolls (≈48mm of print area).</summary>
    public const int Dots58mm = 384;

    /// <summary>Printable width in dots for 80mm rolls (≈72mm of print area).</summary>
    public const int Dots80mm = 576;

    private readonly Func<CancellationToken, Task<IEscPosTransport>> _transportFactory;
    private readonly EscPosPrinterOptions _options;

    public EscPosPrinter(IEscPosTransport transport, EscPosPrinterOptions? options = null)
        : this(_ => Task.FromResult(transport), options) { }

    public EscPosPrinter(Func<CancellationToken, Task<IEscPosTransport>> transportFactory,
                        EscPosPrinterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transportFactory);
        _transportFactory = transportFactory;
        _options = options ?? EscPosPrinterOptions.Default;
    }

    public string Driver => "esc-pos";

    public Task<IReadOnlyList<PrinterInfo>> ListPrintersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PrinterInfo>>(
            [new PrinterInfo("esc-pos", IsDefault: true, Driver: Driver, Status: "transport-bound")]);

    public Task<PrinterCapabilities> GetCapabilitiesAsync(string printerName, CancellationToken cancellationToken = default)
        => Task.FromResult(new PrinterCapabilities(
            PrinterName: printerName,
            SupportedPapers: EquatableArray.Create(PaperSize.Thermal58, PaperSize.Thermal80),
            PaperBins: EquatableArray.Create("Roll"),
            SupportsDuplex: false,
            SupportsColor: false));

    public async Task<PrintResult> PrintAsync(RenderedReport report, PrintOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        await using var transport = await _transportFactory(cancellationToken).ConfigureAwait(false);

        try
        {
            // Reset the printer once at the start.
            await transport.SendAsync(EscPosCommands.Reset, cancellationToken).ConfigureAwait(false);

            int pagesPrinted = 0;
            foreach (var page in report.Pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = RenderPageToEscPos(page, _options);
                await transport.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
                pagesPrinted++;
            }

            // Final feed + paper cut.
            if (_options.FeedDotsBeforeCut > 0)
            {
                await transport.SendAsync(EscPosCommands.FeedAndCut((byte)_options.FeedDotsBeforeCut),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await transport.SendAsync(EscPosCommands.FullCut, cancellationToken).ConfigureAwait(false);
            }

            return new PrintResult(Succeeded: true, PagesPrinted: pagesPrinted);
        }
        catch (Exception ex)
        {
            return new PrintResult(
                Succeeded: false,
                PagesPrinted: 0,
                ErrorMessage: ex.Message,
                Exception: ex);
        }
    }

    /// <summary>
    /// Rasterizes a single page to a 1-bit bitmap at <see cref="ThermalDpi"/>, then packs the
    /// pixels into a sequence of <c>GS v 0</c> raster image commands. Returns the complete
    /// byte stream for the page (including the trailing line feed).
    /// </summary>
    internal static byte[] RenderPageToEscPos(RenderedPage page, EscPosPrinterOptions options)
    {
        int dotWidth = ComputeDotWidth(page.PageSetup, options);
        int widthBytes = (dotWidth + 7) / 8;

        // Render the full page to a 1-bit bitmap via SkiaSharp.
        var (bitmap, heightDots) = RenderPageBitmap(page, dotWidth);
        try
        {
            // Pack pixels: row-major, 1 bit per dot, MSB first. The bitmap is converted to
            // grayscale and thresholded — pixels darker than the threshold become "black"
            // (one bit set) in the raster image.
            using var ms = new MemoryStream(capacity: 16 + widthBytes * heightDots);
            ms.Write(EscPosCommands.RasterImageHeader(widthBytes, heightDots));

            for (int y = 0; y < heightDots; y++)
            {
                for (int xByte = 0; xByte < widthBytes; xByte++)
                {
                    byte b = 0;
                    int xBase = xByte * 8;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int x = xBase + bit;
                        if (x >= dotWidth)
                        {
                            break;
                        }
                        var c = bitmap.GetPixel(x, y);
                        // Grayscale luma; threshold at 128.
                        int luma = (299 * c.Red + 587 * c.Green + 114 * c.Blue) / 1000;
                        if (luma < options.BlackThreshold)
                        {
                            b |= (byte)(1 << (7 - bit));
                        }
                    }
                    ms.WriteByte(b);
                }
            }
            ms.WriteByte(EscPosCommands.LF);
            return ms.ToArray();
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static int ComputeDotWidth(PageSetup setup, EscPosPrinterOptions options)
    {
        if (options.ForcedDotWidth is { } forced && forced > 0)
        {
            return forced;
        }
        // Round to the nearest known roll width — printers crop on the right if we send more.
        var widthDots = (int)Math.Round(setup.PageWidth.ToInches() * ThermalDpi);
        return widthDots <= 384 ? Dots58mm : Dots80mm;
    }

    private static (SKBitmap bitmap, int heightDots) RenderPageBitmap(RenderedPage page, int dotWidth)
    {
        // Height is the maximum primitive bottom + a small margin. For non-continuous paper we
        // still trim to the actual rendered content to avoid wasting paper.
        Unit maxBottom = Unit.Zero;
        foreach (var p in page.Primitives)
        {
            if (p.Bounds.Bottom > maxBottom)
            {
                maxBottom = p.Bounds.Bottom;
            }
        }
        if (maxBottom == Unit.Zero)
        {
            maxBottom = page.PageSetup.IsContinuous ? Unit.FromMm(10) : page.PageSetup.PageHeight;
        }
        var heightDots = (int)Math.Ceiling(maxBottom.ToInches() * ThermalDpi);
        if (heightDots < 1)
        {
            heightDots = 1;
        }

        var bitmap = new SKBitmap(new SKImageInfo(dotWidth, heightDots, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            foreach (var primitive in page.Primitives)
            {
                Replay(canvas, primitive);
            }
        }
        return (bitmap, heightDots);
    }

    private static void Replay(SKCanvas canvas, LayoutPrimitive primitive)
    {
        switch (primitive)
        {
            case DrawTextPrimitive t:
                SkiaPrimitiveRenderer.DrawText(canvas, t.Text, t.Bounds, t.Style, ThermalDpi);
                break;
            case DrawLinePrimitive l:
                SkiaPrimitiveRenderer.DrawLine(canvas, l.From, l.To, l.Pen, ThermalDpi);
                break;
            case DrawRectanglePrimitive r:
                SkiaPrimitiveRenderer.DrawRectangle(canvas, r.Bounds, r.Pen, r.Fill, ThermalDpi);
                break;
            case DrawEllipsePrimitive e:
                SkiaPrimitiveRenderer.DrawEllipse(canvas, e.Bounds, e.Pen, e.Fill, ThermalDpi);
                break;
            case DrawImagePrimitive i:
                if (i.Data.Count > 0)
                {
                    var copy = new byte[i.Data.Count];
                    for (int k = 0; k < copy.Length; k++)
                    {
                        copy[k] = i.Data[k];
                    }
                    SkiaPrimitiveRenderer.DrawImage(canvas, copy, i.Bounds, ThermalDpi);
                }
                break;
        }
    }
}

/// <summary>Tunable knobs for the ESC/POS printer driver.</summary>
public sealed record EscPosPrinterOptions
{
    /// <summary>Override the auto-detected dot width (e.g. for non-standard rolls).</summary>
    public int? ForcedDotWidth { get; init; }

    /// <summary>Luma threshold for the 1-bit dithering (0–255). Pixels darker than this
    /// become black ink. Default 128 — fine for most thermal heads.</summary>
    public byte BlackThreshold { get; init; } = 128;

    /// <summary>Number of feed dots (1/8mm each) before cutting. Default 0 = cut immediately
    /// using <c>GS V 0</c>; a positive value uses <c>GS V 65 n</c>.</summary>
    public int FeedDotsBeforeCut { get; init; }

    public static readonly EscPosPrinterOptions Default = new();
}
