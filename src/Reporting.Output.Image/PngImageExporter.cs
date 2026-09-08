using Reporting.Layout;
using Reporting.Output.Pdf;
using SkiaSharp;

namespace Reporting.Output.Image;

/// <summary>Exports a single vertically stacked PNG subject to a raster pixel budget.
/// Use <see cref="ExportPages"/> for progressive output without a report-sized bitmap.</summary>
public sealed class PngImageExporter : IReportExporter
{
    private readonly ImageRasterizer _rasterizer;
    private readonly int _pageGapPx;

    /// <summary>Creates an exporter with the default 16-million-pixel raster limit.</summary>
    public PngImageExporter(float dpi = 96f, int pageGapPx = 8)
        : this(new ImageRasterizationOptions(), dpi, pageGapPx) { }

    /// <summary>Creates an exporter with an explicit raster budget, resolution and vertical page gap.</summary>
    public PngImageExporter(ImageRasterizationOptions options, float dpi = 96f, int pageGapPx = 8)
    {
        _rasterizer = new ImageRasterizer(dpi, options);
        _pageGapPx = Math.Max(0, pageGapPx);
    }

    public string Format => "png";
    public string FileExtension => ".png";
    public string ContentType => "image/png";

    /// <summary>Returns one PNG per page, retaining all encoded byte arrays in the result.
    /// Prefer <see cref="ExportPages"/> when the consumer can store each page immediately.</summary>
    public IReadOnlyList<byte[]> RenderPages(RenderedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var result = new List<byte[]>(report.Pages.Count);
        foreach (var page in report.Pages)
        {
            var (width, height) = _rasterizer.Dimensions(page);
            using var output = new MemoryStream();
            ExportPage(page, width, height, output, CancellationToken.None);
            result.Add(output.ToArray());
        }
        return result;
    }

    /// <summary>Writes one PNG at a time. The factory receives a 1-based page index and returns a new
    /// destination owned and disposed by this method, including on cancellation or failure.
    /// File streams or ZIP entry streams allow progressive output without retaining encoded pages.
    /// The consumer owns partial files left by a failure. All page dimensions are checked before opening outputs.</summary>
    public void ExportPages(RenderedReport report, Func<int, Stream> createOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(createOutput);
        cancellationToken.ThrowIfCancellationRequested();
        var dimensions = new List<(int W, int H)>(report.Pages.Count);
        foreach (var page in report.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            dimensions.Add(_rasterizer.Dimensions(page));
        }
        for (int i = 0; i < report.Pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var output = createOutput(i + 1)
                ?? throw new InvalidOperationException("A fábrica não retornou um stream de saída.");
            var (width, height) = dimensions[i];
            ExportPage(report.Pages[i], width, height, output, cancellationToken);
        }
    }

    private void ExportPage(RenderedPage page, int width, int height, Stream output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = _rasterizer.Create(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            _rasterizer.Draw(canvas, page, cancellationToken);
        }
        Encode(bitmap, output, cancellationToken);
    }

    /// <summary>Writes a single stacked PNG; rejects images exceeding the budget before allocating or writing.
    /// The caller retains ownership of the destination stream.</summary>
    public void Export(RenderedReport report, Stream output) => ExportCore(report, output, CancellationToken.None);

    /// <summary>Synchronously rasterises and encodes, observing cancellation between primitives and before encoding.</summary>
    public Task ExportAsync(RenderedReport report, Stream output, CancellationToken cancellationToken = default)
    {
        ExportCore(report, output, cancellationToken);
        return Task.CompletedTask;
    }

    private void ExportCore(RenderedReport report, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();
        var dimensions = new List<(int W, int H)>(report.Pages.Count);
        int width = 1;
        long height = 0;
        foreach (var page in report.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var size = _rasterizer.Dimensions(page);
            dimensions.Add(size);
            width = Math.Max(width, size.W);
            height = checked(height + size.H);
        }
        height = Math.Max(1, checked(height + Math.Max(0L, report.Pages.Count - 1L) * _pageGapPx));
        _rasterizer.Validate(width, height);
        using var bitmap = _rasterizer.Create(width, (int)height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            long y = 0;
            for (int i = 0; i < report.Pages.Count; i++)
            {
                canvas.Save();
                canvas.Translate(0, y);
                _rasterizer.Draw(canvas, report.Pages[i], cancellationToken);
                canvas.Restore();
                y += (long)dimensions[i].H + _pageGapPx;
            }
        }
        Encode(bitmap, output, cancellationToken);
    }

    private static void Encode(SKBitmap bitmap, Stream output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Não foi possível codificar a imagem PNG.");
        cancellationToken.ThrowIfCancellationRequested();
        data.SaveTo(output);
    }
}
