using System.Text;
using System.Xml;
using Reporting.Serialization.Internal;

namespace Reporting.Serialization;

/// <summary>
/// Serialises a <see cref="ReportDefinition"/> to an SSRS <c>.rdl</c> file (the
/// <c>…/2016/01/reportdefinition</c> namespace SSRS and Report Builder read) — the inverse of
/// <see cref="RdlImporter"/>. Together they enable the native <c>.rdl → import → edit → export → .rdl</c>
/// cycle (previously OmniReport could read <c>.rdl</c> but only save its own <c>.repx</c>/<c>.repjson</c>).
/// <para>As an <see cref="IReportSerializer"/>, <see cref="Load"/> delegates to <see cref="RdlImporter"/>, so
/// <c>Load(Save(def))</c> is the round-trip contract. Areas that don't map cleanly to RDL are recorded in
/// <c>Metadata["ExportWarnings"]</c> (mirroring the importer's <c>ImportWarnings</c> policy — never a silent
/// drop). This is layered in phases: PR1 = page skeleton; report items, datasets, parameters and Tablix
/// follow.</para>
/// </summary>
public sealed class RdlExporter : IReportSerializer
{
    public string Format => "rdl";
    public string FileExtension => ".rdl";

    /// <summary>Aspects of the last <see cref="Save"/> that don't yet map to RDL (data regions, embedded
    /// images, unsupported items) — mirrors the importer's <c>ImportWarnings</c> so loss is never silent.
    /// Reset on each <see cref="Save"/> call.</summary>
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    public void Save(ReportDefinition definition, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stream);
        var warnings = new List<string>();
        var document = RdlWriter.Write(definition, warnings);
        Warnings = warnings;
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var writer = XmlWriter.Create(stream, settings);
        document.Save(writer);
    }

    /// <summary>Reads an <c>.rdl</c> back into a <see cref="ReportDefinition"/> via <see cref="RdlImporter"/>
    /// — so a single <see cref="RdlExporter"/> instance is a full RDL serializer.</summary>
    public ReportDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new RdlImporter().Import(stream);
    }
}
