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
    private readonly bool _ownsTransport = true;
    private readonly SemaphoreSlim _jobs = new(1, 1);

    /// <summary>Borrows a transport. The caller disposes it after all jobs have completed.</summary>
    public EscPosPrinter(IEscPosTransport transport, EscPosPrinterOptions? options = null)
        : this(_ => Task.FromResult(transport), options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _ownsTransport = false;
    }

    /// <summary>Owns each transport returned by the factory and disposes it after its job, including failure.</summary>
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

        var selected = PrintPageSelection.Enumerate(report.Pages.Count, options);
        await _jobs.WaitAsync(cancellationToken).ConfigureAwait(false);
        IEscPosTransport? transport = null;

        try
        {
            transport = await _transportFactory(cancellationToken).ConfigureAwait(false);
            // Reset the printer once at the start.
            await transport.SendAsync(EscPosCommands.Reset, cancellationToken).ConfigureAwait(false);

            int pagesPrinted = 0;
            foreach (var index in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = RenderPageToEscPos(report.Pages[index], _options);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PrintResult(
                Succeeded: false,
                PagesPrinted: 0,
                ErrorMessage: ex.Message,
                Exception: ex);
        }
        finally
        {
            try { if (_ownsTransport && transport is not null) await transport.DisposeAsync().ConfigureAwait(false); }
            finally { _jobs.Release(); }
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
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            using var context = new SkiaCanvasRenderingContext(canvas, ThermalDpi);
            RenderedReportPlayer.PlayPage(page, context);
            return (bitmap, heightDots);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
