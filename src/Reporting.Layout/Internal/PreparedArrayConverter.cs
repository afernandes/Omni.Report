using System.Text.Json;
using System.Text.Json.Serialization;
using Reporting.Common;

namespace Reporting.Layout.Internal;

internal sealed class PreparedArrayConverter<T> : JsonConverter<EquatableArray<T>>
{
    public override EquatableArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(JsonSerializer.Deserialize<T[]>(ref reader, options) ?? []);

    public override void Write(Utf8JsonWriter writer, EquatableArray<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value) JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }
}
