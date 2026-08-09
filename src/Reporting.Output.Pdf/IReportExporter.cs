using Reporting.Layout;

namespace Reporting.Output.Pdf;

/// <summary>
/// A side-effect-free exporter that converts a paginated <see cref="RenderedReport"/> into
/// some byte representation (PDF, XLSX, …). Implementations are streaming where possible.
/// </summary>
public interface IReportExporter
{
    /// <summary>Identifier of the format (e.g. <c>"pdf"</c>, <c>"xlsx"</c>).</summary>
    string Format { get; }

    /// <summary>Default file extension including the leading dot.</summary>
    string FileExtension { get; }

    /// <summary>MIME content type (used by web hosts / viewers).</summary>
    string ContentType { get; }

    /// <summary>Writes the report to <paramref name="output"/>, blocking until done.</summary>
    void Export(RenderedReport report, Stream output);

    /// <summary>
    /// Asynchronous counterpart of <see cref="Export"/>, and the form a web host should call: the rest of the
    /// pipeline (<c>PaginateAsync</c>, <c>ReadAsync</c>) is already async with a token, so blocking here is
    /// synchronous I/O in the middle of an async request.
    /// </summary>
    /// <remarks>
    /// <para>The default implementation runs <see cref="Export"/> on the calling thread, so an exporter that
    /// does not override this is <b>not</b> truly asynchronous — it only gains cancellation at the points its
    /// own loops check the token. That default exists so external implementers keep compiling; internal
    /// exporters override it as their underlying writer gains async support (<see cref="Format"/>-specific:
    /// several wrap libraries whose write path is synchronous only, e.g. Skia's PDF document and ClosedXML).</para>
    /// <para>Cancellation is observed before any work starts, and at page boundaries by the exporters that
    /// iterate pages.</para>
    /// </remarks>
    Task ExportAsync(RenderedReport report, Stream output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Export(report, output);
        return Task.CompletedTask;
    }
}

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
