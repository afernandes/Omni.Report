using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reporting.Layout.Internal;

internal sealed class PreparedScalarConverter : JsonConverter<object>
{
    private static readonly Type[] Types = [typeof(string), typeof(bool), typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(decimal), typeof(double), typeof(float), typeof(DateTime), typeof(DateTimeOffset),
        typeof(Guid), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(char)];

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        int kind = root.GetProperty("kind").GetInt32();
        if ((uint)kind >= Types.Length) throw new JsonException("Unknown prepared scalar type.");
        return root.GetProperty("value").Deserialize(Types[kind], options);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        int kind = Array.IndexOf(Types, value.GetType());
        if (kind < 0) throw new NotSupportedException($"Prepared documents require a scalar semantic value, received {value.GetType().Name}.");
        writer.WriteStartObject();
        writer.WriteNumber("kind", kind);
        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value, Types[kind], options);
        writer.WriteEndObject();
    }
}
