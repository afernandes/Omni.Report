using Reporting.Output.Pdf;

namespace Reporting.Designer.Blazor.Services;

/// <summary>
/// Read-only registry of every export format the Designer should expose. The host
/// builds it via DI — register one descriptor per format you want, in the order you
/// want them to appear. The PreviewMode toolbar iterates the registry and emits one
/// button per descriptor; the File menu does the same for "Exportar como…".
///
/// <para>Pattern: <see cref="IExporterRegistry"/> wraps a list of <see cref="ExporterDescriptor"/>.
/// Each descriptor decorates an <see cref="IReportExporter"/> with the bits the UI
/// needs (label, icon, sort order). Plain <see cref="IReportExporter"/> instances
/// registered with DI are auto-wrapped with sensible defaults — the host can just
/// do <c>services.AddSingleton&lt;IReportExporter, SkiaPdfExporter&gt;()</c> and the
/// Designer figures out the button. Custom labels / icons can be supplied by
/// registering an explicit <see cref="ExporterDescriptor"/> instead.</para>
/// </summary>
public interface IExporterRegistry
{
    /// <summary>All exporters, already sorted by <see cref="ExporterDescriptor.Order"/>.
    /// Empty when nobody registered anything — the UI hides the export buttons in that
    /// case (the toolbar collapses to "Imprimir" only).</summary>
    IReadOnlyList<ExporterDescriptor> Descriptors { get; }

    /// <summary>Find a descriptor by its format slug (case-insensitive). Returns null
    /// when no exporter matches — used by the command palette to route
    /// <c>file.export.pdf</c> etc. to the right action.</summary>
    ExporterDescriptor? Find(string format);
}

/// <summary>
/// Default implementation. Reads two sources from DI:
/// <list type="bullet">
/// <item>Every <see cref="ExporterDescriptor"/> the host registered explicitly. These
/// carry the host's preferred labels / icons / ordering.</item>
/// <item>Every <see cref="IReportExporter"/> the host registered without a wrapping
/// descriptor. Each one is auto-wrapped with defaults based on its <c>Format</c> string
/// so a host that just registers exporters gets sensible buttons out of the box.</item>
/// </list>
/// Duplicates (same <c>Format</c>) are de-duped — explicit descriptors win.
/// </summary>
public sealed class ExporterRegistry : IExporterRegistry
{
    private readonly List<ExporterDescriptor> _descriptors;
    private readonly Dictionary<string, ExporterDescriptor> _byFormat;

    public ExporterRegistry(
        IEnumerable<ExporterDescriptor> explicitDescriptors,
        IEnumerable<IReportExporter> rawExporters)
    {
        // Explicit descriptors come first — they're what the host customised.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _descriptors = new List<ExporterDescriptor>();
        foreach (var d in explicitDescriptors)
        {
            if (seen.Add(d.Format)) _descriptors.Add(d);
        }
        // Auto-wrap raw IReportExporter registrations the host didn't customise.
        foreach (var exp in rawExporters)
        {
            if (seen.Add(exp.Format)) _descriptors.Add(WrapWithDefaults(exp));
        }
        _descriptors.Sort((a, b) => a.Order.CompareTo(b.Order));
        _byFormat = _descriptors.ToDictionary(d => d.Format, d => d, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ExporterDescriptor> Descriptors => _descriptors;

    /// <inheritdoc/>
    public ExporterDescriptor? Find(string format)
        => string.IsNullOrEmpty(format) ? null
           : _byFormat.TryGetValue(format, out var d) ? d : null;

    /// <summary>Generates a sensible label/icon/order for an exporter the host didn't
    /// describe explicitly. The mapping is conservative — it knows the seven first-party
    /// formats. Unknown formats get a generic <c>file</c> icon and the format name
    /// uppercased as label.</summary>
    private static ExporterDescriptor WrapWithDefaults(IReportExporter exporter)
    {
        return exporter.Format.ToLowerInvariant() switch
        {
            "pdf"      => new(exporter, "PDF",      "Portable Document Format — preserva vetores e fontes", "file-text",        Order: 10,  IsPrimary: true),
            "xlsx"     => new(exporter, "Excel",    "Planilha do Excel (XLSX) com fórmulas e formatação",   "file-spreadsheet", Order: 20),
            "html"     => new(exporter, "HTML",     "Página HTML estática com SVG vetorial",                "file-code",        Order: 30),
            "svg"      => new(exporter, "SVG",      "Gráfico vetorial escalável — uma página por arquivo",  "file-code",        Order: 40),
            "csv"      => new(exporter, "CSV",      "Valores separados por vírgula (Comma-Separated)",       "file",             Order: 50),
            "json"     => new(exporter, "JSON",     "Estrutura JSON — pronta para consumo por APIs",         "file",             Order: 60),
            "markdown" => new(exporter, "Markdown", "Markdown para documentação / GitHub / wiki",            "file",             Order: 70),
            _          => new(exporter, exporter.Format.ToUpperInvariant(),
                             $"Exportar como {exporter.Format.ToUpperInvariant()}", "file", Order: 1000),
        };
    }
}
