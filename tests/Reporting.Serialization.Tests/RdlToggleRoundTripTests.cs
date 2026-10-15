using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Drill-down survives an RDL round-trip: <see cref="ReportElement.ToggleItemId"/> becomes
/// <c>&lt;Visibility&gt;&lt;ToggleItem&gt;</c>, and under a toggle RDL's <c>&lt;Hidden&gt;</c> carries only the initial
/// state (<see cref="ReportElement.InitiallyHidden"/>). The import side was fixed in #217; these pin the export side.
/// </summary>
public class RdlToggleRoundTripTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SaveToBytes_ElementoComToggleItem_PreservaVinculoEEstadoInicial(bool initiallyHidden)
    {
        var rdl = new RdlExporter();

        var element = RoundTrip(rdl, new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            ToggleItemId = "Cabecalho",
            InitiallyHidden = initiallyHidden,
        }, out var xml);

        xml.Should().Contain("<ToggleItem>Cabecalho</ToggleItem>");
        element.ToggleItemId.Should().Be("Cabecalho");
        element.InitiallyHidden.Should().Be(initiallyHidden);
        element.Visible.Should().BeTrue("starting collapsed is not the same as hidden for good");
    }

    [Fact]
    public void SaveToBytes_ElementoComToggleEOcultoPermanente_PreservaVisibleViaCustomProperty()
    {
        var rdl = new RdlExporter();

        var element = RoundTrip(rdl, new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            ToggleItemId = "Cabecalho",
            Visible = false,
        }, out var xml);

        xml.Should().Contain("<Name>omni:Visible</Name>");
        element.Visible.Should().BeFalse();
        element.InitiallyHidden.Should().BeFalse("Hidden under a toggle carries only the initial state");
        element.ToggleItemId.Should().Be("Cabecalho");
    }

    [Fact]
    public void SaveToBytes_ElementoOcultoSemToggle_ContinuaComoHiddenSemCustomProperty()
    {
        var rdl = new RdlExporter();

        var element = RoundTrip(rdl, new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            Visible = false,
        }, out var xml);

        xml.Should().NotContain("omni:Visible").And.NotContain("<ToggleItem>");
        element.Visible.Should().BeFalse();
    }

    [Fact]
    public void LoadFromBytes_ToggleComVisibilidadeCondicional_PreservaACondicaoAoSalvarDeNovo()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(ConditionalToggleRdl));
        var detail = imported.ReportHeader!.Elements.OfType<TextBoxElement>().Single(e => e.ToggleItemId is not null);
        detail.VisibleExpression.Should().NotBeNullOrEmpty("the importer reads the expression-valued Hidden");

        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(imported));
        var reloaded = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(xml))
            .ReportHeader!.Elements.OfType<TextBoxElement>().Single(e => e.ToggleItemId is not null);

        xml.Should().Contain("Fields!Oculto.Value");
        reloaded.VisibleExpression.Should().NotBeNullOrEmpty("the condition must survive a second save");
        reloaded.ToggleItemId.Should().Be("Cab");
        reloaded.InitiallyHidden.Should().BeFalse();
    }

    [Fact]
    public void SaveToBytes_ToggleComExpressaoEEstadoInicialRecolhido_PreservaOsDois()
    {
        var rdl = new RdlExporter();

        var element = RoundTrip(rdl, new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            ToggleItemId = "Cabecalho",
            InitiallyHidden = true,
            VisibleExpression = "Fields.Ativo",
        }, out var xml);

        xml.Should().Contain("<Name>omni:InitiallyHidden</Name>");
        element.InitiallyHidden.Should().BeTrue();
        element.VisibleExpression.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SaveToBytes_ToggleReferenciandoBookmark_ApontaParaONomeExportadoDaTextBox()
    {
        var master = new TextBoxElement
        {
            Expression = "Fields.Grupo",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(60), Unit.FromMm(6)),
            Bookmark = "master",
        };
        var detail = new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            ToggleItemId = "master",
        };
        var definition = new ReportDefinition("Drill", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(20),
                new EquatableArray<ReportElement>(new ReportElement[] { master, detail })),
        };

        var document = XDocument.Parse(Encoding.UTF8.GetString(new RdlExporter().SaveToBytes(definition)));

        var toggle = document.Descendants().Single(e => e.Name.LocalName == "ToggleItem").Value;
        toggle.Should().NotBe("master", "RDL resolves ToggleItem by Textbox name, not by bookmark");
        document.Descendants().Where(e => e.Name.LocalName == "Textbox")
            .Select(e => (string?)e.Attribute("Name")).Should().Contain(toggle);
    }

    [Fact]
    public void SaveToBytes_ToggleReferenciandoNomeImportado_MantemONome()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(ConditionalToggleRdl));

        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(imported));

        xml.Should().Contain("<ToggleItem>Cab</ToggleItem>");
    }

    private const string ConditionalToggleRdl = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <ReportSections><ReportSection>
            <Body><Height>2in</Height><ReportItems>
              <Textbox Name="Cab"><Paragraphs><Paragraph><TextRuns><TextRun><Value>Grupo</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Top>0in</Top><Left>0in</Left><Height>0.25in</Height><Width>2in</Width></Textbox>
              <Textbox Name="Det"><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Detalhe.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Top>0.3in</Top><Left>0in</Left><Height>0.25in</Height><Width>2in</Width>
                <Visibility><Hidden>=Fields!Oculto.Value</Hidden><ToggleItem>Cab</ToggleItem></Visibility></Textbox>
            </ReportItems></Body>
            <Width>6in</Width><Page><PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth></Page>
          </ReportSection></ReportSections>
        </Report>
        """;

    private static TextBoxElement RoundTrip(RdlExporter rdl, TextBoxElement element, out string xml)
    {
        var definition = new ReportDefinition("Drill", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(20),
                new EquatableArray<ReportElement>(new ReportElement[] { element })),
        };
        var bytes = rdl.SaveToBytes(definition);
        xml = Encoding.UTF8.GetString(bytes);
        return rdl.LoadFromBytes(bytes).ReportHeader!.Elements.OfType<TextBoxElement>().Single();
    }
}
