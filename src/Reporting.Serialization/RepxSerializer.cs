using System.Text;
using System.Xml;
using System.Xml.Linq;
using Reporting.Serialization.Internal;

namespace Reporting.Serialization;

/// <summary>
/// Serializer for the <c>.repx</c> XML format. The on-disk layout is human-friendly and
/// diff-able, with element bounds expressed in mils (integer thousandths of an inch) so
/// the round-trip is lossless.
/// </summary>
public sealed class RepxSerializer : IReportSerializer
{
    private readonly IReadOnlyList<IRepxMigration> _migrations;

    public RepxSerializer(IEnumerable<IRepxMigration>? migrations = null)
    {
        _migrations = migrations?.ToList() ?? new List<IRepxMigration>();
    }

    public string Format => "repx";
    public string FileExtension => ".repx";

    public void Save(ReportDefinition definition, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stream);
        var document = RepxWriter.Write(definition, SchemaVersion.Current);
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false), // no BOM
            OmitXmlDeclaration = false,
        };
        using var writer = XmlWriter.Create(stream, settings);
        document.Save(writer);
    }

    public ReportDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var document = XDocument.Load(stream, LoadOptions.None);
        ApplyMigrations(document);
        return RepxReader.Read(document, out _);
    }

    /// <summary>Loads a document and returns both the definition and the parsed schema version.</summary>
    public (ReportDefinition Definition, SchemaVersion Version) LoadWithVersion(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var document = XDocument.Load(stream, LoadOptions.None);
        ApplyMigrations(document);
        var definition = RepxReader.Read(document, out var version);
        return (definition, version);
    }

    private void ApplyMigrations(XDocument document)
    {
        if (_migrations.Count == 0)
        {
            return;
        }
        var root = document.Root!;
        var rawVersion = root.Attribute("SchemaVersion")?.Value ?? "1.0";
        var current = SchemaVersion.Parse(rawVersion);
        // Apply ordered chain — repeatedly look for a migration whose From matches.
        bool progressed;
        do
        {
            progressed = false;
            foreach (var m in _migrations)
            {
                if (m.From == current)
                {
                    m.Apply(document);
                    current = m.To;
                    root.SetAttributeValue("SchemaVersion", current.ToString());
                    progressed = true;
                    break;
                }
            }
        } while (progressed && current < SchemaVersion.Current);
    }
}
