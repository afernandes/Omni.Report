namespace Reporting.Printing.EscPos;

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
