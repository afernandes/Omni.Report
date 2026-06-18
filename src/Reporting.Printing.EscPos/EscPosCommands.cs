namespace Reporting.Printing.EscPos;

/// <summary>Constants for the ESC/POS byte-level command set, as documented in the
/// Epson FX/TM-T family programmer's reference and supported by Bematech, Daruma,
/// Elgin and most Brazilian thermal printers.</summary>
internal static class EscPosCommands
{
    public const byte ESC = 0x1B;
    public const byte GS = 0x1D;
    public const byte LF = 0x0A;
    public const byte FF = 0x0C;
    public const byte HT = 0x09;
    public const byte CR = 0x0D;
    public const byte NUL = 0x00;

    /// <summary><c>ESC @</c> — initialize printer (reset).</summary>
    public static readonly byte[] Reset = { ESC, (byte)'@' };

    /// <summary><c>LF</c> — line feed (1 line).</summary>
    public static readonly byte[] LineFeed = { LF };

    /// <summary><c>ESC J n</c> — advance paper n × 0.125mm. Convenient for spacing
    /// between bands when feeding extra paper after a page.</summary>
    public static byte[] FeedDots(byte dots) => [ESC, (byte)'J', dots];

    /// <summary><c>GS V 0</c> — full paper cut. <c>GS V 1</c> = partial cut.</summary>
    public static readonly byte[] FullCut = { GS, (byte)'V', 0 };
    public static readonly byte[] PartialCut = { GS, (byte)'V', 1 };

    /// <summary><c>GS V m n</c> — feed n × 0.125mm then cut (mode m).</summary>
    public static byte[] FeedAndCut(byte n) => [GS, (byte)'V', 65, n];

    /// <summary>Encodes a raster bit image (<c>GS v 0</c>) header for the given dimensions.
    /// Width is in BYTES (each byte = 8 horizontal dots); height in DOTS.</summary>
    public static byte[] RasterImageHeader(int widthBytes, int heightDots, RasterMode mode = RasterMode.Normal)
    {
        if (widthBytes < 0 || widthBytes > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(widthBytes));
        }
        if (heightDots < 0 || heightDots > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(heightDots));
        }
        return
        [
            GS, (byte)'v', (byte)'0', (byte)mode,
            (byte)(widthBytes & 0xFF), (byte)((widthBytes >> 8) & 0xFF),
            (byte)(heightDots & 0xFF), (byte)((heightDots >> 8) & 0xFF),
        ];
    }
}

public enum RasterMode : byte
{
    Normal = 0,
    DoubleWidth = 1,
    DoubleHeight = 2,
    Quadruple = 3,
}
