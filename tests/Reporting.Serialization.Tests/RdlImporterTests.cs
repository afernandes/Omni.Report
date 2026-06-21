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
    public void ParseSize_returns_null_for_blank_or_unitless()
    {
        RdlImporter.ParseSize("").Should().BeNull();
        RdlImporter.ParseSize(null).Should().BeNull();
        RdlImporter.ParseSize("5").Should().BeNull("RDL mandates a unit; a bare value is unspecified");
        RdlImporter.ParseSize("5furlongs").Should().BeNull("unknown unit is unspecified, not guessed");
    }

    [Theory]
    [InlineData("=Fields!Nome.Value", "Fields.Nome")]
    [InlineData("=Parameters!P.Value", "Parameters.P")]
    [InlineData("=Globals!PageNumber", "PageNumber")]
    [InlineData("Texto literal", "Texto literal")]
    public void Expression_conversion_maps_common_refs(string raw, string expected)
    {
        Reporting.Serialization.Internal.RdlExpression.Convert(raw).Should().Be(expected);
    }

    [Fact]
    public void Expression_conversion_rewrites_vb_concat_to_Concat()
    {
        // VB '&' string concatenation → Concat(...), with field refs converted and literals preserved.
        Reporting.Serialization.Internal.RdlExpression.Convert("=\"Total: \" & Fields!Valor.Value")
            .Should().Be("Concat(\"Total: \", Fields.Valor)");
        Reporting.Serialization.Internal.RdlExpression.Convert("=Fields!A.Value & \" - \" & Fields!B.Value")
            .Should().Be("Concat(Fields.A, \" - \", Fields.B)");
        // No '&' → unchanged (single expression, not wrapped).
        Reporting.Serialization.Internal.RdlExpression.Convert("=Fields!X.Value").Should().Be("Fields.X");
        // '&' nested inside a function argument is also folded (common conditional-text pattern).
        Reporting.Serialization.Internal.RdlExpression.Convert("=IIf(Fields!Q.Value > 0, Fields!A.Value & \" un\", \"\")")
            .Should().Be("IIf(Fields.Q > 0, Concat(Fields.A, \" un\"), \"\")");
    }

    [Fact]
    public void Expression_conversion_does_not_silently_drop_non_value_members()
    {
        // Only `.Value` is rewritten; `.Count`/`.Label` survive (a visible error beats wrong data).
        Reporting.Serialization.Internal.RdlExpression.Convert("=Parameters!P.Count").Should().Contain("Count");
        Reporting.Serialization.Internal.RdlExpression.Convert("=Parameters!P.Label").Should().Contain("Label");
    }

    private const string Styled = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body>
            <Height>5cm</Height>
            <ReportItems>
              <Textbox Name="Total">
                <Top>0cm</Top><Left>0cm</Left><Width>4cm</Width><Height>0.6cm</Height>
                <CanGrow>true</CanGrow>
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Total.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Visibility><Hidden>=Fields!Oculto.Value</Hidden></Visibility>
                <Bookmark>bm-total</Bookmark>
                <Action><Hyperlink>=Fields!Url.Value</Hyperlink></Action>
                <Style>
                  <FontFamily>Calibri</FontFamily>
                  <FontSize>14pt</FontSize>
                  <FontWeight>Bold</FontWeight>
                  <Color>#FF0000</Color>
                  <BackgroundColor>Yellow</BackgroundColor>
                  <TextAlign>Right</TextAlign>
                  <VerticalAlign>Middle</VerticalAlign>
                  <Border><Color>Black</Color><Style>Solid</Style><Width>1pt</Width></Border>
                </Style>
              </Textbox>
            </ReportItems>
          </Body>
        </Report>
        """;

    [Fact]
    public void Item_style_is_imported()
    {
        var def = new RdlImporter().ImportXml(Styled);
        var tb = def.ReportHeader!.Elements.OfType<TextBoxElement>().Single();
        tb.Style.Font!.Family.Should().Be("Calibri");
        tb.Style.Font.Size.Should().Be(14);
        tb.Style.Font.Style.Should().HaveFlag(Reporting.Styling.FontStyle.Bold);
        tb.Style.ForeColor.Should().Be(Reporting.Styling.Color.FromHex("#FF0000"));
        tb.Style.BackColor.Should().Be(Reporting.Styling.Color.FromRgb(255, 255, 0));
        tb.Style.HorizontalAlignment.Should().Be(Reporting.Styling.HorizontalAlignment.Right);
        tb.Style.VerticalAlignment.Should().Be(Reporting.Styling.VerticalAlignment.Middle);
        tb.Style.Border!.Top.IsVisible.Should().BeTrue();
        tb.CanGrow.Should().BeTrue();
    }

    [Fact]
    public void Item_visibility_bookmark_and_action_are_imported()
    {
        var def = new RdlImporter().ImportXml(Styled);
        var tb = def.ReportHeader!.Elements.OfType<TextBoxElement>().Single();
        // RDL Hidden=expr → VisibleExpression = !(converted).
        tb.VisibleExpression.Should().Be("!(Fields.Oculto)");
        tb.Bookmark.Should().Be("bm-total");
        tb.Action.Should().NotBeNull();
        tb.Action!.Hyperlink.Should().Be("Fields.Url");
    }

    private const string ReportLevel = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <EmbeddedImages>
            <EmbeddedImage Name="Logo"><MimeType>image/png</MimeType><ImageData>AQIDBA==</ImageData></EmbeddedImage>
          </EmbeddedImages>
          <CustomProperties>
            <CustomProperty><Name>Autor</Name><Value>Equipe BI</Value></CustomProperty>
          </CustomProperties>
          <Code>Public Function Dobro(x As Integer) As Integer
            Return x * 2
          End Function</Code>
          <Body>
            <Height>3cm</Height>
            <ReportItems>
              <Image Name="Img">
                <Source>Embedded</Source><Value>Logo</Value>
                <Top>0cm</Top><Left>0cm</Left><Width>3cm</Width><Height>2cm</Height>
              </Image>
            </ReportItems>
          </Body>
        </Report>
        """;

    [Fact]
    public void Embedded_image_bytes_are_resolved_inline()
    {
        var def = new RdlImporter().ImportXml(ReportLevel);
        var img = def.ReportHeader!.Elements.OfType<Reporting.Elements.ImageElement>().Single();
        img.Source.Should().Be(Reporting.Elements.ImageSourceKind.Inline);
        img.InlineData.ToArray().Should().Equal((byte)1, (byte)2, (byte)3, (byte)4); // AQIDBA== decodes to 1,2,3,4
    }

    [Fact]
    public void Custom_properties_and_report_code_are_preserved_in_metadata()
    {
        var def = new RdlImporter().ImportXml(ReportLevel);
        def.Metadata["Autor"].Should().Be("Equipe BI");
        def.Metadata.ContainsKey("RdlCode").Should().BeTrue();
        def.Metadata["RdlCode"].Should().Contain("Dobro");
    }

    [Fact]
    public void Tablix_matrix_is_imported_with_groups_corner_and_body_value()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="Matrix1">
                  <Top>0cm</Top><Left>0cm</Left><Width>12cm</Width><Height>3cm</Height>
                  <DataSetName>Vendas</DataSetName>
                  <TablixCorner><TablixCornerRows><TablixCornerRow><TablixCornerCell><CellContents>
                    <Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>Região</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox>
                  </CellContents></TablixCornerCell></TablixCornerRow></TablixCornerRows></TablixCorner>
                  <TablixBody><TablixRows><TablixRow><TablixCells><TablixCell><CellContents>
                    <Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Sum(Fields!Total.Value)</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox>
                  </CellContents></TablixCell></TablixCells></TablixRow></TablixRows></TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember><Group Name="Mes">
                    <GroupExpressions><GroupExpression>=Fields!Mes.Value</GroupExpression></GroupExpressions></Group></TablixMember></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="Regiao">
                    <GroupExpressions><GroupExpression>=Fields!Regiao.Value</GroupExpression></GroupExpressions></Group></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        var t = def.ReportHeader!.Elements.OfType<Reporting.Elements.TablixElement>().Single();

        t.DataSetName.Should().Be("Vendas");
        t.RowGroups.Select(g => g.GroupExpression).Should().Equal("Fields.Regiao");
        t.ColumnGroups.Select(g => g.GroupExpression).Should().Equal("Fields.Mes");
        t.Cells.Should().Contain(c => c.RowIndex == 0 && c.ColumnIndex == 0
            && ((Reporting.Elements.LabelElement)c.Content!).Text == "Região");
        var body = t.Cells.Single(c => c.RowIndex == 1 && c.ColumnIndex == 1).Content;
        ((Reporting.Elements.TextBoxElement)body!).Expression.Should().Be("Sum(Fields.Total)");
        def.Metadata.ContainsKey("ImportWarnings").Should().BeFalse("a clean matrix has nothing to warn about");
    }

    [Fact]
    public void Tablix_without_both_hierarchies_records_a_warning()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="FlatTable">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>2cm</Height>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="Det" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        def.Metadata["ImportWarnings"].Should().Contain("FlatTable");
    }

    [Fact]
    public void DataSets_are_imported_with_fields_calculated_filter_sort_and_query()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <DataSets>
                <DataSet Name="Vendas">
                  <Query>
                    <CommandText>SELECT * FROM Vendas</CommandText>
                    <CommandType>Text</CommandType>
                    <QueryParameters>
                      <QueryParameter Name="@Ano"><Value>=Parameters!Ano.Value</Value></QueryParameter>
                    </QueryParameters>
                  </Query>
                  <Fields>
                    <Field Name="Total"><DataField>Total</DataField><TypeName>System.Decimal</TypeName></Field>
                    <Field Name="Imposto"><Value>=Fields!Total.Value * 0.1</Value></Field>
                  </Fields>
                  <Filters>
                    <Filter>
                      <FilterExpression>=Fields!Total.Value</FilterExpression>
                      <Operator>GreaterThan</Operator>
                      <FilterValues><FilterValue>0</FilterValue></FilterValues>
                    </Filter>
                  </Filters>
                  <SortExpressions>
                    <SortExpression><Value>=Fields!Total.Value</Value><Direction>Descending</Direction></SortExpression>
                  </SortExpressions>
                </DataSet>
              </DataSets>
            </Report>
            """;
        var ds = new RdlImporter().ImportXml(rdl).DataSources[0];

        ds.Name.Should().Be("Vendas");
        ds.Fields.Select(f => f.Name).Should().Equal("Total");
        ds.Fields[0].FieldType.Should().Be(typeof(decimal));
        ds.CalculatedFields.Should().ContainSingle();
        ds.CalculatedFields[0].Name.Should().Be("Imposto");
        ds.CalculatedFields[0].Expression.Should().Be("Fields.Total * 0.1");
        ds.FilterExpression.Should().Be("Fields.Total > 0");
        ds.SortExpressions[0].Expression.Should().Be("Fields.Total");
        ds.SortExpressions[0].Direction.Should().Be(Reporting.Data.SortDirection.Descending);
        ds.Parameters["CommandText"].Should().Be("SELECT * FROM Vendas");
        ds.Parameters["QueryParameter:@Ano"].Should().Be("Parameters.Ano");
    }

    [Fact]
    public void DataSet_filters_combine_and_skip_unsupported_or_valueless()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <DataSets><DataSet Name="D"><Filters>
                <Filter><FilterExpression>=Fields!A.Value</FilterExpression><Operator>GreaterThanOrEqual</Operator><FilterValues><FilterValue>10</FilterValue></FilterValues></Filter>
                <Filter><FilterExpression>=Fields!B.Value</FilterExpression><Operator>NotEqual</Operator><FilterValues><FilterValue>x</FilterValue></FilterValues></Filter>
                <Filter><FilterExpression>=Fields!C.Value</FilterExpression><Operator>In</Operator><FilterValues><FilterValue>1</FilterValue></FilterValues></Filter>
                <Filter><FilterExpression>=Fields!D.Value</FilterExpression><Operator>GreaterThan</Operator><FilterValues></FilterValues></Filter>
              </Filters></DataSet></DataSets>
            </Report>
            """;
        var ds = new RdlImporter().ImportXml(rdl).DataSources[0];
        // A >= 10 AND B <> "x"; the unsupported In and the valueless GreaterThan are dropped, not fabricated.
        ds.FilterExpression.Should().Be("Fields.A >= 10 && Fields.B <> \"x\"");
    }

    [Fact]
    public void Report_variables_are_imported()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Variables>
                <Variable Name="Acumulado"><Value>=Sum(Fields!Total.Value)</Value></Variable>
              </Variables>
            </Report>
            """;
        var v = new RdlImporter().ImportXml(rdl).Variables[0];
        v.Name.Should().Be("Acumulado");
        v.Expression.Should().Be("Sum(Fields.Total)");
        v.Scope.Should().Be(Reporting.Parameters.VariableScope.Report);
    }

    [Fact]
    public void Parameter_metadata_hidden_nullable_allowblank_are_imported()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <ReportParameters>
                <ReportParameter Name="P">
                  <DataType>String</DataType>
                  <Nullable>true</Nullable><AllowBlank>true</AllowBlank><Hidden>true</Hidden>
                </ReportParameter>
              </ReportParameters>
            </Report>
            """;
        var p = new RdlImporter().ImportXml(rdl).Parameters[0];
        p.Nullable.Should().BeTrue();
        p.AllowBlank.Should().BeTrue();
        p.Hidden.Should().BeTrue();
    }
}
