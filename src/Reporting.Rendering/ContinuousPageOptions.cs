using Reporting.Geometry;

namespace Reporting.Rendering;

/// <summary>Per-page limits for direct rendering to continuous paper. These are not a host-wide memory budget.</summary>
public sealed record ContinuousPageOptions
{
    /// <summary>Maximum physical height, including margins. Defaults to ten metres.</summary>
    public Unit MaxHeight { get; init; } = Unit.FromMm(10_000);

    /// <summary>Maximum width times height of a continuous bitmap. Defaults to 16 million pixels.</summary>
    public long MaxRasterPixels { get; init; } = 16_000_000;

    /// <summary>Maximum drawing and clipping operations recorded for one page.</summary>
    public int MaxOperations { get; init; } = 100_000;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxHeight.Mils);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRasterPixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxOperations);
    }
}
