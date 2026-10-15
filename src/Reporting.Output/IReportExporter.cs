using Reporting.Layout;

namespace Reporting.Output;

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
    /// <para>The default implementation runs <see cref="Export"/> on the calling thread and writes synchronously to
    /// <paramref name="output"/>; it exists so external implementers keep compiling. The built-in exporters override
    /// it and observe cancellation between pages (or row batches). CSV, JSON, XML and HTML flush to the destination
    /// asynchronously as they go; SVG, DOCX, XLSX and Markdown, whose writer can only emit the whole document, build it
    /// in memory and then copy it asynchronously. PDF, PNG and TIFF stream straight into <paramref name="output"/>
    /// with synchronous writes — buffering a raster document of any size in memory would be worse — so give them a
    /// stream that accepts synchronous I/O (a file, or a buffer the host copies to the response). An external
    /// exporter can use <see cref="AsyncExport"/> for the same patterns.</para>
    /// </remarks>
    Task ExportAsync(RenderedReport report, Stream output, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Export(report, output);
        return Task.CompletedTask;
    }
}
