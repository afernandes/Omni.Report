using Reporting.Layout;

namespace Reporting.Output.Pdf;

/// <summary>Convenience helpers over <see cref="IReportExporter"/>.</summary>
public static class ReportExporterExtensions
{
    public static byte[] ExportToBytes(this IReportExporter exporter, RenderedReport report)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        using var ms = new MemoryStream();
        exporter.Export(report, ms);
        return ms.ToArray();
    }

    public static void ExportToFile(this IReportExporter exporter, RenderedReport report, string path)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var fs = File.Create(path);
        exporter.Export(report, fs);
    }

    /// <summary>Renders the report into a byte array, honouring <paramref name="cancellationToken"/>.</summary>
    public static async Task<byte[]> ExportToBytesAsync(this IReportExporter exporter, RenderedReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        using var ms = new MemoryStream();
        await exporter.ExportAsync(report, ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    /// <summary>Writes the report to <paramref name="path"/>. The file handle is opened for asynchronous I/O,
    /// so an exporter that overrides <see cref="IReportExporter.ExportAsync"/> writes without blocking.</summary>
    public static async Task ExportToFileAsync(this IReportExporter exporter, RenderedReport report, string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, useAsync: true);
        await exporter.ExportAsync(report, fs, cancellationToken).ConfigureAwait(false);
    }
}
