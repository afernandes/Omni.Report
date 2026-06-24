using SkiaSharp;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;
using Reporting.Rendering.Skia;

namespace Reporting.Output.Image;

/// <summary>
/// Rasterises a <see cref="RenderedReport"/> to a <b>multi-page baseline TIFF</b> (one image per report page)
/// using the same <c>SkiaPrimitiveRenderer</c> as the PNG/PDF/SVG backends, so output is visually identical.
/// <para>
/// The TIFF is written by hand (little-endian, uncompressed RGB, one strip per page) rather than via a native
/// codec: SkiaSharp cannot encode TIFF, and this keeps the exporter dependency-free and cross-platform. Baseline
/// uncompressed RGB is the most broadly-readable TIFF flavour (accepted by GDI+, libtiff, Preview, etc.).
/// </para>
/// </summary>
public sealed class TiffImageExporter : IReportExporter
{
    private readonly float _dpi;

    /// <param name="dpi">Raster resolution. 96 ≈ screen; raise to ~150–300 for print-quality images.</param>
    public TiffImageExporter(float dpi = 96f)
    {
        _dpi = dpi <= 0 ? 96f : dpi;
    }

    public string Format => "tiff";
    public string FileExtension => ".tiff";
    public string ContentType => "image/tiff";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var pages = new List<(int W, int H, byte[] Rgb)>(report.Pages.Count);
        foreach (var page in report.Pages)
        {
            int w = Math.Max(1, Px(page.PageSetup.PageWidth));
            int h = Math.Max(1, PageHeightPx(page));
            using var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                foreach (var primitive in page.Primitives)
                {
                    RegionRasterizer.Replay(canvas, primitive, _dpi);
                }
            }
            pages.Add((w, h, ToRgb(bitmap, w, h)));
        }
        if (pages.Count == 0)
        {
            pages.Add((1, 1, [255, 255, 255])); // a TIFF must carry at least one image directory
        }

        var bytes = EncodeBaselineTiff(pages);
        output.Write(bytes, 0, bytes.Length);
    }

    // RGBA8888 (premultiplied onto an opaque white clear → alpha is 255) → packed RGB, dropping the alpha byte.
    private static byte[] ToRgb(SKBitmap bitmap, int w, int h)
    {
        var src = bitmap.GetPixelSpan();
        int rowBytes = bitmap.RowBytes;
        var rgb = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
        {
            int srcRow = y * rowBytes;
            int dstRow = y * w * 3;
            for (int x = 0; x < w; x++)
            {
                int s = srcRow + (x * 4);
                int d = dstRow + (x * 3);
                rgb[d] = src[s];         // R
                rgb[d + 1] = src[s + 1]; // G
                rgb[d + 2] = src[s + 2]; // B
            }
        }
        return rgb;
    }

    // Baseline little-endian TIFF: header + one IFD per page, each with an uncompressed RGB strip. Offsets are
    // laid out as [IFD(114B)][BitsPerSample(6B)][strip(W*H*3)] per page, chained via the next-IFD pointer.
    private static byte[] EncodeBaselineTiff(IReadOnlyList<(int W, int H, byte[] Rgb)> pages)
    {
        const int ifdSize = 2 + (NumEntries * 12) + 4; // entry count + entries + next-IFD offset
        const int bpsSize = 6;                          // BitsPerSample = three 16-bit values (8,8,8)

        var ifdOffset = new uint[pages.Count];
        var bpsOffset = new uint[pages.Count];
        var stripOffset = new uint[pages.Count];
        uint cursor = 8; // first IFD sits right after the 8-byte header
        for (int i = 0; i < pages.Count; i++)
        {
            ifdOffset[i] = cursor;
            bpsOffset[i] = cursor + ifdSize;
            stripOffset[i] = cursor + ifdSize + bpsSize;
            cursor = stripOffset[i] + (uint)pages[i].Rgb.Length;
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        // Header: "II" (little-endian) + magic 42 + offset to the first IFD.
        w.Write((byte)'I');
        w.Write((byte)'I');
        w.Write((ushort)42);
        w.Write(8u);

        for (int i = 0; i < pages.Count; i++)
        {
            var (width, height, rgb) = pages[i];
            w.Write((ushort)NumEntries);
            Entry(w, 256, TypeShort, 1, (uint)width);        // ImageWidth
            Entry(w, 257, TypeShort, 1, (uint)height);       // ImageLength
            Entry(w, 258, TypeShort, 3, bpsOffset[i]);       // BitsPerSample → [8,8,8] at offset
            Entry(w, 259, TypeShort, 1, 1);                  // Compression = none
            Entry(w, 262, TypeShort, 1, 2);                  // PhotometricInterpretation = RGB
            Entry(w, 273, TypeLong, 1, stripOffset[i]);      // StripOffsets
            Entry(w, 277, TypeShort, 1, 3);                  // SamplesPerPixel = 3
            Entry(w, 278, TypeLong, 1, (uint)height);        // RowsPerStrip = whole image
            Entry(w, 279, TypeLong, 1, (uint)rgb.Length);    // StripByteCounts
            w.Write(i < pages.Count - 1 ? ifdOffset[i + 1] : 0u); // next IFD (0 = last page)

            w.Write((ushort)8);
            w.Write((ushort)8);
            w.Write((ushort)8); // BitsPerSample data
            w.Write(rgb);        // strip
        }
        return ms.ToArray();
    }

    private const int NumEntries = 9;
    private const ushort TypeShort = 3; // 16-bit unsigned
    private const ushort TypeLong = 4;  // 32-bit unsigned

    // One 12-byte IFD entry: tag, type, count, then the value (SHORT count-1 lands in the low 2 bytes) or offset.
    private static void Entry(BinaryWriter w, ushort tag, ushort type, uint count, uint valueOrOffset)
    {
        w.Write(tag);
        w.Write(type);
        w.Write(count);
        w.Write(valueOrOffset);
    }

    private int Px(Unit u) => (int)Math.Ceiling(u.ToPoints() / 72.0 * _dpi);

    private int PageHeightPx(RenderedPage page)
    {
        if (!page.PageSetup.IsContinuous)
        {
            return Px(page.PageSetup.PageHeight);
        }
        Unit maxBottom = Unit.Zero;
        foreach (var p in page.Primitives)
        {
            if (p.Bounds.Bottom > maxBottom)
            {
                maxBottom = p.Bounds.Bottom;
            }
        }
        return Math.Max(1, Px(maxBottom + page.PageSetup.Margins.Bottom));
    }
}
