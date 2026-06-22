using System.Text;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Reporting.Elements;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// PR4 of the RDL exporter — the matrix/crosstab <c>&lt;Tablix&gt;</c>. A matrix imports as a
/// <see cref="TablixElement"/> (row/column groups + a corner cell and a body value cell); the exporter is its
/// inverse, so a matrix <c>.rdl</c> round-trips by value. (The flat-table decomposition — a Body that is one
/// flat Tablix becoming PageHeader+Detail bands — is the separate next phase.)
/// </summary>
public class RdlTablixRoundTripTests
{
    private const string MatrixRdl = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body>
            <Height>50mm</Height>
            <ReportItems>
              <Tablix Name="Matriz">
                <TablixCorner>
                  <TablixCornerRows><TablixCornerRow><TablixCornerCell>
                    <CellContents><Textbox Name="c"><Paragraphs><Paragraph><TextRuns>
                      <TextRun><Value>Região</Value></TextRun>
                    </TextRuns></Paragraph></Paragraphs></Textbox></CellContents>
                  </TablixCornerCell></TablixCornerRow></TablixCornerRows>
                </TablixCorner>
                <TablixBody>
                  <TablixColumns><TablixColumn><Width>40mm</Width></TablixColumn></TablixColumns>
                  <TablixRows><TablixRow>
                    <Height>8mm</Height>
                    <TablixCells><TablixCell>
                      <CellContents><Textbox Name="v"><Paragraphs><Paragraph><TextRuns>
                        <TextRun><Value>=Sum(Fields!Vendas.Value)</Value></TextRun>
                      </TextRuns></Paragraph></Paragraphs><Style><Format>C2</Format></Style></Textbox></CellContents>
                    </TablixCell></TablixCells>
                  </TablixRow></TablixRows>
                </TablixBody>
                <TablixColumnHierarchy><TablixMembers>
                  <TablixMember><Group Name="ColGrp0"><GroupExpressions>
                    <GroupExpression>=Fields!Mes.Value</GroupExpression>
                  </GroupExpressions></Group></TablixMember>
                </TablixMembers></TablixColumnHierarchy>
                <TablixRowHierarchy><TablixMembers>
                  <TablixMember>
                    <Group Name="RowGrp0"><GroupExpressions>
                      <GroupExpression>=Fields!Regiao.Value</GroupExpression>
                    </GroupExpressions></Group>
                    <SortExpressions><SortExpression><Value>=Fields!Regiao.Value</Value></SortExpression></SortExpressions>
                  </TablixMember>
                </TablixMembers></TablixRowHierarchy>
                <DataSetName>Vendas</DataSetName>
                <NoRowsMessage>Sem dados</NoRowsMessage>
                <Top>5mm</Top><Left>5mm</Left><Width>40mm</Width><Height>16mm</Height>
              </Tablix>
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
    public void Matrix_tablix_round_trips_value_equal()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixRdl));

        // Sanity: a matrix imports as a TablixElement (in the ReportHeader band) with both group axes.
        var tablix = imported.ReportHeader!.Elements.OfType<TablixElement>().Should().ContainSingle().Subject;
        tablix.RowGroups.Should().ContainSingle(g => g.GroupExpression == "Fields.Regiao");
        tablix.ColumnGroups.Should().ContainSingle(g => g.GroupExpression == "Fields.Mes");
        tablix.Cells.Should().HaveCount(2);                 // corner (0,0) + body value (1,1)
        tablix.DataSetName.Should().Be("Vendas");

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));

        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes(),
            "a matrix Tablix must reconstruct exactly what the importer reads");
    }

    [Fact]
    public void Matrix_cells_and_sort_survive_the_round_trip()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixRdl));
        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));

        var tablix = reimported.ReportHeader!.Elements.OfType<TablixElement>().Single();
        tablix.Cells.Single(c => c is { RowIndex: 0, ColumnIndex: 0 }).Content
            .Should().BeOfType<LabelElement>().Which.Text.Should().Be("Região");
        var body = tablix.Cells.Single(c => c is { RowIndex: 1, ColumnIndex: 1 }).Content
            .Should().BeOfType<TextBoxElement>().Subject;
        body.Expression.Should().Be("Sum(Fields.Vendas)");
        body.Style.Format.Should().Be("C2");
        tablix.RowGroups.Single().SortExpression.Should().Be("Fields.Regiao");
    }

    [Fact]
    public void Save_emits_tablix_matrix_xml()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixRdl));
        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(imported));

        xml.Should().Contain("<Tablix").And.Contain("<TablixBody>").And.Contain("<TablixColumns>");
        xml.Should().Contain("<TablixRowHierarchy>").And.Contain("<TablixColumnHierarchy>");
        xml.Should().Contain("<GroupExpression>=Fields!Regiao.Value</GroupExpression>");
        xml.Should().Contain("<TablixCorner>").And.Contain("<DataSetName>Vendas</DataSetName>");
    }

    private const string MatrixSubtotalRdl = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body>
            <Height>50mm</Height>
            <ReportItems>
              <Tablix Name="M">
                <TablixBody>
                  <TablixColumns><TablixColumn><Width>40mm</Width></TablixColumn></TablixColumns>
                  <TablixRows><TablixRow><Height>8mm</Height><TablixCells><TablixCell>
                    <CellContents><Textbox Name="v"><Paragraphs><Paragraph><TextRuns>
                      <TextRun><Value>=Sum(Fields!Vendas.Value)</Value></TextRun>
                    </TextRuns></Paragraph></Paragraphs></Textbox></CellContents>
                  </TablixCell></TablixCells></TablixRow></TablixRows>
                </TablixBody>
                <TablixColumnHierarchy><TablixMembers><TablixMember/></TablixMembers></TablixColumnHierarchy>
                <TablixRowHierarchy><TablixMembers>
                  <TablixMember><Group Name="G"><GroupExpressions><GroupExpression>=Fields!Regiao.Value</GroupExpression></GroupExpressions></Group></TablixMember>
                  <TablixMember><Group/></TablixMember>
                </TablixMembers></TablixRowHierarchy>
                <DataSetName>Vendas</DataSetName>
                <Top>5mm</Top><Left>5mm</Left><Width>40mm</Width><Height>16mm</Height>
              </Tablix>
            </ReportItems>
          </Body>
          <Width>190mm</Width>
          <Page><PageHeight>297mm</PageHeight><PageWidth>210mm</PageWidth>
            <LeftMargin>10mm</LeftMargin><RightMargin>10mm</RightMargin><TopMargin>10mm</TopMargin><BottomMargin>10mm</BottomMargin></Page>
        </Report>
        """;

    [Fact]
    public void Row_subtotals_survive_the_round_trip()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixSubtotalRdl));
        imported.ReportHeader!.Elements.OfType<TablixElement>().Single().RowSubtotals.Should().BeTrue();

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));
        reimported.ReportHeader!.Elements.OfType<TablixElement>().Single().RowSubtotals.Should().BeTrue();
    }

    // A column total member (empty <Group/> sibling) on the column axis, with a dynamic row group keeping the
    // element on the matrix path. Guards the column subtotals write site against a flag swap.
    private const string MatrixColSubtotalRdl = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body>
            <Height>50mm</Height>
            <ReportItems>
              <Tablix Name="M">
                <TablixBody>
                  <TablixColumns><TablixColumn><Width>40mm</Width></TablixColumn></TablixColumns>
                  <TablixRows><TablixRow><Height>8mm</Height><TablixCells><TablixCell>
                    <CellContents><Textbox Name="v"><Paragraphs><Paragraph><TextRuns>
                      <TextRun><Value>=Sum(Fields!Vendas.Value)</Value></TextRun>
                    </TextRuns></Paragraph></Paragraphs></Textbox></CellContents>
                  </TablixCell></TablixCells></TablixRow></TablixRows>
                </TablixBody>
                <TablixColumnHierarchy><TablixMembers>
                  <TablixMember><Group Name="C"><GroupExpressions><GroupExpression>=Fields!Mes.Value</GroupExpression></GroupExpressions></Group></TablixMember>
                  <TablixMember><Group/></TablixMember>
                </TablixMembers></TablixColumnHierarchy>
                <TablixRowHierarchy><TablixMembers>
                  <TablixMember>
                    <Group Name="R"><GroupExpressions><GroupExpression>=Fields!Regiao.Value</GroupExpression></GroupExpressions></Group>
                    <SortExpressions><SortExpression><Value>=Fields!Regiao.Value</Value><Direction>Descending</Direction></SortExpression></SortExpressions>
                  </TablixMember>
                </TablixMembers></TablixRowHierarchy>
                <DataSetName>Vendas</DataSetName>
                <Top>5mm</Top><Left>5mm</Left><Width>40mm</Width><Height>16mm</Height>
              </Tablix>
            </ReportItems>
          </Body>
          <Width>190mm</Width>
          <Page><PageHeight>297mm</PageHeight><PageWidth>210mm</PageWidth>
            <LeftMargin>10mm</LeftMargin><RightMargin>10mm</RightMargin><TopMargin>10mm</TopMargin><BottomMargin>10mm</BottomMargin></Page>
        </Report>
        """;

    [Fact]
    public void Column_subtotals_and_descending_sort_survive_the_round_trip()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixColSubtotalRdl));
        var tablix = imported.ReportHeader!.Elements.OfType<TablixElement>().Single();
        tablix.ColumnSubtotals.Should().BeTrue();
        tablix.RowGroups.Single().SortDescending.Should().BeTrue();

        var back = rdl.LoadFromBytes(rdl.SaveToBytes(imported)).ReportHeader!.Elements.OfType<TablixElement>().Single();
        back.ColumnSubtotals.Should().BeTrue();
        back.RowGroups.Single().SortDescending.Should().BeTrue();
    }

    [Fact]
    public void No_rows_message_with_an_ampersand_round_trips_as_a_literal()
    {
        // The caption is a literal, not an expression — '&' must not be folded into Concat on re-import.
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixRdl));
        var tx = imported.ReportHeader!.Elements.OfType<TablixElement>().Single();
        var withMsg = imported with
        {
            ReportHeader = imported.ReportHeader with
            {
                Elements = new Reporting.Common.EquatableArray<Reporting.Elements.ReportElement>(
                    new Reporting.Elements.ReportElement[] { tx with { NoRowsMessage = "Total & Subtotal" } }),
            },
        };

        var back = rdl.LoadFromBytes(rdl.SaveToBytes(withMsg)).ReportHeader!.Elements.OfType<TablixElement>().Single();
        back.NoRowsMessage.Should().Be("Total & Subtotal");
    }

    [Fact]
    public void A_keyless_tablix_group_warns_instead_of_corrupting_the_shape()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(MatrixRdl));
        var tx = imported.ReportHeader!.Elements.OfType<TablixElement>().Single();
        var keyless = tx with
        {
            RowGroups = new Reporting.Common.EquatableArray<TablixGroup>(new[] { new TablixGroup("Rows0") }),
        };
        var def = imported with
        {
            ReportHeader = imported.ReportHeader with
            {
                Elements = new Reporting.Common.EquatableArray<Reporting.Elements.ReportElement>(
                    new Reporting.Elements.ReportElement[] { keyless }),
            },
        };

        using var ms = new MemoryStream();
        rdl.Save(def, ms);
        rdl.Warnings.Should().Contain(w => w.Contains("sem GroupExpression"));
    }
}
