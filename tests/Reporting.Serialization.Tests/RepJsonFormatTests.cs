using System.Text;
using System.Text.Json;
using FluentAssertions;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

public class RepJsonFormatTests
{
    [Fact]
    public void Produces_valid_json_object()
    {
        var s = new RepJsonSerializer();
        var bytes = s.SaveToBytes(Fixtures.KitchenSink());
        var doc = JsonDocument.Parse(bytes);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        doc.RootElement.GetProperty("name").GetString().Should().Be("KitchenSink");
    }

    [Fact]
    public void Format_metadata_set_correctly()
    {
        var s = new RepJsonSerializer();
        s.Format.Should().Be("repjson");
        s.FileExtension.Should().Be(".repjson");
    }

    [Fact]
    public void Rejects_non_object_root()
    {
        var s = new RepJsonSerializer();
        Action act = () => s.LoadFromBytes("[]"u8.ToArray());
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Rejects_empty_payload()
    {
        var s = new RepJsonSerializer();
        Action act = () => s.LoadFromBytes(Array.Empty<byte>());
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Load_with_version_returns_schema_version()
    {
        var s = new RepJsonSerializer();
        var bytes = s.SaveToBytes(Fixtures.KitchenSink());
        using var ms = new MemoryStream(bytes);
        var (def, version) = s.LoadWithVersion(ms);
        def.Should().NotBeNull();
        version.Should().Be(SchemaVersion.Current);
    }

    [Fact]
    public void Unknown_element_kind_throws_descriptive_format_exception()
    {
        var s = new RepJsonSerializer();
        var json = """
        {
          "schemaVersion": "1.0", "name": "x",
          "pageSetup": { "paper": { "name": "A4", "width": "210mm", "height": "297mm" },
                         "orientation": "Portrait", "margins": "0,0,0,0", "columns": 1, "columnSpacing": "0" },
          "detail": { "height": "6mm", "visible": true, "canGrow": false, "canShrink": false,
                      "elements": [ { "kind": "WAT", "id": "x", "bounds": "0,0,10,10", "visible": true, "style": {"horizontalAlignment":"Left","verticalAlignment":"Top","wordWrap":true} } ] }
        }
        """;
        Action act = () => s.LoadFromBytes(Encoding.UTF8.GetBytes(json));
        act.Should().Throw<FormatException>().WithMessage("*WAT*");
    }

    [Fact]
    public void Element_kind_discriminator_is_present()
    {
        var s = new RepJsonSerializer();
        var def = Fixtures.KitchenSink();
        var bytes = s.SaveToBytes(def);
        var text = Encoding.UTF8.GetString(bytes);
        text.Should().Contain("\"kind\": \"TextBox\"");
        text.Should().Contain("\"kind\": \"Line\"");
        text.Should().Contain("\"kind\": \"Rectangle\"");
        text.Should().Contain("\"kind\": \"Chart\"");
    }
}
