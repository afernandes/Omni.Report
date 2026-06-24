using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Xml;
using Xunit;

namespace Reporting.Output.Tests;

public sealed record XLinha(string Nome, string Valor);

/// <summary>
/// Coverage for the structured XML exporter: the shared <see cref="Reporting.Output.Pdf.IReportExporter"/> contract,
/// well-formed output with a stable schema (report → pages → primitives), XML escaping of special chars, and the
/// TextsOnly option.
/// </summary>
public class XmlExporterTests
{
    private static async Task<string> ExportXml(XmlExportOptions? opts = null)
    {
        var report = await ReportBuilder.Create("Relatório XML")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", new[] { new XLinha("Açaí & Cia", "1200") })
            .Detail(d => d.Height(8)
                .Text("{Fields.Nome}").At(0, 0).Size(60, 6)
                .Line().At(0, 7).Size(60, 0))
            .Build().PaginateAsync();

        using var ms = new MemoryStream();
        new XmlExporter(opts).Export(report, ms);
        return new UTF8Encoding(false).GetString(ms.ToArray());
    }

    [Fact]
    public void Exporter_contract_is_satisfied()
    {
        var e = new XmlExporter();
        e.Format.Should().Be("xml");
        e.FileExtension.Should().Be(".xml");
        e.ContentType.Should().Contain("xml");
    }

    [Fact]
    public async Task Xml_is_well_formed_with_a_stable_schema_and_escapes_special_chars()
    {
        var xml = await ExportXml();

        var doc = XDocument.Parse(xml); // throws if not well-formed
        doc.Root!.Name.LocalName.Should().Be("report");
        doc.Root.Attribute("name")!.Value.Should().Be("Relatório XML");
        doc.Root.Attribute("pageCount").Should().NotBeNull();

        doc.Root.Element("pages")!.Elements("page").Should().NotBeEmpty();
        // "&" is XML-escaped on write and unescaped by the parser; unicode (ç/í) survives intact.
        doc.Descendants("text").Select(t => t.Value).Should().Contain(v => v.Contains("Açaí & Cia"));
    }

    [Fact]
    public async Task Texts_only_option_skips_non_text_primitives()
    {
        var full = XDocument.Parse(await ExportXml());
        full.Descendants("line").Should().NotBeEmpty("the full export keeps the line primitive");

        var textsOnly = XDocument.Parse(await ExportXml(new XmlExportOptions { TextsOnly = true }));
        textsOnly.Descendants("primitives").Elements()
            .Should().OnlyContain(e => e.Name.LocalName == "text", "TextsOnly drops lines/rects/etc.");
    }

    [Fact]
    public async Task Mils_unit_emits_integer_coordinates()
    {
        var xml = await ExportXml(new XmlExportOptions { Unit = XmlUnit.Mils });
        var doc = XDocument.Parse(xml);
        doc.Root!.Attribute("unit")!.Value.Should().Be("mils");
        var x = doc.Descendants("text").First().Attribute("x")!.Value;
        x.Should().NotContain(".", "mils are raw integers");
    }
}
