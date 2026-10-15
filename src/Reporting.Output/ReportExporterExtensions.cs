using Reporting.Common;
using Reporting.Layout;

namespace Reporting.Output;

/// <summary>Convenience helpers over <see cref="IReportExporter"/>.</summary>
public static class ReportExporterExtensions
{
    /// <summary>Exports <paramref name="report"/> into a byte array (whole document in memory).</summary>
    /// <param name="exporter">The exporter to run.</param>
    /// <param name="report">The paginated report.</param>
    /// <returns>The encoded document.</returns>
    public static byte[] ExportToBytes(this IReportExporter exporter, RenderedReport report)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        using var ms = new MemoryStream();
        exporter.Export(report, ms);
        return ms.ToArray();
    }

    /// <summary>Exports <paramref name="report"/> to the file at <paramref name="path"/>, creating or replacing it.</summary>
    /// <param name="exporter">The exporter to run.</param>
    /// <param name="report">The paginated report.</param>
    /// <param name="path">Destination file path.</param>
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

    /// <summary>Exports only the pages of <paramref name="pages"/> from an already paginated report.</summary>
    /// <param name="exporter">The exporter to run.</param>
    /// <param name="report">The paginated report.</param>
    /// <param name="output">Destination stream.</param>
    /// <param name="pages">Pages to export, 1-based and inclusive.</param>
    /// <param name="cancellationToken">Observed before the export starts and by the exporter.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range ends after the last page.</exception>
    public static Task ExportAsync(this IReportExporter exporter, RenderedReport report, Stream output,
        PageRange pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(report);
        pages.EnsureWithin(report.PageCount);
        var slice = report with { Pages = new EquatableArray<RenderedPage>(report.Pages.Skip(pages.From - 1).Take(pages.Count)) };
        return exporter.ExportAsync(slice, output, cancellationToken);
    }

    /// <summary>Exports a <see cref="PreparedReport"/> — all of it, or only <paramref name="pages"/>. Only the pages
    /// exported are read back from the prepared storage, so exporting pages 3–7 of a long report does not load the
    /// rest into memory.</summary>
    /// <param name="exporter">The exporter to run.</param>
    /// <param name="report">The prepared report; it stays open and owned by the caller.</param>
    /// <param name="output">Destination stream.</param>
    /// <param name="pages">Pages to export, 1-based and inclusive; <see langword="null"/> exports every page.</param>
    /// <param name="cancellationToken">Observed while the pages are read and by the exporter.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range ends after the last page.</exception>
    public static Task ExportAsync(this IReportExporter exporter, PreparedReport report, Stream output,
        PageRange? pages = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(report);
        var range = pages ?? (report.PageCount == 0 ? default(PageRange?) : new PageRange(1, report.PageCount));
        range?.EnsureWithin(report.PageCount);
        var selected = range is { } r
            ? Enumerable.Range(r.From - 1, r.Count).Select(index => report.GetPage(index, cancellationToken)).ToArray()
            : [];
        var rendered = new RenderedReport(report.Name, new EquatableArray<RenderedPage>(selected))
        {
            Diagnostics = report.Diagnostics,
        };
        return exporter.ExportAsync(rendered, output, cancellationToken);
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
