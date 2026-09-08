using Reporting.Layout;
using Reporting.Output.Pdf;
using SkiaSharp;

namespace Reporting.Output.Image;

/// <summary>Writes a multi-page baseline TIFF (little-endian, uncompressed RGB) progressively.
/// Holds one RGBA page and one RGB scanline, rather than all page pixels and a second full-file buffer.
/// Destination streams need not support seeking. The caller owns the destination and any partial output.</summary>
public sealed class TiffImageExporter : IReportExporter
{
    private readonly ImageRasterizer _rasterizer;

    /// <summary>Creates an exporter with the default 16-million-pixel per-page raster limit.</summary>
    public TiffImageExporter(float dpi = 96f) : this(new ImageRasterizationOptions(), dpi) { }

    /// <summary>Creates an exporter with an explicit per-page raster budget and resolution.</summary>
    public TiffImageExporter(ImageRasterizationOptions options, float dpi = 96f)
        => _rasterizer = new ImageRasterizer(dpi, options);

    public string Format => "tiff";
    public string FileExtension => ".tiff";
    public string ContentType => "image/tiff";

    public void Export(RenderedReport report, Stream output) => ExportCore(report, output, CancellationToken.None);

    /// <summary>Writes synchronously, observing cancellation between primitives and scanlines.</summary>
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
        var dimensions = new List<(int W, int H)>(Math.Max(1, report.Pages.Count));
        ulong length = 8;
        foreach (var page in report.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var size = _rasterizer.Dimensions(page);
            dimensions.Add(size);
            ulong bytes = (ulong)size.W * (ulong)size.H * 3;
            length = checked(length + DirectoryBytes + bytes + (bytes & 1));
            if (length > uint.MaxValue)
            {
                throw new InvalidOperationException("O relatório excede o limite de offsets de 32 bits do TIFF baseline.");
            }
        }
        if (dimensions.Count == 0) dimensions.Add((1, 1));

        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write(8u);

        uint offset = 8;
        for (int i = 0; i < dimensions.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (width, height) = dimensions[i];
            uint bytes = checked((uint)((long)width * height * 3));
            uint next = checked(offset + DirectoryBytes + bytes + (bytes & 1));
            writer.Write((ushort)NumEntries);
            Entry(writer, 256, TypeLong, 1, (uint)width);
            Entry(writer, 257, TypeLong, 1, (uint)height);
            Entry(writer, 258, TypeShort, 3, offset + IfdBytes);
            Entry(writer, 259, TypeShort, 1, 1);
            Entry(writer, 262, TypeShort, 1, 2);
            Entry(writer, 273, TypeLong, 1, offset + DirectoryBytes);
            Entry(writer, 277, TypeShort, 1, 3);
            Entry(writer, 278, TypeLong, 1, (uint)height);
            Entry(writer, 279, TypeLong, 1, bytes);
            writer.Write(i < dimensions.Count - 1 ? next : 0u);
            writer.Write((ushort)8);
            writer.Write((ushort)8);
            writer.Write((ushort)8);

            using (var bitmap = _rasterizer.Create(width, height))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.White);
                    if (i < report.Pages.Count) _rasterizer.Draw(canvas, report.Pages[i], cancellationToken);
                }
                WriteRgb(bitmap, writer, cancellationToken);
            }
            if ((bytes & 1) != 0) writer.Write((byte)0); // TIFF directories require word alignment.
            offset = next;
        }
    }

    private static void WriteRgb(SKBitmap bitmap, BinaryWriter writer, CancellationToken cancellationToken)
    {
        var source = bitmap.GetPixelSpan();
        var row = new byte[checked(bitmap.Width * 3)];
        for (int y = 0; y < bitmap.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pixels = source.Slice(y * bitmap.RowBytes, bitmap.Width * 4);
            for (int x = 0; x < bitmap.Width; x++)
            {
                row[x * 3] = pixels[x * 4];
                row[x * 3 + 1] = pixels[x * 4 + 1];
                row[x * 3 + 2] = pixels[x * 4 + 2];
            }
            writer.Write(row);
        }
    }

    private const int NumEntries = 9;
    private const uint IfdBytes = 2 + NumEntries * 12 + 4;
    private const uint DirectoryBytes = IfdBytes + 6;
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;

    private static void Entry(BinaryWriter writer, ushort tag, ushort type, uint count, uint valueOrOffset)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(valueOrOffset);
    }
}
