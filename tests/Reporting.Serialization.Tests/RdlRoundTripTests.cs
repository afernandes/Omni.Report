using System.Text;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Serialization;
using Reporting.Styling;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// PR2 of the RDL exporter — simple report items (Textbox/Label, Line, Rectangle + nested children, Image)
/// with their <c>&lt;Style&gt;</c>, mapped through the static bands. The core guarantee is the round-trip:
/// importing a static <c>.rdl</c>, exporting it and re-importing yields a value-equal
/// <see cref="ReportDefinition"/> (records are equatable), and a report authored code-first survives the same.
/// </summary>
public class RdlRoundTripTests
{
    // A static report exercising every PR2 item kind: a literal title (→ Label), a bound textbox (→ TextBox)
    // with style + format, a Line with a non-default pen, a Rectangle with a nested child (relative bounds),
    // an external Image, plus PageHeader and PageFooter sections.
    private const string StaticRdl = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body>
            <Height>250mm</Height>
            <ReportItems>
              <Textbox Name="Titulo">
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>Relatório de Vendas</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Style><FontFamily>Arial</FontFamily><FontSize>14pt</FontSize><FontWeight>Bold</FontWeight><Color>#800000</Color></Style>
                <Top>5mm</Top><Left>5mm</Left><Height>10mm</Height><Width>120mm</Width>
              </Textbox>
              <Textbox Name="Total">
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Total.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Style><TextAlign>Right</TextAlign><Format>C2</Format></Style>
                <Top>20mm</Top><Left>5mm</Left><Height>8mm</Height><Width>40mm</Width>
              </Textbox>
              <Line Name="Separador">
                <Style><Border><Color>#000000</Color><Style>Solid</Style><Width>1pt</Width></Border></Style>
                <Top>32mm</Top><Left>5mm</Left><Height>0mm</Height><Width>120mm</Width>
              </Line>
              <Rectangle Name="Caixa">
                <ReportItems>
                  <Textbox Name="Interno">
                    <Paragraphs><Paragraph><TextRuns><TextRun><Value>Dentro</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                    <Top>2mm</Top><Left>2mm</Left><Height>8mm</Height><Width>40mm</Width>
                  </Textbox>
                </ReportItems>
                <Top>40mm</Top><Left>5mm</Left><Height>30mm</Height><Width>60mm</Width>
              </Rectangle>
              <Image Name="Logo">
                <Source>External</Source><Value>logo.png</Value><Sizing>FitProportional</Sizing>
                <Top>75mm</Top><Left>5mm</Left><Height>20mm</Height><Width>20mm</Width>
              </Image>
            </ReportItems>
          </Body>
          <Width>190mm</Width>
          <Page>
            <PageHeader>
              <Height>15mm</Height>
              <ReportItems>
                <Textbox Name="Cab">
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Cabeçalho</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                  <Top>2mm</Top><Left>2mm</Left><Height>8mm</Height><Width>80mm</Width>
                </Textbox>
              </ReportItems>
            </PageHeader>
            <PageHeight>297mm</PageHeight>
            <PageWidth>210mm</PageWidth>
            <LeftMargin>10mm</LeftMargin><RightMargin>10mm</RightMargin>
            <TopMargin>10mm</TopMargin><BottomMargin>10mm</BottomMargin>
            <PageFooter>
              <Height>10mm</Height>
              <ReportItems>
                <Textbox Name="Rodape">
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Globals!PageNumber</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                  <Top>2mm</Top><Left>2mm</Left><Height>6mm</Height><Width>40mm</Width>
                </Textbox>
              </ReportItems>
            </PageFooter>
          </Page>
        </Report>
        """;

    [Fact]
    public void Importing_exporting_and_reimporting_a_static_rdl_is_value_equal()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(StaticRdl));

        // Sanity: the fixture really produced content (so equality below isn't comparing two empty reports).
        imported.ReportHeader.Should().NotBeNull();
        imported.ReportHeader!.Elements.Should().HaveCount(5); // Label, TextBox, Line, Rectangle, Image
        imported.PageHeader.Should().NotBeNull();
        imported.PageFooter.Should().NotBeNull();

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));

        // Element Id is an ephemeral per-instance GUID with no RDL counterpart (RDL identity is Name), so the
        // importer regenerates it — exclude it. Everything else (type, text/expression, bounds, style, bands)
        // must reconstruct identically.
        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes(),
            "export must faithfully reconstruct what the importer reads");
    }

    [Fact]
    public void Item_kinds_survive_the_round_trip_with_their_identity()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(StaticRdl));
        var items = imported.ReportHeader!.Elements;

        // A literal stays a Label; a "=…" expression stays a bound TextBox.
        items.OfType<LabelElement>().Should().ContainSingle(l => l.Text == "Relatório de Vendas");
        items.OfType<TextBoxElement>().Should().ContainSingle(t => t.Expression == "Fields.Total");
        items.OfType<LineElement>().Should().ContainSingle();
        items.OfType<ImageElement>().Should().ContainSingle(i => i.Path == "logo.png");
        var rect = items.OfType<RectangleElement>().Should().ContainSingle().Subject;
        rect.Children.OfType<LabelElement>().Should().ContainSingle(c => c.Text == "Dentro");

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));
        reimported.ReportHeader!.Elements.Should().BeEquivalentTo(items,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes());
    }

    [Fact]
    public void Style_and_line_pen_survive_export()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(StaticRdl));
        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));

        var title = reimported.ReportHeader!.Elements.OfType<LabelElement>().Single(l => l.Name == "Titulo");
        title.Style.Font!.Style.Should().HaveFlag(FontStyle.Bold);
        title.Style.ForeColor.Should().Be(Color.FromRgb(0x80, 0x00, 0x00));

        var total = reimported.ReportHeader.Elements.OfType<TextBoxElement>().Single();
        total.Style.HorizontalAlignment.Should().Be(HorizontalAlignment.Right);
        total.Style.Format.Should().Be("C2");

        var line = reimported.ReportHeader.Elements.OfType<LineElement>().Single();
        line.Pen.Thickness.ToPoints().Should().BeApproximately(1, 0.05); // 1pt border preserved (not the 0.5pt default)
    }

    // A textbox exercising the rest of the style + common-attribute surface: background, vertical align,
    // padding, a per-side-less border, no-wrap, a hidden flag and a bookmark.
    private const string RichRdl = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body>
            <Height>40mm</Height>
            <ReportItems>
              <Textbox Name="Rico">
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Nome.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Style>
                  <BackgroundColor>#EEEEEE</BackgroundColor>
                  <VerticalAlign>Middle</VerticalAlign>
                  <PaddingLeft>2mm</PaddingLeft><PaddingTop>1mm</PaddingTop><PaddingRight>2mm</PaddingRight><PaddingBottom>1mm</PaddingBottom>
                  <Border><Color>#333333</Color><Style>Dashed</Style><Width>2pt</Width></Border>
                  <WrapMode>NoWrap</WrapMode>
                </Style>
                <Visibility><Hidden>true</Hidden></Visibility>
                <Bookmark>marcador1</Bookmark>
                <Top>5mm</Top><Left>5mm</Left><Height>10mm</Height><Width>60mm</Width>
              </Textbox>
            </ReportItems>
          </Body>
          <Width>190mm</Width>
          <Page>
            <PageHeight>297mm</PageHeight><PageWidth>210mm</PageWidth>
            <LeftMargin>10mm</LeftMargin><RightMargin>10mm</RightMargin><TopMargin>10mm</TopMargin><BottomMargin>10mm</BottomMargin>
          </Page>
        </Report>
        """;

