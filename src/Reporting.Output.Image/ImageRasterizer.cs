using Reporting.Layout;
using SkiaSharp;

namespace Reporting.Output.Image;

internal sealed class ImageRasterizer
{
    internal float Dpi { get; }
    private readonly long _maxPixels;

    internal ImageRasterizer(float dpi, ImageRasterizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!float.IsFinite(dpi)) throw new ArgumentOutOfRangeException(nameof(dpi));
        if (options.MaxRasterPixels <= 0 || options.MaxRasterPixels > int.MaxValue / 4)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "O limite de pixels deve ser positivo e caber em um raster RGBA de 2 GiB.");
        }
        Dpi = dpi <= 0 ? 96f : dpi;
        _maxPixels = options.MaxRasterPixels;
    }

    internal (int W, int H) Dimensions(RenderedPage page)
    {
        long height = page.PageSetup.PageHeight.Mils;
        if (page.PageSetup.IsContinuous)
        {
            height = 0;
            foreach (var primitive in page.Primitives)
            {
                height = Math.Max(height, (long)primitive.Bounds.Y.Mils + primitive.Bounds.Height.Mils);
            }
            height += page.PageSetup.Margins.Bottom.Mils;
        }
        var dimensions = (W: Pixels(page.PageSetup.PageWidth.Mils), H: Pixels(height));
        Validate(dimensions.W, dimensions.H);
        return dimensions;
    }

    private int Pixels(long mils)
    {
        var pixels = Math.Ceiling(mils / 1000.0 * Dpi);
        if (!double.IsFinite(pixels) || pixels > int.MaxValue || pixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mils), "As dimensões da imagem excedem o intervalo suportado.");
        }
        return Math.Max(1, (int)pixels);
    }

    internal void Validate(long width, long height)
    {
        if (width <= 0 || height <= 0 || width > int.MaxValue || height > int.MaxValue
            || width > _maxPixels / height)
        {
            throw new InvalidOperationException($"O raster de {width} × {height} excede o limite de {_maxPixels} pixels. Exporte PNG por página com ExportPages ou configure explicitamente MaxRasterPixels.");
        }
    }

    internal SKBitmap Create(int width, int height)
    {
        Validate(width, height);
        var bitmap = new SKBitmap();
        if (!bitmap.TryAllocPixels(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            bitmap.Dispose();
            throw new InvalidOperationException("Não foi possível alocar o raster dentro do limite configurado.");
        }
        return bitmap;
    }

    internal void Draw(SKCanvas canvas, RenderedPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var primitive in page.Primitives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RegionRasterizer.Replay(canvas, primitive, Dpi);
        }
    }
}
