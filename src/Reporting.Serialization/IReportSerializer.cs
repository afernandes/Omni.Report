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

/// <summary>Convenience helpers over <see cref="IReportSerializer"/>.</summary>
public static class ReportSerializerExtensions
{
    /// <summary>Serializes a <see cref="ReportDefinition"/> to a UTF-8 byte array.</summary>
    public static byte[] SaveToBytes(this IReportSerializer serializer, ReportDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        using var ms = new MemoryStream();
        serializer.Save(definition, ms);
        return ms.ToArray();
    }

    /// <summary>Deserializes a <see cref="ReportDefinition"/> from a UTF-8 byte array.</summary>
    public static ReportDefinition LoadFromBytes(this IReportSerializer serializer, ReadOnlySpan<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        using var ms = new MemoryStream(bytes.ToArray(), writable: false);
        return serializer.Load(ms);
    }

    /// <summary>Saves to a file on disk.</summary>
    public static void SaveToFile(this IReportSerializer serializer, ReportDefinition definition, string path)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var fs = File.Create(path);
        serializer.Save(definition, fs);
    }

    /// <summary>Loads from a file on disk.</summary>
    public static ReportDefinition LoadFromFile(this IReportSerializer serializer, string path)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var fs = File.OpenRead(path);
        return serializer.Load(fs);
    }
}
