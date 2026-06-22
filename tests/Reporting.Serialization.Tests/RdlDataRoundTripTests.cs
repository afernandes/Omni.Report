using System.Text;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// PR3 of the RDL exporter — the top-level data blocks: <c>&lt;DataSets&gt;</c> (a
/// <see cref="DataSourceDefinition"/> per DataSet: Fields/CalculatedFields/Query/SortExpressions),
/// <c>&lt;ReportParameters&gt;</c> and <c>&lt;Variables&gt;</c>. Verified by the same round-trip contract:
/// importing a <c>.rdl</c>, exporting and re-importing yields a value-equal <see cref="ReportDefinition"/>.
/// </summary>
public class RdlDataRoundTripTests
{
    private const string DataRdl = """
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"
                xmlns:rd="http://schemas.microsoft.com/SQLServer/reporting/reportdesigner">
          <DataSets>
            <DataSet Name="Pedidos">
              <Query>
                <DataSourceName>PedidosDataSource</DataSourceName>
                <CommandText>SELECT * FROM Pedidos WHERE Ano = @ano</CommandText>
                <QueryParameters>
                  <QueryParameter Name="@ano"><Value>=Parameters!Ano.Value</Value></QueryParameter>
                </QueryParameters>
              </Query>
              <Fields>
                <Field Name="Total"><DataField>Total</DataField><rd:TypeName>System.Decimal</rd:TypeName></Field>
                <Field Name="Cliente"><DataField>Cliente</DataField><rd:TypeName>System.String</rd:TypeName></Field>
                <Field Name="Imposto"><Value>=Fields!Total.Value * 0.18</Value></Field>
              </Fields>
              <SortExpressions>
                <SortExpression><Value>=Fields!Cliente.Value</Value></SortExpression>
                <SortExpression><Value>=Fields!Total.Value</Value><Direction>Descending</Direction></SortExpression>
              </SortExpressions>
            </DataSet>
          </DataSets>
          <ReportParameters>
            <ReportParameter Name="Ano">
              <DataType>Integer</DataType>
              <Prompt>Ano de referência</Prompt>
              <Nullable>true</Nullable>
              <DefaultValue><Values><Value>2024</Value></Values></DefaultValue>
              <ValidValues>
                <ParameterValues>
                  <ParameterValue><Value>2023</Value><Label>Ano 2023</Label></ParameterValue>
                  <ParameterValue><Value>2024</Value><Label>Ano 2024</Label></ParameterValue>
                </ParameterValues>
              </ValidValues>
            </ReportParameter>
          </ReportParameters>
          <Variables>
            <Variable Name="Taxa"><Value>=0.18</Value></Variable>
          </Variables>
          <Body>
            <Height>30mm</Height>
            <ReportItems>
              <Textbox Name="T">
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>Olá</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                <Top>2mm</Top><Left>2mm</Left><Height>8mm</Height><Width>40mm</Width>
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
    public void DataSets_parameters_and_variables_round_trip_value_equal()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(DataRdl));

        // Sanity: the fixture really populated the data blocks.
        imported.DataSources.Should().ContainSingle(d => d.Name == "Pedidos");
        var ds = imported.DataSources[0];
        ds.Fields.Should().HaveCount(2);            // Total, Cliente (DataFields)
        ds.CalculatedFields.Should().ContainSingle(c => c.Name == "Imposto");
        ds.SortExpressions.Should().HaveCount(2);
        ds.Parameters.Should().ContainKey("_sql");
        imported.Parameters.Should().ContainSingle(p => p.Name == "Ano");
        imported.Variables.Should().ContainSingle(v => v.Name == "Taxa");

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));

        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes(),
            "the data blocks must reconstruct exactly what the importer reads");
    }

    [Fact]
    public void Parameter_facets_survive_the_round_trip()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(DataRdl));
        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));

        var ano = reimported.Parameters.Single(p => p.Name == "Ano");
        ano.ValueType.Should().Be(typeof(int));
        ano.Prompt.Should().Be("Ano de referência");
        ano.Nullable.Should().BeTrue();
        ano.DefaultValue.Should().Be(2024);
        ano.AvailableValues.Should().NotBeNull();
        ano.AvailableValues!.Values.Should().HaveCount(2);
        ano.AvailableValues.Values[1].Label.Should().Be("Ano 2024");
    }

    [Fact]
    public void DataSet_query_and_field_types_survive_the_round_trip()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(DataRdl));
        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));

        var ds = reimported.DataSources.Single(d => d.Name == "Pedidos");
        ds.Parameters["_sql"].Should().Contain("SELECT");
        ds.Parameters.Should().ContainKey("param:@ano").WhoseValue.Should().Be("Ano|"); // binding to Parameters!Ano
        ds.Fields.Single(f => f.Name == "Total").FieldType.Should().Be(typeof(decimal));
        ds.SortExpressions.Single(s => s.Expression == "Fields.Total").Direction.Should().Be(SortDirection.Descending);
    }

    [Fact]
    public void A_code_first_data_definition_survives_export_then_import()
    {
        var def = new ReportDefinition("CodeFirst", PageSetup.A4Portrait, DetailBand.Empty)
        {
            DataSources = new EquatableArray<DataSourceDefinition>(new[]
            {
                new DataSourceDefinition("Vendas")
                {
                    Fields = new EquatableArray<DataField>(new[]
                    {
                        new DataField("Produto", typeof(string)),
                        new DataField("Qtd", typeof(int)),
                    }),
                    CalculatedFields = new EquatableArray<CalculatedField>(new[]
                    {
                        new CalculatedField("Dobro", "Fields.Qtd * 2"),
                    }),
                    Parameters = new EquatableDictionary<string, string>(
                        new Dictionary<string, string> { ["_sql"] = "SELECT * FROM Vendas" }),
                },
            }),
            Parameters = new EquatableArray<ReportParameter>(new[]
            {
                new ReportParameter("Cidade", typeof(string), Prompt: "Cidade", DefaultValue: "São Paulo"),
            }),
            Variables = new EquatableArray<ReportVariable>(new[]
            {
                new ReportVariable("Meta", "1000", VariableScope.Report),
            }),
        };

        var rdl = new RdlExporter();
        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));

        var ds = back.DataSources.Single(d => d.Name == "Vendas");
        ds.Fields.Should().Contain(f => f.Name == "Produto" && f.FieldType == typeof(string));
        ds.CalculatedFields.Should().ContainSingle(c => c.Name == "Dobro" && c.Expression == "Fields.Qtd * 2");
        ds.Parameters["_sql"].Should().Be("SELECT * FROM Vendas");
        back.Parameters.Should().ContainSingle(p => p.Name == "Cidade" && (string)p.DefaultValue! == "São Paulo");
        back.Variables.Should().ContainSingle(v => v.Name == "Meta" && v.Expression == "1000");
    }

    [Fact]
    public void Save_emits_the_data_blocks_in_xml()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(DataRdl));
        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(imported));

        xml.Should().Contain("<DataSets>").And.Contain("<DataSet Name=\"Pedidos\">");
        xml.Should().Contain("<ReportParameters>").And.Contain("<ReportParameter Name=\"Ano\">");
        xml.Should().Contain("<Variables>").And.Contain("<Variable Name=\"Taxa\">");
        xml.Should().Contain("rd:TypeName").And.Contain("System.Decimal");
        xml.Should().Contain("<DataField>Total</DataField>");
    }

    [Fact]
    public void Lossy_or_unrepresentable_aspects_are_warned_not_dropped()
    {
        var def = new ReportDefinition("Lossy", PageSetup.A4Portrait, DetailBand.Empty)
        {
            DataSources = new EquatableArray<DataSourceDefinition>(new[]
            {
                new DataSourceDefinition("DS") { FilterExpression = "Fields.Total > 0" },
            }),
            Parameters = new EquatableArray<ReportParameter>(new[]
            {
                // Required=false without Nullable or a default → not representable in RDL (derived Required=true).
                new ReportParameter("P", typeof(string), Required: false),
            }),
            Variables = new EquatableArray<ReportVariable>(new[]
            {
                new ReportVariable("RowVar", "1", VariableScope.Row), // non-Report scope: not re-read from RDL
            }),
        };

        var rdl = new RdlExporter();
        using var ms = new MemoryStream();
        rdl.Save(def, ms);

        rdl.Warnings.Should().Contain(w => w.Contains("FilterExpression"));
        rdl.Warnings.Should().Contain(w => w.Contains("Required"));
        rdl.Warnings.Should().Contain(w => w.Contains("escopo"));
    }

    [Fact]
    public void Calculated_field_result_type_round_trips_via_rd_typename()
    {
        var def = new ReportDefinition("R", PageSetup.A4Portrait, DetailBand.Empty)
        {
            DataSources = new EquatableArray<DataSourceDefinition>(new[]
            {
                new DataSourceDefinition("DS")
                {
                    CalculatedFields = new EquatableArray<CalculatedField>(new[]
                    {
                        new CalculatedField("Imposto", "Fields.Total * 0.18", typeof(decimal)),
                    }),
                },
            }),
        };

        var rdl = new RdlExporter();
        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));

        back.DataSources[0].CalculatedFields.Single(c => c.Name == "Imposto").ResultType.Should().Be(typeof(decimal));
    }

    [Fact]
    public void Query_driven_available_values_round_trip()
    {
        var def = new ReportDefinition("R", PageSetup.A4Portrait, DetailBand.Empty)
        {
            Parameters = new EquatableArray<ReportParameter>(new[]
            {
                new ReportParameter("Cidade", typeof(string),
                    AvailableValues: ParameterAvailableValues.FromQuery("Lookup", "Id", "Nome")),
            }),
        };

        var rdl = new RdlExporter();
        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));

        var av = back.Parameters.Single(p => p.Name == "Cidade").AvailableValues!;
        av.IsQuery.Should().BeTrue();
        av.DataSet.Should().Be("Lookup");
        av.ValueField.Should().Be("Id");
        av.LabelField.Should().Be("Nome");
    }

    [Fact]
    public void DateTime_default_value_round_trips_with_subsecond_precision()
    {
        var when = new DateTime(2026, 6, 18, 14, 30, 45).AddTicks(1234567);
        var def = new ReportDefinition("R", PageSetup.A4Portrait, DetailBand.Empty)
        {
            Parameters = new EquatableArray<ReportParameter>(new[]
            {
                new ReportParameter("Quando", typeof(DateTime), DefaultValue: when),
            }),
        };

        var rdl = new RdlExporter();
        var back = rdl.LoadFromBytes(rdl.SaveToBytes(def));

        back.Parameters.Single().DefaultValue.Should().Be(when); // ISO "o" preserves ticks (Convert.ToString didn't)
    }

    [Fact]
    public void Newly_covered_lossy_aspects_each_emit_a_warning()
    {
        var def = new ReportDefinition("Lossy2", PageSetup.A4Portrait, DetailBand.Empty)
        {
            DataSources = new EquatableArray<DataSourceDefinition>(new[]
            {
                new DataSourceDefinition("DS")
                {
                    DataMember = "m",
                    Relations = new EquatableArray<DataRelation>(new[] { new DataRelation("r", "P", "pid", "C", "cid") }),
                    Fields = new EquatableArray<DataField>(new[] { new DataField("Col", typeof(string), DisplayName: "Coluna") }),
                    Parameters = new EquatableDictionary<string, string>(new Dictionary<string, string>
                    {
                        ["_kind"] = "SqlServer", ["_connection"] = "Server=.", ["_sql"] = "select 1",
                    }),
                },
            }),
            Parameters = new EquatableArray<ReportParameter>(new[]
            {
                new ReportParameter("G", typeof(Guid)), // no RDL equivalent → String + warning
            }),
            Variables = new EquatableArray<ReportVariable>(new[]
            {
                new ReportVariable("V", "1", VariableScope.Report, InitialValue: 0),
            }),
        };

        var rdl = new RdlExporter();
        using var ms = new MemoryStream();
        rdl.Save(def, ms);
        var w = string.Join("\n", rdl.Warnings);

        w.Should().Contain("conexão");                 // _kind/_connection/_timeout
        w.Should().Contain("DisplayName");
        w.Should().Contain("DataMember/Relations");
        w.Should().Contain("Guid");                    // unrepresentable parameter type
        w.Should().Contain("InitialValue");
    }
}
