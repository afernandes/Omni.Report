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
        // The free Cliente textbox is a band sibling…
        def.ReportHeader!.Elements.OfType<TextBoxElement>().Select(t => t.Expression).Should().Contain("Fields.Nome");
        // …and the Total textbox nested in the Rectangle keeps its converted aggregate expression.
        var nested = def.ReportHeader.Elements.OfType<RectangleElement>().Single()
            .Children.OfType<TextBoxElement>().Select(t => t.Expression);
        nested.Should().Contain("Sum(Fields.Total)"); // VB Fields!X.Value → Fields.X inside the aggregate
    }

    [Fact]
    public void Rectangle_is_imported_as_a_container_preserving_nested_hierarchy()
    {
        var def = Import();
        // The Rectangle keeps its nested item as a CHILD (relative bounds) instead of flattening it to an
        // absolute sibling in the band — so the band has exactly the rect + the free Cliente textbox.
        var rect = def.ReportHeader!.Elements.OfType<RectangleElement>().Should().ContainSingle().Subject;
        def.ReportHeader.Elements.OfType<TextBoxElement>().Should().ContainSingle()
            .Which.Expression.Should().Contain("Nome"); // only the free Cliente textbox is a band sibling
        // The nested Total textbox lives in rect.Children with bounds RELATIVE to the rectangle (0.5cm,0.5cm),
        // NOT the old absolute (3.5cm,1.5cm).
        var total = rect.Children.OfType<TextBoxElement>().Single(t => t.Expression.Contains("Sum"));
        total.Bounds.X.Should().Be(Unit.FromCm(0.5));
        total.Bounds.Y.Should().Be(Unit.FromCm(0.5));
    }

    [Fact]
    public void Rectangle_nested_inside_rectangle_imports_recursively_with_relative_bounds()
    {
        const string rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>6cm</Height><ReportItems>
                <Rectangle Name="Outer">
                  <Top>1cm</Top><Left>1cm</Left><Width>10cm</Width><Height>5cm</Height>
                  <ReportItems>
                    <Rectangle Name="Inner">
                      <Top>0.5cm</Top><Left>0.5cm</Left><Width>4cm</Width><Height>2cm</Height>
                      <ReportItems>
                        <Textbox Name="Leaf">
                          <Top>0.25cm</Top><Left>0.25cm</Left><Width>3cm</Width><Height>0.6cm</Height>
                          <Paragraphs><Paragraph><TextRuns><TextRun><Value>oi</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                        </Textbox>
                      </ReportItems>
                    </Rectangle>
                  </ReportItems>
                </Rectangle>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        var outer = def.ReportHeader!.Elements.OfType<RectangleElement>().Single();
        var inner = outer.Children.OfType<RectangleElement>().Single();
        inner.Bounds.X.Should().Be(Unit.FromCm(0.5), "inner rect bounds are RELATIVE to the outer rect");
        var leaf = inner.Children.OfType<LabelElement>().Single(); // literal text → Label
        leaf.Bounds.X.Should().Be(Unit.FromCm(0.25), "leaf bounds are RELATIVE to the inner rect");
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

    [Fact]
    public void Image_Source_Database_imports_as_an_expression()
    {
        // <Source>Database</Source><Value>=Fields!Foto.Value</Value> → Expression source; the renderer's
        // ResolveExpression turns the resulting byte[] into the drawn image.
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Image Name="foto"><Top>0cm</Top><Left>0cm</Left><Width>3cm</Width><Height>3cm</Height>
                  <Source>Database</Source><Value>=Fields!Foto.Value</Value><MIMEType>image/png</MIMEType>
                </Image>
              </ReportItems></Body>
            </Report>
            """;
        var img = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.ImageElement>().Single();
        img.Source.Should().Be(Reporting.Elements.ImageSourceKind.Expression);
        img.Expression.Should().Be("Fields.Foto");
    }

    [Theory]
    [InlineData("logo.png", "logo.png", null)]                  // External literal path
    [InlineData("=Fields!Logo.Value", null, "Fields.Logo")]     // External expression
    public void Style_BackgroundImage_External_imports(string value, string? expectedPath, string? expectedExpr)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Textbox Name="t"><Top>0cm</Top><Left>0cm</Left><Width>5cm</Width><Height>1cm</Height>
                  <Style><BackgroundImage><Source>External</Source><Value>{value}</Value></BackgroundImage></Style>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>x</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var el = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.Single();
        el.Style.BackgroundImage.Should().NotBeNull();
        var bg = el.Style.BackgroundImage!;
        bg.Path.Should().Be(expectedPath);
        bg.Expression.Should().Be(expectedExpr);
    }

    [Theory]
    [InlineData("Fit", "Stretch")]              // RDL Fit = stretch to box (distorts)
    [InlineData("FitProportional", "Fit")]      // preserve aspect (letterbox)
    [InlineData("Clip", "Native")]              // native size, clipped
    [InlineData("AutoSize", "Fit")]             // no fixed-bounds equivalent → model default
    [InlineData(null, "Fit")]                   // absent → model default
    public void Image_Sizing_is_imported(string? rdlSizing, string expected)
    {
        var sizingEl = rdlSizing is null ? "" : $"<Sizing>{rdlSizing}</Sizing>";
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>4cm</Height><ReportItems>
                <Image Name="L"><Top>0cm</Top><Left>0cm</Left><Width>4cm</Width><Height>3cm</Height>
                  <Source>External</Source><Value>logo.png</Value>{sizingEl}
                </Image>
              </ReportItems></Body>
            </Report>
            """;
        var img = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.ImageElement>().Single();
        img.Sizing.Should().Be(Enum.Parse<Reporting.Elements.ImageSizing>(expected));
    }

    [Theory]
    [InlineData("<PageBreak><BreakLocation>Start</BreakLocation></PageBreak>", "Start")]
    [InlineData("<PageBreak><BreakLocation>End</BreakLocation></PageBreak>", "End")]
    [InlineData("<PageBreak><BreakLocation>StartAndEnd</BreakLocation></PageBreak>", "StartAndEnd")]
    [InlineData("<PageBreakAtStart>true</PageBreakAtStart>", "Start")]            // RDL 2005 legacy
    [InlineData("<PageBreakAtEnd>true</PageBreakAtEnd>", "End")]
    [InlineData("<PageBreakAtStart>true</PageBreakAtStart><PageBreakAtEnd>true</PageBreakAtEnd>", "StartAndEnd")]
    [InlineData("", "None")]                                                       // absent → None (no regression)
    public void Tablix_PageBreak_is_imported_to_the_detail_band(string pageBreakXml, string expected)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="T"><Top>0cm</Top><Left>0cm</Left><Width>6cm</Width><Height>2cm</Height>
                  <DataSetName>Vendas</DataSetName>{pageBreakXml}
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>6cm</Width></TablixColumn></TablixColumns>
                    <TablixRows><TablixRow><TablixCells><TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!P.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell></TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember /></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="Details" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        new RdlImporter().ImportXml(rdl).Detail.PageBreak
            .Should().Be(Enum.Parse<Reporting.Bands.PageBreak>(expected));
    }

    [Fact]
    public void Page_columns_and_spacing_are_imported()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Page><Columns>3</Columns><ColumnSpacing>1cm</ColumnSpacing></Page>
              <Body><Height>2cm</Height><ReportItems /></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        def.PageSetup.Columns.Should().Be(3);
        def.PageSetup.ColumnSpacing.ToCm().Should().BeApproximately(1, 0.01);
    }

    [Fact]
    public void No_columns_defaults_to_single_column()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems /></Body>
            </Report>
            """;
        new RdlImporter().ImportXml(rdl).PageSetup.Columns.Should().Be(1);
    }

    [Fact]
    public void Report_level_metadata_is_imported_and_round_trips()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Description>Relatório de vendas mensal</Description>
              <Author>Ana Silva</Author>
              <AutoRefresh>1800</AutoRefresh>
              <Language>en-US</Language>
              <Body><Height>2cm</Height><ReportItems /></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        def.Metadata["Description"].Should().Be("Relatório de vendas mensal");
        def.Metadata["Author"].Should().Be("Ana Silva");
        def.Metadata["AutoRefresh"].Should().Be("1800");
        def.Metadata["Language"].Should().Be("en-US"); // also locks in the existing Language import

        // The generic Metadata bag round-trips through both formats without serializer changes.
        var repx = new RepxSerializer();
        repx.LoadFromBytes(repx.SaveToBytes(def)).Metadata.Should().Contain(def.Metadata);
        var json = new RepJsonSerializer();
        json.LoadFromBytes(json.SaveToBytes(def)).Metadata.Should().Contain(def.Metadata);
    }

    [Fact]
    public void Absent_report_metadata_creates_no_keys()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems /></Body>
            </Report>
            """;
        var meta = new RdlImporter().ImportXml(rdl).Metadata;
        meta.Should().NotContainKey("Description");
        meta.Should().NotContainKey("Author");
        meta.Should().NotContainKey("AutoRefresh");
    }

    [Fact]
    public void Report_Language_is_imported_into_Metadata()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Language>en-US</Language>
              <Body><Height>2cm</Height><ReportItems /></Body>
            </Report>
            """;
        new RdlImporter().ImportXml(rdl).Metadata.Should().ContainKey("Language").WhoseValue.Should().Be("en-US");
    }

    [Fact]
    public void Report_without_Language_has_no_Metadata_key()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems /></Body>
            </Report>
            """;
        new RdlImporter().ImportXml(rdl).Metadata.Should().NotContainKey("Language");
    }

    [Theory]
    [InlineData("Sem dados disponíveis.", "Sem dados disponíveis.")]              // literal stays literal
    [InlineData("=&quot;Nada para &quot; &amp; Parameters!Ano.Value", "Concat(\"Nada para \", Parameters.Ano)")] // expression converted
    public void Tablix_NoRowsMessage_is_imported(string rdlMessage, string expected)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="T"><Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>2cm</Height>
                  <NoRowsMessage>{rdlMessage}</NoRowsMessage>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>8cm</Width></TablixColumn></TablixColumns>
                    <TablixRows><TablixRow><Height>1cm</Height><TablixCells><TablixCell><CellContents>
                      <Textbox Name="c0"><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!X.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox>
                    </CellContents></TablixCell></TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember /></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="d" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        // A lone flat Tablix decomposes into a DetailBand, which carries the NoRowsMessage (also expression-converted).
        new RdlImporter().ImportXml(rdl).Detail.NoRowsMessage.Should().Be(expected);
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

    [Theory]
    // Globals: ReportName stays a bare identifier (resolved by the evaluator); OverallPageNumber→PageNumber.
    [InlineData("=Globals!ReportName", "ReportName")]
    [InlineData("=Globals!OverallPageNumber", "PageNumber")]
    [InlineData("=Globals!OverallTotalPages", "TotalPages")]
    [InlineData("=Fields!Nome.Value Like \"A*\"", "Like(Fields.Nome, \"A*\")")]
    [InlineData("=Campo Like \"A*\"", "Like(Campo, \"A*\")")] // non-.Value member left intact
    [InlineData("=Fields!A.Value Like Parameters!P.Value", "Like(Fields.A, Parameters.P)")]
    [InlineData("=IIf(Fields!N.Value Like \"A*\", 1, 0)", "IIf(Like(Fields.N, \"A*\"), 1, 0)")]
    // VB precedence: & binds tighter than Like → Like(Concat(a,b), "X*").
    [InlineData("=Fields!A.Value & Fields!B.Value Like \"X*\"", "Like(Concat(Fields.A, Fields.B), \"X*\")")]
    // A literal "Like" inside a string is preserved (not rewritten).
    [InlineData("=\"x Like y\"", "\"x Like y\"")]
    // Idempotent: an existing Like(...) function call is not re-wrapped.
    [InlineData("=Like(Fields!A.Value, \"Z*\")", "Like(Fields.A, \"Z*\")")]
    // Word boundary: "Like" as a substring of an identifier does not fire.
    [InlineData("=Fields!Likelihood.Value", "Fields.Likelihood")]
    // Chained Like folds left-associatively (rare but pinned).
    [InlineData("=Fields!A.Value Like \"X*\" Like \"Y*\"", "Like(Like(Fields.A, \"X*\"), \"Y*\")")]
    public void Vb_Like_infix_is_rewritten_to_the_Like_function(string rdlExpr, string expected)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems>
                <Textbox Name="T"><Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>1cm</Height>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>{System.Security.SecurityElement.Escape(rdlExpr)}</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var tb = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.TextBoxElement>().Single();
        tb.Expression.Should().Be(expected);
    }

    [Theory]
    // VB escapes a literal double-quote inside a string by doubling it (""). The four quote-aware scanners
    // (SplitTopLevel, SplitTopLevelLike, ConvertParenGroups, MatchingParen) now treat "" as ONE literal
    // quote and stay in-string, instead of the previous close-then-reopen. That old path actually produced
    // identical output for every input (the two quotes are adjacent, so the state reconverges before any
    // split/paren/comma decision) — verified by a differential harness over ~8M inputs — so these cases are
    // regression lock-ins for the now-explicit handling, NOT cases that fail without it. Raised by the
    // adversarial review of #102 (infix Like). Covers Like / & / parens following a "" string.
    // (a) Like after a string containing "".
    [InlineData("=Fields!A.Value Like \"he said \"\"hi\"\"\"", "Like(Fields.A, \"he said \"\"hi\"\"\")")]
    // (b) & / Concat after a string containing "" — string as the first and as the second operand.
    [InlineData("=\"x \"\"y\"\"\" & Fields!A.Value", "Concat(\"x \"\"y\"\"\", Fields.A)")]
    [InlineData("=Fields!A.Value & \"x \"\"y\"\"\"", "Concat(Fields.A, \"x \"\"y\"\"\")")]
    // (c) Balanced parens after a "" string — MatchingParen / ConvertParenGroups / comma-split must keep the
    // closing paren and the argument count right despite the doubled quotes.
    [InlineData("=IIf(Fields!Cond.Value, \"a \"\"b\"\"\", Fields!C.Value)", "IIf(Fields.Cond, \"a \"\"b\"\"\", Fields.C)")]
    // Combined: a "" string inside a function argument that also carries an infix Like (recurses through
    // ConvertParenGroups → ConvertLike), so all four scanners run over the doubled quotes at once.
    [InlineData("=IIf(Fields!N.Value Like \"x \"\"y\"\"\", 1, 0)", "IIf(Like(Fields.N, \"x \"\"y\"\"\"), 1, 0)")]
    public void Expression_conversion_handles_vb_escaped_double_quotes(string raw, string expected)
    {
        Reporting.Serialization.Internal.RdlExpression.Convert(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("0pt", "100pt", "Horizontal")]   // flat height → horizontal ruler
    [InlineData("50pt", "0pt", "Vertical")]       // flat width → vertical ruler
    [InlineData("40pt", "60pt", "TopLeftToBottomRight")] // both sized → diagonal
    public void Line_direction_is_inferred_from_bounds(string height, string width, string expectedDir)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Line Name="L"><Top>0cm</Top><Left>0cm</Left><Width>{width}</Width><Height>{height}</Height>
                  <Style><Border><Color>Red</Color><Width>2pt</Width></Border></Style>
                </Line>
              </ReportItems></Body>
            </Report>
            """;
        var line = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.LineElement>().Single();
        line.Direction.Should().Be(Enum.Parse<Reporting.Elements.LineDirection>(expectedDir));
        line.Pen.Should().NotBeNull("a <Style><Border> still maps to the line Pen via ApplyCommon");
    }

    [Theory]
    [InlineData("NoWrap", false)]
    [InlineData("WordWrap", true)]
    public void Style_WrapMode_is_imported_to_WordWrap(string wrapMode, bool expected)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems>
                <Textbox Name="T"><Top>0cm</Top><Left>0cm</Left><Width>3cm</Width><Height>1cm</Height>
                  <Style><WrapMode>{wrapMode}</WrapMode></Style>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!X.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var tb = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.TextBoxElement>().Single();
        tb.Style.WordWrap.Should().Be(expected);
    }

    [Fact]
    public void Style_without_WrapMode_keeps_WordWrap_default_true()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems>
                <Textbox Name="T"><Top>0cm</Top><Left>0cm</Left><Width>3cm</Width><Height>1cm</Height>
                  <Style><FontWeight>Bold</FontWeight></Style>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!X.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var tb = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.TextBoxElement>().Single();
        tb.Style.WordWrap.Should().BeTrue("WrapMode ausente → default do model (wrap)");
    }

    [Fact]
    public void ReportItems_reference_and_element_name_are_imported()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems>
                <Textbox Name="Titulo">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>1cm</Height>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Capitulo.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
                <Textbox Name="Eco">
                  <Top>1cm</Top><Left>0cm</Left><Width>8cm</Width><Height>1cm</Height>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>=ReportItems!Titulo.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var els = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements;
        // The named text box keeps its RDL Name (enables ReportItems references).
        els.Should().Contain(e => e.Name == "Titulo");
        // The reference is converted to the OmniReport scope form.
        var eco = els.OfType<Reporting.Elements.TextBoxElement>().Single(t => t.Name == "Eco");
        eco.Expression.Should().Be("ReportItems.Titulo");
    }

    [Fact]
    public void Textbox_with_multiple_runs_imports_as_TextRuns()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems>
                <Textbox Name="Saudacao">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>1cm</Height>
                  <Paragraphs><Paragraph><TextRuns>
                    <TextRun><Value>Olá </Value></TextRun>
                    <TextRun><Value>=Fields!Nome.Value</Value><Style><FontWeight>Bold</FontWeight></Style>
                      <ActionInfo><Actions><Action><Hyperlink>=Fields!Url.Value</Hyperlink></Action></Actions></ActionInfo></TextRun>
                    <TextRun><Value>!</Value></TextRun>
                  </TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var tb = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.TextBoxElement>().Single();

        tb.TextRuns.Select(r => r.Value).Should().Equal("Olá ", "Fields.Nome", "!");
        tb.TextRuns[1].Style!.Font!.Style.Should().HaveFlag(Reporting.Styling.FontStyle.Bold);
        tb.TextRuns[1].Action.Should().NotBeNull("o ActionInfo por-run vira ElementAction");
        // Fallback Expression = template concatenando os runs (literal verbatim, expressão como {expr}).
        tb.Expression.Should().Be("Olá {Fields.Nome}!");
    }

    [Fact]
    public void Textbox_html_markup_run_warns_and_flattens()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems>
                <Textbox Name="Rico">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>1cm</Height>
                  <Paragraphs><Paragraph><TextRuns>
                    <TextRun><Value>a</Value></TextRun>
                    <TextRun><Value>&lt;b&gt;b&lt;/b&gt;</Value><MarkupType>HTML</MarkupType></TextRun>
                  </TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        def.Metadata["ImportWarnings"].Should().Contain("HTML");
    }

    [Fact]
    public void Single_run_textbox_keeps_the_legacy_path()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>2cm</Height><ReportItems>
                <Textbox Name="Simples">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>1cm</Height>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Total.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var tb = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.TextBoxElement>().Single();
        tb.Expression.Should().Be("Fields.Total");
        tb.TextRuns.Should().BeEmpty("um único run continua no caminho single-expression");
    }

    [Fact]
    public void DataViz_chart_gauge_and_subreport_are_imported()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>6cm</Height><ReportItems>
                <Chart Name="Ch">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>5cm</Height>
                  <ChartData><ChartSeriesCollection><ChartSeries Name="S1"><Type>Line</Type>
                    <DataPoints><DataPoint><DataValues><DataValue><Value>=Sum(Fields!Total.Value)</Value></DataValue></DataValues></DataPoint></DataPoints>
                  </ChartSeries></ChartSeriesCollection></ChartData>
                  <ChartCategoryHierarchy><ChartMembers><ChartMember><Group><GroupExpressions><GroupExpression>=Fields!Mes.Value</GroupExpression></GroupExpressions></Group></ChartMember></ChartMembers></ChartCategoryHierarchy>
                </Chart>
                <GaugePanel Name="G">
                  <Top>0cm</Top><Left>9cm</Left><Width>4cm</Width><Height>4cm</Height>
                  <GaugePanelItems><RadialGauge Name="R"><GaugeScales><GaugeScale>
                    <Maximum><Value>200</Value></Maximum>
                    <GaugePointers><GaugePointer>
                    <GaugeInputValue><Value>=Fields!Pct.Value</Value></GaugeInputValue>
                  </GaugePointer></GaugePointers></GaugeScale></GaugeScales></RadialGauge></GaugePanelItems>
                </GaugePanel>
                <Subreport Name="Sub">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>3cm</Height>
                  <ReportName>Detalhe</ReportName>
                  <Parameters><Parameter Name="id"><Value>=Fields!Id.Value</Value></Parameter></Parameters>
                </Subreport>
              </ReportItems></Body>
            </Report>
            """;
        var els = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements;

        var chart = els.OfType<Reporting.Elements.ChartElement>().Single();
        chart.Kind.Should().Be(Reporting.Elements.ChartKind.Line);
        chart.Series.Should().ContainSingle();
        chart.Series[0].ValueExpression.Should().Be("Sum(Fields.Total)");
        chart.Series[0].CategoryExpression.Should().Be("Fields.Mes");

        var gauge = els.OfType<Reporting.Elements.GaugeElement>().Single();
        gauge.Kind.Should().Be(Reporting.Elements.GaugeKind.Radial);
        gauge.ValueExpression.Should().Be("Fields.Pct");

        var sub = els.OfType<Reporting.Elements.SubreportElement>().Single();
        sub.ReportId.Should().Be("Detalhe");
        sub.ParameterBindings["id"].Should().Be("Fields.Id");
    }

    [Fact]
    public void Style_properties_valued_by_expression_import_as_PropertyExpressions()
    {
        // Conditional formatting (negative-in-red, threshold colouring) is expressed as =expr in RDL <Style>.
        // ReadStyle only parses literals, so these were silently dropped; now they become PropertyExpressions.
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Textbox Name="t"><Top>0cm</Top><Left>0cm</Left><Width>5cm</Width><Height>1cm</Height>
                  <Style>
                    <Color>=IIf(Fields!Saldo.Value &lt; 0, "#FF0000", "#000000")</Color>
                    <Format>=IIf(Fields!Pct.Value &gt; 1, "P2", "N2")</Format>
                    <FontFamily>Arial</FontFamily>
                  </Style>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Saldo.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var el = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.Single();
        // The two expression-valued style props became bindings; the literal FontFamily did NOT.
        el.PropertyExpressions.Should().ContainKey("Style.ForeColor");
        el.PropertyExpressions["Style.ForeColor"].Should().Contain("IIf").And.Contain("Fields.Saldo");
        el.PropertyExpressions.Should().ContainKey("Style.Format");
        el.PropertyExpressions.Should().NotContainKey("Style.Font.Family", "a literal style value stays literal, not a binding");
    }

    [Fact]
    public void Gauge_scale_ranges_and_min_max_are_imported()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>5cm</Height><ReportItems>
                <GaugePanel Name="G"><Top>0cm</Top><Left>0cm</Left><Width>4cm</Width><Height>4cm</Height>
                  <GaugePanelItems><RadialGauge Name="R"><GaugeScales><GaugeScale>
                    <Minimum><Value>0</Value></Minimum>
                    <Maximum><Value>120</Value></Maximum>
                    <ScaleRanges>
                      <ScaleRange><StartValue>0</StartValue><EndValue>60</EndValue><BackgroundColor>Green</BackgroundColor></ScaleRange>
                      <ScaleRange><StartValue>60</StartValue><EndValue>90</EndValue><BackgroundColor>#FFFF00</BackgroundColor></ScaleRange>
                      <ScaleRange><StartValue>90</StartValue><EndValue>120</EndValue><BackgroundColor>Red</BackgroundColor></ScaleRange>
                    </ScaleRanges>
                    <GaugePointers><GaugePointer><GaugeInputValue><Value>=Fields!Pct.Value</Value></GaugeInputValue></GaugePointer></GaugePointers>
                  </GaugeScale></GaugeScales></RadialGauge></GaugePanelItems>
                </GaugePanel>
              </ReportItems></Body>
            </Report>
            """;
        var gauge = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.OfType<Reporting.Elements.GaugeElement>().Single();
        gauge.MinimumExpression.Should().Be("0");
        gauge.MaximumExpression.Should().Be("120");
        gauge.Ranges.Should().HaveCount(3);
        gauge.Ranges[0].StartExpression.Should().Be("0");
        gauge.Ranges[0].EndExpression.Should().Be("60");
        gauge.Ranges[0].ColorHex.Should().Be(Reporting.Styling.Color.FromRgb(0, 128, 0).ToHex(), "Green");
        gauge.Ranges[1].ColorHex.Should().Be(Reporting.Styling.Color.FromHex("#FFFF00").ToHex(), "hex passes through");
    }

    [Fact]
    public void Tablix_flat_table_is_imported_with_header_and_detail_cells()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="Tabela1">
                  <Top>0cm</Top><Left>0cm</Left><Width>6cm</Width><Height>2cm</Height>
                  <DataSetName>Vendas</DataSetName>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>4cm</Width></TablixColumn><TablixColumn><Width>2cm</Width></TablixColumn></TablixColumns>
                    <TablixRows>
                      <TablixRow><TablixCells>
                        <TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>Produto</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>Total</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                      <TablixRow><TablixCells>
                        <TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Produto.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox><Style><Format>C</Format></Style><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Total.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                    </TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember /><TablixMember /></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers><TablixMember /><TablixMember><Group Name="Details" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);

        // A lone flat Tablix decomposes into a repeating column-header band (PageHeader) + a paginating
        // DetailBand with one TextBox per column — a native banded table, not a monolithic TablixElement.
        def.ReportHeader.Should().BeNull("the body's only item became the bands");
        var headers = def.PageHeader!.Elements.OfType<Reporting.Elements.LabelElement>().ToList();
        headers.Select(h => h.Text).Should().Equal("Produto", "Total");

        var details = def.Detail.Elements.OfType<Reporting.Elements.TextBoxElement>().ToList();
        details.Select(d => d.Expression).Should().Equal("Fields.Produto", "Fields.Total");
        details[1].Style.Format.Should().Be("C", "the detail cell keeps its RDL Format");
        def.Detail.DataSetName.Should().Be("Vendas", "the band is bound to the Tablix's dataset (#112)");

        // Columns are placed at absolute X by the RDL widths (4cm then 2cm).
        details[0].Bounds.Width.ToCm().Should().BeApproximately(4, 0.05);
        details[1].Bounds.X.ToCm().Should().BeApproximately(4, 0.05);
        def.Metadata.ContainsKey("ImportWarnings").Should().BeFalse("a clean flat table imports fully");
    }

    // dynamic group member on each axis; the suffix optionally adds the static total member (<Group/>).
    private const string DynRow = "<TablixMember><Group Name=\"g\"><GroupExpressions><GroupExpression>=Fields!Cat.Value</GroupExpression></GroupExpressions></Group></TablixMember>";
    private const string DynCol = "<TablixMember><Group Name=\"c\"><GroupExpressions><GroupExpression>=Fields!Mes.Value</GroupExpression></GroupExpressions></Group></TablixMember>";
    private const string TotalMember = "<TablixMember><Group /></TablixMember>"; // empty group = SSRS total
    private const string DetailsMember = "<TablixMember><Group Name=\"Details\" /></TablixMember>"; // named static = detail, NOT a total

    private static string MatrixRdl(string rowMembers, string colMembers) => $"""
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body><Height>4cm</Height><ReportItems>
            <Tablix Name="M"><Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>3cm</Height>
              <TablixBody><TablixColumns><TablixColumn><Width>8cm</Width></TablixColumn></TablixColumns>
                <TablixRows><TablixRow><TablixCells><TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Sum(Fields!V.Value)</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell></TablixCells></TablixRow></TablixRows>
              </TablixBody>
              <TablixColumnHierarchy><TablixMembers>{colMembers}</TablixMembers></TablixColumnHierarchy>
              <TablixRowHierarchy><TablixMembers>{rowMembers}</TablixMembers></TablixRowHierarchy>
            </Tablix>
          </ReportItems></Body>
        </Report>
        """;

    private static Reporting.Elements.TablixElement ImportMatrix(string rowMembers, string colMembers)
        => new RdlImporter().ImportXml(MatrixRdl(rowMembers, colMembers))
            .ReportHeader!.Elements.OfType<Reporting.Elements.TablixElement>().Single();

    [Fact]
    public void Tablix_matrix_row_total_member_sets_RowSubtotals()
    {
        var t = ImportMatrix(DynRow + TotalMember, DynCol);
        t.RowSubtotals.Should().BeTrue("a static <Group/> sibling of the dynamic row group is the total");
        t.ColumnSubtotals.Should().BeFalse("the column axis has only a dynamic group");
    }

    [Fact]
    public void Tablix_matrix_column_total_member_sets_ColumnSubtotals()
    {
        var t = ImportMatrix(DynRow, DynCol + TotalMember);
        t.ColumnSubtotals.Should().BeTrue();
        t.RowSubtotals.Should().BeFalse();
    }

    [Fact]
    public void Tablix_matrix_without_total_members_has_no_subtotals()
    {
        var t = ImportMatrix(DynRow, DynCol);
        t.RowSubtotals.Should().BeFalse();
        t.ColumnSubtotals.Should().BeFalse();
    }

    [Fact]
    public void Tablix_matrix_with_named_details_member_is_not_a_subtotal()
    {
        // A named static group (Details/leaf) sibling of a dynamic group must NOT be mistaken for a total.
        var t = ImportMatrix(DynRow + DetailsMember, DynCol);
        t.RowSubtotals.Should().BeFalse("a named static group is detail rows, not a total");
    }

    [Fact]
    public void Tablix_cell_ColSpan_is_imported_into_the_band()
    {
        // Header row: a merged cell (ColSpan=2) over two detail columns; detail row: two normal cells.
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="T"><Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>2cm</Height>
                  <DataSetName>D</DataSetName>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>4cm</Width></TablixColumn><TablixColumn><Width>4cm</Width></TablixColumn></TablixColumns>
                    <TablixRows>
                      <TablixRow><TablixCells>
                        <TablixCell><ColSpan>2</ColSpan><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>Resumo</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                      <TablixRow><TablixCells>
                        <TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!A.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!B.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                    </TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember /><TablixMember /></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers><TablixMember /><TablixMember><Group Name="Details" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        // Lone flat Tablix → decomposes to bands; the merged header Label spans both columns (~8cm), wider
        // than a single detail TextBox (~4cm).
        var header = def.PageHeader!.Elements.OfType<Reporting.Elements.LabelElement>().Single();
        header.Text.Should().Be("Resumo");
        header.Bounds.Width.ToCm().Should().BeApproximately(8, 0.1);
        def.Detail.Elements.OfType<Reporting.Elements.TextBoxElement>().First().Bounds.Width.ToCm().Should().BeApproximately(4, 0.1);
    }

    [Fact]
    public void Tablix_flat_with_another_body_item_stays_a_TablixElement()
    {
        // Guard: more than one Body data region → keep the existing single-block TablixElement path.
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Textbox Name="titulo"><Top>0cm</Top><Left>0cm</Left><Width>6cm</Width><Height>1cm</Height><Paragraphs><Paragraph><TextRuns><TextRun><Value>Relatório</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox>
                <Tablix Name="T"><Top>1cm</Top><Left>0cm</Left><Width>6cm</Width><Height>2cm</Height>
                  <DataSetName>Vendas</DataSetName>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>6cm</Width></TablixColumn></TablixColumns>
                    <TablixRows><TablixRow><TablixCells><TablixCell><CellContents><Textbox><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!Produto.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell></TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember /></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="Details" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        def.ReportHeader!.Elements.OfType<Reporting.Elements.TablixElement>().Should().ContainSingle("two body items → TablixElement path");
        def.Detail.Elements.Should().BeEmpty("the detail band stays empty in the TablixElement path");
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
                  <Visibility><Hidden>=Fields!Oculto.Value</Hidden></Visibility>
                  <TablixBody><TablixRows><TablixRow><TablixCells><TablixCell><CellContents>
                    <Textbox><Style><Format>C</Format></Style><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Sum(Fields!Total.Value)</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox>
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
        var body = (Reporting.Elements.TextBoxElement)t.Cells.Single(c => c.RowIndex == 1 && c.ColumnIndex == 1).Content!;
        body.Expression.Should().Be("Sum(Fields.Total)");
        body.Style.Format.Should().Be("C", "the body cell keeps its RDL Format instead of the N2 default");
        // Common props on the Tablix itself are applied (the ApplyCommon TablixElement arm).
        t.VisibleExpression.Should().Be("!(Fields.Oculto)");
        def.Metadata.ContainsKey("ImportWarnings").Should().BeFalse("a clean matrix has nothing to warn about");
    }

    [Fact]
    public void Tablix_flat_table_with_no_body_cells_warns_empty()
    {
        // Details row member but no <TablixBody> → flat-table branch yields zero cells → "vazia" warning.
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="Vazia">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>2cm</Height>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="Det" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        def.Metadata["ImportWarnings"].Should().Contain("vazia");
    }

    [Fact]
    public void Tablix_hybrid_table_matrix_records_a_warning()
    {
        // One dynamic axis (row group) + a static other axis → table+matrix hybrid is a follow-up → warns.
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><Height>3cm</Height><ReportItems>
                <Tablix Name="Hibrido">
                  <Top>0cm</Top><Left>0cm</Left><Width>8cm</Width><Height>2cm</Height>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="G"><GroupExpressions><GroupExpression>=Fields!Regiao.Value</GroupExpression></GroupExpressions></Group></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        def.Metadata["ImportWarnings"].Should().Contain("híbrido");
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
        // Query maps to the designer's live convention (_sql / param:@x), so it opens in the designer.
        ds.Parameters["_sql"].Should().Be("SELECT * FROM Vendas");
        // @Ano bound to report parameter Ano → encoded "Ano|" (reportParam|literal).
        ds.Parameters["param:@Ano"].Should().Be("Ano|");
    }

    [Fact]
    public void Query_parameter_with_a_dynamic_expression_is_frozen_as_literal_with_a_warning()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <DataSets><DataSet Name="DS"><Query>
                <CommandText>SELECT 1</CommandText>
                <QueryParameters><QueryParameter Name="@Hoje"><Value>=Today()</Value></QueryParameter></QueryParameters>
              </Query></DataSet></DataSets>
              <Body><Height>1cm</Height><ReportItems /></Body>
            </Report>
            """;
        var def = new RdlImporter().ImportXml(rdl);
        // Dynamic value → literal slot (best-effort) + warning (never silent).
        def.DataSources.Single().Parameters["param:@Hoje"].Should().Be("|Today()");
        def.Metadata["ImportWarnings"].Should().Contain("@Hoje");
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
