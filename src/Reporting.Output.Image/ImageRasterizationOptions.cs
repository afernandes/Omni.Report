namespace Reporting.Output.Image;

/// <summary>Limits the raster allocated by one export. Hosts must also bound concurrency
/// and the storage used by destination streams.</summary>
public sealed class ImageRasterizationOptions
{
    /// <summary>Maximum pixels in one raster: the whole stacked PNG, or one TIFF/individual PNG page.
    /// Defaults to 16 million (approximately 61 MiB of RGBA pixels). Encoder and primitive resources
    /// are additional. Raising this limit explicitly permits a larger native allocation.</summary>
    public long MaxRasterPixels { get; init; } = 16_000_000;
}
