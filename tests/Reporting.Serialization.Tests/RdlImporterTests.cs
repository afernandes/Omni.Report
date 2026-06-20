using FluentAssertions;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

public class RdlImporterTests
{
    private const string Sample = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Page>
            <PageHeight>29.7cm</PageHeight>
            <PageWidth>21cm</PageWidth>
            <LeftMargin>2cm</LeftMargin>
            <RightMargin>2cm</RightMargin>
            <TopMargin>1cm</TopMargin>
            <BottomMargin>1cm</BottomMargin>
            <PageHeader>
              <Height>2cm</Height>
              <ReportItems>
                <Textbox Name="Title">
                  <Top>0cm</Top><Left>0cm</Left><Width>10cm</Width><Height>1cm</Height>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Relatório de Vendas</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems>
            </PageHeader>
          </Page>
          <Body>
            <Height>5cm</Height>
            <ReportItems>
              <Textbox Name="Cliente">
                <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>0.6cm</Height>
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Nome.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
              </Textbox>
              <Rectangle Name="Box">
                <Top>1cm</Top><Left>3cm</Left><Width>8cm</Width><Height>2cm</Height>
                <ReportItems>
                  <Textbox Name="Total">
                    <Top>0.5cm</Top><Left>0.5cm</Left><Width>4cm</Width><Height>0.6cm</Height>
                    <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Sum(Fields!Total.Value)</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                  </Textbox>
                </ReportItems>
              </Rectangle>
            </ReportItems>
          </Body>
          <ReportParameters>
            <ReportParameter Name="Status">
              <DataType>String</DataType>
              <Prompt>Situação</Prompt>
              <ValidValues>
                <ParameterValues>
                  <ParameterValue><Value>A</Value><Label>Ativo</Label></ParameterValue>
                  <ParameterValue><Value>I</Value><Label>Inativo</Label></ParameterValue>
                </ParameterValues>
              </ValidValues>
            </ReportParameter>
            <ReportParameter Name="Cliente">
              <DataType>String</DataType>
              <MultiValue>true</MultiValue>
              <ValidValues>
                <DataSetReference>
                  <DataSetName>Clientes</DataSetName>
                  <ValueField>Id</ValueField>
                  <LabelField>Nome</LabelField>
                </DataSetReference>
              </ValidValues>
            </ReportParameter>
          </ReportParameters>
        </Report>
        """;

    private static ReportDefinition Import() => new RdlImporter().ImportXml(Sample, "Vendas");

    [Fact]
    public void Page_size_and_margins_are_imported()
    {
        var def = Import();
        def.PageSetup.Paper.Width.Should().Be(Unit.FromCm(21));
        def.PageSetup.Paper.Height.Should().Be(Unit.FromCm(29.7));
        def.PageSetup.Margins.Left.Should().Be(Unit.FromCm(2));
        def.PageSetup.Margins.Top.Should().Be(Unit.FromCm(1));
    }

    [Fact]
    public void Parameters_import_with_static_and_query_available_values()
    {
        var def = Import();
        def.Parameters.Count.Should().Be(2);

        var status = def.Parameters[0];
        status.Name.Should().Be("Status");
        status.Prompt.Should().Be("Situação");
        status.AvailableValues!.Values.Select(v => v.Value).Should().Equal("A", "I");
        status.AvailableValues.Values[0].Label.Should().Be("Ativo");

        var cliente = def.Parameters[1];
        cliente.AllowMultiple.Should().BeTrue();
        cliente.AvailableValues!.IsQuery.Should().BeTrue();
        cliente.AvailableValues.DataSet.Should().Be("Clientes");
        cliente.AvailableValues.ValueField.Should().Be("Id");
        cliente.AvailableValues.LabelField.Should().Be("Nome");
    }

    [Fact]
    public void Page_header_literal_textbox_becomes_a_label()
    {
        var def = Import();
        var label = def.PageHeader!.Elements.OfType<LabelElement>().Single();
        label.Text.Should().Be("Relatório de Vendas");
    }

    [Fact]
    public void Body_field_textbox_becomes_a_textbox_with_converted_expression()
    {
        var def = Import();
        var texts = def.ReportHeader!.Elements.OfType<TextBoxElement>().Select(t => t.Expression).ToList();
        texts.Should().Contain("Fields.Nome");
        texts.Should().Contain("Sum(Fields.Total)"); // VB Fields!X.Value → Fields.X inside the aggregate
    }

    [Fact]
    public void Rectangle_is_imported_and_nested_items_are_offset_to_absolute()
    {
        var def = Import();
        def.ReportHeader!.Elements.OfType<RectangleElement>().Should().ContainSingle();
        // The nested Total textbox sits at rect(3cm,1cm) + (0.5cm,0.5cm) = (3.5cm, 1.5cm) absolute.
        var total = def.ReportHeader.Elements.OfType<TextBoxElement>().Single(t => t.Expression.Contains("Sum"));
        total.Bounds.X.Should().Be(Unit.FromCm(3.5));
        total.Bounds.Y.Should().Be(Unit.FromCm(1.5));
    }

    [Theory]
    [InlineData("2.5in")]
    [InlineData("21cm")]
    [InlineData("10mm")]
    [InlineData("20pt")]
    public void ParseSize_handles_rdl_units(string raw)
    {
        RdlImporter.ParseSize(raw).Should().NotBeNull();
    }

    [Fact]
    public void ParseSize_returns_null_for_blank()
    {
        RdlImporter.ParseSize("").Should().BeNull();
        RdlImporter.ParseSize(null).Should().BeNull();
    }
}
