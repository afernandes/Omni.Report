using FluentAssertions;
using Reporting.Printing.EscPos;
using Xunit;

namespace Reporting.Printing.EscPos.Tests;

public class EscPosCommandsTests
{
    [Fact]
    public void Reset_is_ESC_at_sign()
    {
        EscPosCommandsAccessor.Reset.Should().Equal([0x1B, (byte)'@']);
    }

    [Fact]
    public void Full_cut_is_GS_V_0()
    {
        EscPosCommandsAccessor.FullCut.Should().Equal([0x1D, (byte)'V', 0x00]);
    }

    [Fact]
    public void Partial_cut_is_GS_V_1()
    {
        EscPosCommandsAccessor.PartialCut.Should().Equal([0x1D, (byte)'V', 0x01]);
    }

    [Fact]
    public void Feed_and_cut_emits_GS_V_65_n()
    {
        EscPosCommandsAccessor.FeedAndCut(20).Should().Equal([0x1D, (byte)'V', 65, 20]);
    }

    [Fact]
    public void Raster_header_packs_width_and_height_little_endian()
    {
        // 48 bytes wide × 200 dots tall, normal mode.
        var header = EscPosCommandsAccessor.RasterImageHeader(48, 200, RasterMode.Normal);
        header.Should().HaveCount(8);
        header[0].Should().Be(0x1D); // GS
        header[1].Should().Be((byte)'v');
        header[2].Should().Be((byte)'0');
        header[3].Should().Be(0); // normal mode
        // Width in bytes, little-endian
        ((header[5] << 8) | header[4]).Should().Be(48);
        // Height in dots, little-endian
        ((header[7] << 8) | header[6]).Should().Be(200);
    }

    [Fact]
    public void Feed_dots_emits_ESC_J_n()
    {
        EscPosCommandsAccessor.FeedDots(40).Should().Equal([0x1B, (byte)'J', 40]);
    }

    [Theory]
    [InlineData(RasterMode.Normal, 0)]
    [InlineData(RasterMode.DoubleWidth, 1)]
    [InlineData(RasterMode.DoubleHeight, 2)]
    [InlineData(RasterMode.Quadruple, 3)]
    public void Raster_modes_serialize_correctly(RasterMode mode, byte expected)
    {
        var header = EscPosCommandsAccessor.RasterImageHeader(10, 10, mode);
        header[3].Should().Be(expected);
    }

    [Fact]
    public void Raster_header_rejects_out_of_range_dimensions()
    {
        Action wide = () => EscPosCommandsAccessor.RasterImageHeader(70000, 100);
        wide.Should().Throw<ArgumentOutOfRangeException>();
        Action tall = () => EscPosCommandsAccessor.RasterImageHeader(10, 70000);
        tall.Should().Throw<ArgumentOutOfRangeException>();
    }
}

/// <summary>InternalsVisibleTo bridge — the constants are internal to the production
/// project, so we expose them through this helper that the Directory.Build.props grants
/// access to via the auto-applied test InternalsVisibleTo.</summary>
internal static class EscPosCommandsAccessor
{
    public static byte[] Reset => EscPosCommands.Reset;
    public static byte[] LineFeed => EscPosCommands.LineFeed;
    public static byte[] FullCut => EscPosCommands.FullCut;
    public static byte[] PartialCut => EscPosCommands.PartialCut;
    public static byte[] FeedAndCut(byte n) => EscPosCommands.FeedAndCut(n);
    public static byte[] FeedDots(byte dots) => EscPosCommands.FeedDots(dots);
    public static byte[] RasterImageHeader(int widthBytes, int heightDots, RasterMode mode = RasterMode.Normal)
        => EscPosCommands.RasterImageHeader(widthBytes, heightDots, mode);
}
