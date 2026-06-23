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
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Lossless RDL round-trip of model fields with no native <c>&lt;Subreport&gt;</c>/<c>&lt;Tablix&gt;</c> slot,
/// carried in RDL's own <c>&lt;CustomProperties&gt;</c> (<c>omni:</c>-prefixed — part of the XSD, ignored by
/// Report Builder, so the file still opens in SSRS). Inverse pair: <c>RdlWriter.WriteCustomProperties</c> ↔
/// <c>RdlImporter.ApplyCustomProperties</c>.
/// </summary>
public class RdlCustomPropertiesRoundTripTests
{
    private static Rectangle R(double x, double y, double w, double h)
        => new(Unit.FromMm(x), Unit.FromMm(y), Unit.FromMm(w), Unit.FromMm(h));

    private static ReportDefinition WithHeader(params ReportElement[] items)
        => new("Outer", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(40),
                new EquatableArray<ReportElement>(items)),
        };

    [Fact]
    public void Subreport_data_expression_round_trips_via_custom_properties()
    {
        var def = WithHeader(new SubreportElement
        {
            ReportId = "Detalhe",
            DataExpression = "Fields.Detalhe",
            Bounds = R(0, 0, 80, 20),
        });

        var rdl = new RdlExporter();
        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(def));
        // The omni: value is the raw OmniReport expression (our private namespace — no lossy VB conversion).
        xml.Should().Contain("<Name>omni:DataExpression</Name>").And.Contain("<Value>Fields.Detalhe</Value>");

        var sub = rdl.LoadFromBytes(rdl.SaveToBytes(def)).ReportHeader!.Elements.OfType<SubreportElement>().Single();
        sub.ReportId.Should().Be("Detalhe");
        sub.DataExpression.Should().Be("Fields.Detalhe");
    }

    [Fact]
    public void Subreport_inline_definition_round_trips_via_custom_properties()
    {
        var inline = new ReportDefinition("Inner", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>(new ReportElement[]
            {
                new TextBoxElement { Expression = "Fields.X", Bounds = R(0, 0, 40, 6) },
            })));
        var def = WithHeader(new SubreportElement { InlineDefinition = inline, Bounds = R(0, 0, 80, 20) });

        var rdl = new RdlExporter();
        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(def));
        xml.Should().Contain("<Name>omni:InlineDefinition</Name>")
           .And.Contain("<ReportName>Inner</ReportName>"); // placeholder so <Subreport> stays XSD-valid

        var sub = rdl.LoadFromBytes(rdl.SaveToBytes(def)).ReportHeader!.Elements.OfType<SubreportElement>().Single();
        sub.ReportId.Should().BeNull();                   // synthetic placeholder cleared on import
        sub.InlineDefinition.Should().NotBeNull();
        sub.InlineDefinition.Should().BeEquivalentTo(inline,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes());
    }

    [Fact]
    public void Tablix_subtotal_labels_round_trip_via_custom_properties()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixRdl));
        var tablix = imported.ReportHeader!.Elements.OfType<TablixElement>().Single();
        var def = imported with
        {
            ReportHeader = imported.ReportHeader! with
            {
                Elements = new EquatableArray<ReportElement>(new ReportElement[]
                {
                    tablix with { SubtotalLabel = "Subtotal", GrandTotalLabel = "Total Geral" },
                }),
            },
        };

        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(def));
        xml.Should().Contain("<Name>omni:SubtotalLabel</Name>").And.Contain("<Value>Subtotal</Value>")
           .And.Contain("<Name>omni:GrandTotalLabel</Name>").And.Contain("<Value>Total Geral</Value>");

        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def)).ReportHeader!.Elements.OfType<TablixElement>().Single();
        back.SubtotalLabel.Should().Be("Subtotal");
        back.GrandTotalLabel.Should().Be("Total Geral");
    }

    [Fact]
    public void Custom_properties_use_the_rdl_sanctioned_shape()
    {
        var def = WithHeader(new SubreportElement { ReportId = "X", DataExpression = "Fields.D", Bounds = R(0, 0, 80, 20) });
        var xml = Encoding.UTF8.GetString(new RdlExporter().SaveToBytes(def));
        xml.Should().Contain("<CustomProperties>")
           .And.Contain("<CustomProperty>")
           .And.Contain("<Name>omni:DataExpression</Name>");
    }

    private const string MatrixRdl = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body><Height>50mm</Height><ReportItems>
            <Tablix Name="Matriz">
              <TablixCorner><TablixCornerRows><TablixCornerRow><TablixCornerCell>
                <CellContents><Textbox Name="c"><Paragraphs><Paragraph><TextRuns>
                  <TextRun><Value>Região</Value></TextRun>
                </TextRuns></Paragraph></Paragraphs></Textbox></CellContents>
              </TablixCornerCell></TablixCornerRow></TablixCornerRows></TablixCorner>
              <TablixBody>
                <TablixColumns><TablixColumn><Width>40mm</Width></TablixColumn></TablixColumns>
                <TablixRows><TablixRow><Height>8mm</Height>
                  <TablixCells><TablixCell>
                    <CellContents><Textbox Name="v"><Paragraphs><Paragraph><TextRuns>
                      <TextRun><Value>=Sum(Fields!Vendas.Value)</Value></TextRun>
                    </TextRuns></Paragraph></Paragraphs></Textbox></CellContents>
                  </TablixCell></TablixCells>
                </TablixRow></TablixRows>
              </TablixBody>
              <TablixColumnHierarchy><TablixMembers>
                <TablixMember><Group Name="ColGrp0"><GroupExpressions>
                  <GroupExpression>=Fields!Mes.Value</GroupExpression>
                </GroupExpressions></Group></TablixMember>
              </TablixMembers></TablixColumnHierarchy>
              <TablixRowHierarchy><TablixMembers>
                <TablixMember><Group Name="RowGrp0"><GroupExpressions>
                  <GroupExpression>=Fields!Regiao.Value</GroupExpression>
                </GroupExpressions></Group></TablixMember>
              </TablixMembers></TablixRowHierarchy>
              <DataSetName>Vendas</DataSetName>
              <Top>5mm</Top><Left>5mm</Left><Width>40mm</Width><Height>16mm</Height>
            </Tablix>
          </ReportItems></Body>
          <Width>190mm</Width>
          <Page><PageHeight>297mm</PageHeight><PageWidth>210mm</PageWidth>
            <LeftMargin>10mm</LeftMargin><RightMargin>10mm</RightMargin><TopMargin>10mm</TopMargin><BottomMargin>10mm</BottomMargin></Page>
        </Report>
        """;
}
