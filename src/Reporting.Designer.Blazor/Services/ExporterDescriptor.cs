using Reporting.Output.Pdf;

namespace Reporting.Designer.Blazor.Services;

/// <summary>
/// UI-friendly wrapper around an <see cref="IReportExporter"/>. Carries the bits the
/// Designer + PreviewMode need to render a button (label, icon, shortcut) WITHOUT
/// hard-coding the list of formats — the toolbar enumerates whatever the host
/// registered via DI and renders one button per descriptor.
/// </summary>
/// <param name="Exporter">The underlying exporter — does the actual byte conversion.</param>
/// <param name="Label">Short human label shown on the button (e.g. <c>"PDF"</c>,
/// <c>"Excel"</c>, <c>"HTML"</c>). Localizable by the host.</param>
/// <param name="Description">Tooltip / longer description (e.g. "Exportar como Portable
/// Document Format — preserva fontes e vetores").</param>
/// <param name="IconName">Lucide icon name registered in <c>IconCatalog</c> — drawn
/// next to the label. Reserved names: <c>file-text</c>, <c>file-spreadsheet</c>,
/// <c>file-code</c>, <c>file</c>. Unknown icons fall back to <c>file</c>.</param>
/// <param name="Order">Sort key — lower numbers appear first. Defaults give PDF the
/// primary slot, XLSX/HTML next, then text formats. Host can override.</param>
/// <param name="IsPrimary">Hint to the UI: highlight this exporter as the "default"
/// (filled button vs ghost). Typically only one — PDF.</param>
public sealed record ExporterDescriptor(
    IReportExporter Exporter,
    string Label,
    string Description,
    string IconName,
    int Order = 100,
    bool IsPrimary = false)
{
    /// <summary>Mime type for HTTP responses / data URIs.</summary>
    public string ContentType => Exporter.ContentType;

    /// <summary>File extension with leading dot — used to build the download filename.</summary>
    public string FileExtension => Exporter.FileExtension;

    /// <summary>Short format slug (lowercase, e.g. <c>"pdf"</c>) — handy for command
    /// palette / keyboard shortcut routing.</summary>
    public string Format => Exporter.Format;
}
