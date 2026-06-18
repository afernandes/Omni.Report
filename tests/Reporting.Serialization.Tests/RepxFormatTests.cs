using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

public class RepxFormatTests
{
    [Fact]
    public void Produces_valid_xml_with_utf8_declaration()
    {
        var s = new RepxSerializer();
        var bytes = s.SaveToBytes(Fixtures.KitchenSink());
        var text = Encoding.UTF8.GetString(bytes);
        text.Should().StartWith("<?xml");
        text.Should().Contain("encoding=\"utf-8\"");
        text.Should().Contain("<Report SchemaVersion=\"1.0\"");
    }

    [Fact]
    public void Format_metadata_set_correctly()
    {
        var s = new RepxSerializer();
        s.Format.Should().Be("repx");
        s.FileExtension.Should().Be(".repx");
    }

    [Fact]
    public void Rejects_empty_document()
    {
        var s = new RepxSerializer();
        using var ms = new MemoryStream();
        Action act = () => s.Load(ms);
        // XDocument.Load throws XmlException on empty stream
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Rejects_wrong_root_element()
    {
        var s = new RepxSerializer();
        var xml = "<NotReport SchemaVersion=\"1.0\" Name=\"x\"/>";
        Action act = () => s.LoadFromBytes(Encoding.UTF8.GetBytes(xml));
        act.Should().Throw<FormatException>().WithMessage("*Report*");
    }

    [Fact]
    public void Load_with_version_returns_schema_version()
    {
        var s = new RepxSerializer();
        var bytes = s.SaveToBytes(Fixtures.KitchenSink());
        using var ms = new MemoryStream(bytes);
        var (def, version) = s.LoadWithVersion(ms);
        def.Should().NotBeNull();
        version.Should().Be(SchemaVersion.Current);
    }

    [Fact]
    public void Migration_runs_when_version_older()
    {
        // Construct a fake v0.9 document and supply a migration to v1.0.
        var fakeXml = "<Report SchemaVersion=\"0.9\" Name=\"OldFormat\"><PageSetup Orientation=\"Portrait\" Margins=\"500,500,500,500\" Columns=\"1\" ColumnSpacing=\"0\"><Paper Name=\"A4\" Width=\"8268\" Height=\"11693\"/></PageSetup><Detail Height=\"236\" Visible=\"true\" CanGrow=\"false\" CanShrink=\"false\"><Elements/></Detail></Report>";

        var s = new RepxSerializer(new[] { new RenameRootMigration() });
        var (def, version) = s.LoadWithVersion(new MemoryStream(Encoding.UTF8.GetBytes(fakeXml)));
        def.Name.Should().Be("OldFormat-migrated");
        version.Should().Be(new SchemaVersion(1, 0));
    }

    [Fact]
    public void Unit_format_accepts_mm_inch_pt_suffixes()
    {
        // Use a fixture with a "human-friendly" suffix XML and verify it round-trips back via mils.
        var xml = @"<Report SchemaVersion=""1.0"" Name=""x"">
  <PageSetup Orientation=""Portrait"" Margins=""500,500,500,500"" Columns=""1"" ColumnSpacing=""0"">
    <Paper Name=""Custom"" Width=""100mm"" Height=""150mm""/>
  </PageSetup>
  <Detail Height=""6mm"" Visible=""true"" CanGrow=""false"" CanShrink=""false"">
    <Elements/>
  </Detail>
</Report>";
        var s = new RepxSerializer();
        var def = s.LoadFromBytes(Encoding.UTF8.GetBytes(xml));
        def.PageSetup.Paper.Width.ToMm().Should().BeApproximately(100, 0.1);
        def.PageSetup.Paper.Height.ToMm().Should().BeApproximately(150, 0.1);
        def.Detail.Height.ToMm().Should().BeApproximately(6, 0.1);
    }

    private sealed class RenameRootMigration : IRepxMigration
    {
        public SchemaVersion From => new(0, 9);
        public SchemaVersion To => new(1, 0);
        public void Apply(XDocument document)
        {
            var root = document.Root!;
            var name = root.Attribute("Name")?.Value ?? "";
            root.SetAttributeValue("Name", name + "-migrated");
        }
    }
}
