using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reporting.Serialization.Internal;

namespace Reporting.Serialization;

/// <summary>
/// Serializer for the <c>.repjson</c> JSON format. Produces a compact JSON object whose
/// shape mirrors the <c>.repx</c> XML schema. Element types are discriminated by the
/// <c>"kind"</c> field. Round-trip equality is structural.
/// </summary>
public sealed class RepJsonSerializer : IReportSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
    };

    public string Format => "repjson";
    public string FileExtension => ".repjson";

    public void Save(ReportDefinition definition, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stream);
        var node = RepJsonWriter.Write(definition, SchemaVersion.Current);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        node.WriteTo(writer);
    }

    public ReportDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var node = JsonNode.Parse(stream)
            ?? throw new FormatException("Empty JSON document.");
        if (node is not JsonObject root)
        {
            throw new FormatException("Root JSON value must be an object.");
        }
        return RepJsonReader.Read(root, out _);
    }

    /// <summary>Loads and returns both the definition and the parsed schema version.</summary>
    public (ReportDefinition Definition, SchemaVersion Version) LoadWithVersion(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var node = JsonNode.Parse(stream)
            ?? throw new FormatException("Empty JSON document.");
        if (node is not JsonObject root)
        {
            throw new FormatException("Root JSON value must be an object.");
        }
        var definition = RepJsonReader.Read(root, out var version);
        return (definition, version);
    }
}
