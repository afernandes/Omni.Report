using Microsoft.Extensions.DependencyInjection;

namespace Reporting.Output;

/// <summary>Registers the first-party exporters in a host's container.</summary>
public static class OutputServiceCollectionExtensions
{
    /// <summary>
    /// Registers the nine first-party exporters (PDF, XLSX, DOCX, HTML, SVG, CSV, JSON, XML, Markdown) as
    /// <see cref="IReportExporter"/> singletons. The Designer's exporter registry and the Viewer discover what is
    /// registered — there is no fixed list in either.
    /// </summary>
    /// <remarks>
    /// <para>For a host that needs only some formats, skip this package and register the exporters from the
    /// individual <c>Output.*</c> packages — e.g. <c>services.AddSingleton&lt;IReportExporter, SkiaPdfExporter&gt;()</c>;
    /// then ClosedXML (XLSX) and OpenXml (DOCX) are not restored at all.</para>
    /// </remarks>
    public static IServiceCollection AddOmniReportExporters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IReportExporter>(_ => new Pdf.SkiaPdfExporter());
        services.AddSingleton<IReportExporter>(_ => new Excel.ExcelExporter());
        services.AddSingleton<IReportExporter>(_ => new Docx.DocxExporter());
        services.AddSingleton<IReportExporter>(_ => new Html.SvgHtmlExporter());
        services.AddSingleton<IReportExporter>(_ => new Svg.SvgExporter());
        services.AddSingleton<IReportExporter>(_ => new Csv.CsvExporter());
        services.AddSingleton<IReportExporter>(_ => new Json.JsonExporter());
        services.AddSingleton<IReportExporter>(_ => new Xml.XmlExporter());
        services.AddSingleton<IReportExporter>(_ => new Markdown.MarkdownExporter());
        return services;
    }
}