    [Fact]
    public void The_full_style_and_common_attribute_surface_round_trips()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(RichRdl));

        var box = imported.ReportHeader!.Elements.OfType<TextBoxElement>().Single();
        box.Visible.Should().BeFalse();        // <Hidden>true</Hidden>
        box.Bookmark.Should().NotBeNullOrEmpty();
        box.Style.BackColor.Should().NotBeNull();
        box.Style.Padding.Should().NotBeNull();
        box.Style.Border.Should().NotBeNull();

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));
        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes());
    }

    [Fact]
    public void A_code_first_report_survives_export_then_import()
    {
        // Built purely from low-level records — proves the exporter consumes the common ReportDefinition and
        // does NOT depend on the importer's Metadata (independence of the 3 authoring modes).
        var def = new ReportDefinition("CodeFirst", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(40),
                new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new LabelElement
                    {
                        Name = "T",
                        Text = "Olá Mundo",
                        Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(5), Unit.FromMm(80), Unit.FromMm(10)),
                    },
                    new TextBoxElement
                    {
                        Name = "V",
                        Expression = "Concat(Fields.Nome, Parameters.Sufixo)",
                        Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(18), Unit.FromMm(60), Unit.FromMm(8)),
                    },
                })),
        };

        var rdl = new RdlExporter();
        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));

        back.ReportHeader!.Elements.OfType<LabelElement>().Should().ContainSingle(l => l.Text == "Olá Mundo");
        back.ReportHeader.Elements.OfType<TextBoxElement>().Should()
            .ContainSingle(t => t.Expression == "Concat(Fields.Nome, Parameters.Sufixo)");
    }

    [Fact]
    public void A_data_bound_detail_exports_a_flat_tablix_and_round_trips()
    {
        // A data-bound Detail (no Groups, no extra bands) is the inverse of TryFlatTablixBands → a flat
        // <Tablix>. (PR5 turned the old "deferred" warning into a real export.)
        var def = new ReportDefinition("WithData", PageSetup.A4Portrait,
            DetailBand.Empty with
            {
                Height = Unit.FromMm(8),
                DataSetName = "Vendas",
                Elements = new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new TextBoxElement { Expression = "Fields.Total", Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(8)) },
                }),
            });

        var rdl = new RdlExporter();
        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(def));
        xml.Should().Contain("<Tablix").And.Contain("<DataSetName>Vendas</DataSetName>");
        rdl.Warnings.Should().NotContain(w => w.Contains("fase posterior")); // no longer deferred

        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));
        back.Detail.DataSetName.Should().Be("Vendas");
        back.Detail.Elements.OfType<TextBoxElement>().Should().ContainSingle(t => t.Expression == "Fields.Total");
    }
}
