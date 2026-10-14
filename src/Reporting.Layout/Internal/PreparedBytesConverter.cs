using System.Text.Json;
using System.Text.Json.Serialization;
using Reporting.Common;

namespace Reporting.Layout.Internal;

internal sealed class PreparedBytesConverter : JsonConverter<EquatableArray<byte>>
{
    public override EquatableArray<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetBytesFromBase64());

    public override void Write(Utf8JsonWriter writer, EquatableArray<byte> value, JsonSerializerOptions options)
        => writer.WriteBase64StringValue(value.ToArray());
}
