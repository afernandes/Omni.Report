namespace Reporting.Layout;

/// <summary>Storage policy for a prepared document. Temporary files are deleted on disposal or failure.</summary>
public sealed record PreparedReportOptions
{
    /// <summary>Directory for private temporary files. Null uses the operating system temporary directory.</summary>
    public string? TemporaryDirectory { get; init; }

    /// <summary>Maximum serialized bytes written to temporary storage. Defaults to 256 MiB.
    /// This controls disk usage; one page is serialized in memory before the check.</summary>
    public long MaxStorageBytes { get; init; } = 256L * 1024 * 1024;
}
