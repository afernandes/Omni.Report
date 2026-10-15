using System.Collections;
using Reporting.Layout;
using Reporting.Rendering.Skia;

namespace Reporting.Designer.Blazor.Services;

/// <summary>Rasterizes requested pages only, retaining at most three encoded images.
/// Each raster context is released before the requested image is returned.</summary>
public sealed class PreviewPageCache : IReadOnlyList<byte[]>
{
    private const long MaxRasterPixels = 16_000_000;
    private readonly Func<int, RenderedPage> _getPage;
    /// <summary>Creates a cache over an existing in-memory document.</summary>
    public PreviewPageCache(RenderedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Count = report.PageCount;
        _getPage = index => report.Pages[index];
    }
    /// <summary>Creates a cache over borrowed prepared storage. The owner disposes the document.</summary>
    public PreviewPageCache(PreparedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Count = report.PageCount;
        _getPage = index => report.GetPage(index);
    }
    private readonly Dictionary<int, byte[]> _images = [];
    private readonly Queue<int> _order = [];
    private readonly object _gate = new();

    /// <summary>Number of pages available without rasterization.</summary>
    public int Count { get; }

    /// <summary>Number of encoded images currently retained.</summary>
    public int CachedPageCount { get { lock (_gate) return _images.Count; } }

    // A 96-DPI raster shown on a 2x screen, or at 200% zoom, is upscaled by the browser and the text blurs. The page
    // is rasterized at 96 × scale instead — the <img> keeps its CSS size, it only gets more pixels.
    private const double MaxScale = 4;
    private const double ScaleStep = 0.25;

    /// <summary>Raster density over 96 DPI: device pixel ratio × zoom. Pages that would exceed the pixel budget at
    /// this scale are rendered at the largest scale that fits.</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>Sets <see cref="Scale"/> (clamped to 1–4, in quarter steps so small zoom changes reuse the cache)
    /// and drops the cached images when it changes.</summary>
    /// <returns><see langword="true"/> when the scale changed and pages will be rasterized again.</returns>
    public bool SetScale(double scale)
    {
        var snapped = Math.Clamp(Math.Ceiling(scale / ScaleStep) * ScaleStep, 1, MaxScale);
        lock (_gate)
        {
            if (snapped == Scale) return false;
            Scale = snapped;
            _images.Clear();
            _order.Clear();
            return true;
        }
    }

    /// <summary>Gets the PNG for a zero-based page index.</summary>
    public byte[] this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            lock (_gate)
            {
                if (_images.TryGetValue(index, out var image)) return image;
                // Evict before allocating the next raster, limiting concurrent cache retention.
                if (_images.Count == 3) _images.Remove(_order.Dequeue());
                var page = _getPage(index);
                var scale = Math.Min(Scale, FittingScale(page));
                using var renderer = new SkiaRenderingContext((float)(96 * scale));
                RenderedReportPlayer.PlayPage(page, renderer);
                image = renderer.GetPagePng(0);
                _images.Add(index, image);
                _order.Enqueue(index);
                return image;
            }
        }
    }

    // The largest scale whose raster stays within the pixel budget; a page that does not fit even at 96 DPI fails.
    private static double FittingScale(RenderedPage page)
    {
        long height = page.PageSetup.PageHeight.Mils;
        if (page.PageSetup.IsContinuous)
        {
            height = Math.Max(40, page.Primitives.Select(primitive =>
                (long)primitive.Bounds.Y.Mils + primitive.Bounds.Height.Mils).DefaultIfEmpty(0).Max()
                + page.PageSetup.Margins.Bottom.Mils);
        }
        var widthPixels = Math.Ceiling(page.PageSetup.PageWidth.Mils * 96.0 / 1000);
        var heightPixels = Math.Ceiling(height * 96.0 / 1000);
        if (widthPixels <= 0 || heightPixels <= 0 || widthPixels * heightPixels > MaxRasterPixels)
            throw new InvalidOperationException($"A página excede o limite de {MaxRasterPixels} pixels do preview.");
        return Math.Sqrt(MaxRasterPixels / (widthPixels * heightPixels));
    }

    /// <summary>Enumerates PNGs on demand. Consumers retaining results own those references.</summary>
    public IEnumerator<byte[]> GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
