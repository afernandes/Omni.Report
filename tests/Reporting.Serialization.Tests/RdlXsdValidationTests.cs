using System.Text;
using System.Xml;
using System.Xml.Schema;
using FluentAssertions;
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
/// Validates that <c>.rdl</c> produced by <see cref="RdlExporter"/> conforms to the official Microsoft RDL
/// 2016/01 XML Schema (vendored at <c>schemas/rdl/2016-01/ReportDefinition.xsd</c>, extracted from [MS-RDL]).
/// This is the real "opens in SSRS / Report Builder" guarantee — XSD validity, not just round-trip-by-value.
/// </summary>
public class RdlXsdValidationTests
{
    private static readonly XmlSchemaSet Schema = LoadSchema();

    private static XmlSchemaSet LoadSchema()
    {
        var asm = typeof(RdlXsdValidationTests).Assembly;
        var resource = asm.GetManifestResourceNames().Single(n => n.EndsWith("ReportDefinition.xsd"));
        using var stream = asm.GetManifestResourceStream(resource)!;
        var set = new XmlSchemaSet();
        set.Add(null, XmlReader.Create(stream)); // targetNamespace declared inside the schema
        set.Compile();
        return set;
    }

    private static IReadOnlyList<string> ValidationErrors(byte[] rdl)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
        settings.Schemas.Add(Schema);
        settings.ValidationEventHandler += (_, e) =>
            errors.Add($"{e.Severity} (line {e.Exception?.LineNumber}): {e.Message}");
        using var ms = new MemoryStream(rdl);
        using var reader = XmlReader.Create(ms, settings);
        while (reader.Read()) { }
        return errors;
    }

    private static Rectangle R(double x, double y, double w, double h)
        => new(Unit.FromMm(x), Unit.FromMm(y), Unit.FromMm(w), Unit.FromMm(h));

    [Fact]
    public void Vendored_schema_compiles()
    {
        Schema.IsCompiled.Should().BeTrue();
        Schema.GlobalElements.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Minimal_report_is_xsd_valid()
    {
        var def = new ReportDefinition("Min", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(8), new EquatableArray<ReportElement>(new ReportElement[]
            {
                new TextBoxElement { Expression = "Fields.X", Bounds = R(0, 0, 80, 8) },
            })));
        ValidationErrors(new RdlExporter().SaveToBytes(def)).Should().BeEmpty();
    }

    [Fact]
    public void Header_with_common_items_is_xsd_valid()
    {
        var def = new ReportDefinition("H", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(40),
                new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new LabelElement { Text = "Título", Bounds = R(0, 0, 80, 8) },
                    new TextBoxElement { Expression = "Fields.X", Bounds = R(0, 10, 80, 8), Bookmark = "bm1" },
                    new LineElement { Bounds = R(0, 20, 80, 0) },
                    new RectangleElement { Bounds = R(0, 22, 80, 10) },
                })),
        };
        ValidationErrors(new RdlExporter().SaveToBytes(def)).Should().BeEmpty();
    }

    [Fact]
    public void Subreport_with_custom_properties_is_xsd_valid()
    {
        var inline = new ReportDefinition("Inner", PageSetup.A4Portrait,
            new DetailBand(Unit.FromMm(6), new EquatableArray<ReportElement>(new ReportElement[]
            {
                new TextBoxElement { Expression = "Fields.X", Bounds = R(0, 0, 40, 6) },
            })));
        var def = new ReportDefinition("S", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(40),
                new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new SubreportElement { ReportId = "Ext", DataExpression = "Fields.Detalhe", Bounds = R(0, 0, 80, 20) },
                    new SubreportElement { InlineDefinition = inline, Bounds = R(0, 22, 80, 16) },
                })),
        };
        ValidationErrors(new RdlExporter().SaveToBytes(def)).Should().BeEmpty();
    }

    [Fact]
    public void Matrix_tablix_with_custom_properties_is_xsd_valid()
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
        ValidationErrors(rdl.SaveToBytes(def)).Should().BeEmpty();
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
