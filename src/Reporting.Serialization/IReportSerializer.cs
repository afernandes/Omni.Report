namespace Reporting.Serialization;

/// <summary>
/// A serializer that converts a <see cref="ReportDefinition"/> to and from a binary
/// representation (XML or JSON). Implementations guarantee structural round-trip:
/// <c>Load(Save(def)).Equals(def)</c>.
/// </summary>
public interface IReportSerializer
{
    /// <summary>Identifier of the format (e.g. <c>"repx"</c>, <c>"repjson"</c>).</summary>
    string Format { get; }

    /// <summary>Default file extension including the leading dot.</summary>
    string FileExtension { get; }

    void Save(ReportDefinition definition, Stream stream);

    ReportDefinition Load(Stream stream);
}
