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
