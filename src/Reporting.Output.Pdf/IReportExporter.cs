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

    void Export(RenderedReport report, Stream output);
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
}
