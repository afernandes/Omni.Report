using System.Buffers.Binary;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Image;
using Reporting.Output.Pdf;
using Xunit;

namespace Reporting.Output.Tests;

/// <summary>
/// The TIFF exporter writes a multi-page baseline (uncompressed RGB) TIFF by hand. We verify the byte structure
/// independently (header + IFD chain + tags) on every platform, and additionally decode it with the GDI+ codec
/// on Windows so a real-world reader confirms the hand-rolled bytes.
/// </summary>
public class TiffExporterTests
{
    private static Report BuildReport(int rows) =>
        ReportBuilder.Create("Tiff")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Enumerable.Range(0, rows).Select(i => new Linha($"Item {i}", i.ToString())).ToArray())
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 6))
            .Build();

    [Fact]
    public void Declares_tiff_format_metadata()
    {
        var exporter = new TiffImageExporter();
        exporter.Format.Should().Be("tiff");
        exporter.FileExtension.Should().Be(".tiff");
        exporter.ContentType.Should().Be("image/tiff");
    }

    [Fact]
    public async Task Writes_a_well_formed_multi_page_baseline_tiff()
    {
        // Enough rows to force more than one A4 page → more than one IFD.
        var rendered = await BuildReport(400).PaginateAsync();
        rendered.Pages.Count.Should().BeGreaterThan(1, "the report should paginate to several pages");

        var bytes = new TiffImageExporter().ExportToBytes(rendered);

        // Header: "II" little-endian, magic 42, first IFD offset.
        bytes.Length.Should().BeGreaterThan(8);
        bytes[0].Should().Be((byte)'I');
        bytes[1].Should().Be((byte)'I');
        ReadU16(bytes, 2).Should().Be(42);
        uint ifd = ReadU32(bytes, 4);
        ifd.Should().Be(8u);

        // Walk the IFD chain; each directory must be a valid baseline RGB strip image.
        int directories = 0;
        while (ifd != 0)
        {
            ifd.Should().BeLessThan((uint)bytes.Length, "an IFD offset must point inside the file");
            int count = ReadU16(bytes, (int)ifd);
            count.Should().Be(9, "the baseline writer emits exactly nine tags");

            var tags = new Dictionary<ushort, (ushort Type, uint Count, uint Value)>();
            for (int e = 0; e < count; e++)
            {
                int at = (int)ifd + 2 + (e * 12);
                ushort tag = ReadU16(bytes, at);
                tags[tag] = (ReadU16(bytes, at + 2), ReadU32(bytes, at + 4), ReadU32(bytes, at + 8));
            }

            tags[256].Value.Should().BeGreaterThan(0); // ImageWidth
            tags[257].Value.Should().BeGreaterThan(0); // ImageLength
            tags[259].Value.Should().Be(1);            // Compression = none
            tags[262].Value.Should().Be(2);            // Photometric = RGB
            tags[277].Value.Should().Be(3);            // SamplesPerPixel
            // BitsPerSample is three SHORTs stored at an offset → [8,8,8].
            uint bps = tags[258].Value;
            ReadU16(bytes, (int)bps).Should().Be(8);
            ReadU16(bytes, (int)bps + 2).Should().Be(8);
            ReadU16(bytes, (int)bps + 4).Should().Be(8);
            // The strip must hold exactly width*height*3 bytes and lie within the file.
            uint strip = tags[273].Value;
            uint stripLen = tags[279].Value;
            stripLen.Should().Be(tags[256].Value * tags[257].Value * 3u);
            (strip + stripLen).Should().BeLessThanOrEqualTo((uint)bytes.Length);

            directories++;
            int nextAt = (int)ifd + 2 + (count * 12);
            ifd = ReadU32(bytes, nextAt);
        }

        directories.Should().Be(rendered.Pages.Count, "one TIFF directory (frame) per report page");
    }

    [Fact]
    public async Task A_real_decoder_reads_the_pages_and_size_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // GDI+ (System.Drawing) is Windows-only; the structural test covers other platforms.
        }

        var rendered = await BuildReport(400).PaginateAsync();
        var bytes = new TiffImageExporter().ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
#pragma warning disable CA1416 // guarded by OperatingSystem.IsWindows above
        using var img = System.Drawing.Image.FromStream(ms);
        var pageDim = new System.Drawing.Imaging.FrameDimension(img.FrameDimensionsList[0]);
        img.GetFrameCount(pageDim).Should().Be(rendered.Pages.Count, "GDI+ sees one frame per report page");
        img.Width.Should().BeGreaterThan(0);
        img.Height.Should().BeGreaterThan(0);
#pragma warning restore CA1416
    }

    private static ushort ReadU16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at, 2));
    private static uint ReadU32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at, 4));
}
