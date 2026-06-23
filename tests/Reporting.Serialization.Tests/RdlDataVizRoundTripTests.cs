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
/// Data-viz export (Chart/Gauge/Subreport → <c>&lt;Chart&gt;</c>/<c>&lt;GaugePanel&gt;</c>/<c>&lt;Subreport&gt;</c>)
/// — the inverses of the importer's (flattened) readers, so an imported <c>.rdl</c> carrying these round-trips
/// by value. Each writer re-emits exactly the subset the importer reads; model fields with no RDL counterpart
/// are warned (never silently dropped).
/// </summary>
public class RdlDataVizRoundTripTests
{
    private static string Wrap(string item) => $"""
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
          <Body><Height>80mm</Height><ReportItems>
            {item}
          </ReportItems></Body>
          <Width>190mm</Width>
          <Page><PageHeight>297mm</PageHeight><PageWidth>210mm</PageWidth>
            <LeftMargin>10mm</LeftMargin><RightMargin>10mm</RightMargin><TopMargin>10mm</TopMargin><BottomMargin>10mm</BottomMargin></Page>
        </Report>
        """;

    private const string ChartItem = """
        <Chart Name="Grafico">
          <ChartData>
            <ChartSeriesCollection>
              <ChartSeries Name="Vendas">
                <Type>Line</Type>
                <DataPoints><DataPoint><DataValues><DataValue><Value>=Sum(Fields!Total.Value)</Value></DataValue></DataValues></DataPoint></DataPoints>
              </ChartSeries>
            </ChartSeriesCollection>
          </ChartData>
          <ChartCategoryHierarchy><ChartMembers><ChartMember><Group><GroupExpressions>
            <GroupExpression>=Fields!Mes.Value</GroupExpression>
          </GroupExpressions></Group></ChartMember></ChartMembers></ChartCategoryHierarchy>
          <Top>5mm</Top><Left>5mm</Left><Width>100mm</Width><Height>60mm</Height>
        </Chart>
        """;

    [Fact]
    public void Chart_round_trips_value_equal()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(Wrap(ChartItem)));

        var chart = imported.ReportHeader!.Elements.OfType<ChartElement>().Should().ContainSingle().Subject;
        chart.Kind.Should().Be(ChartKind.Line);
        var series = chart.Series.Should().ContainSingle().Subject;
        series.ValueExpression.Should().Be("Sum(Fields.Total)");
        series.CategoryExpression.Should().Be("Fields.Mes");

        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(imported));
        xml.Should().Contain("<ChartData>").And.Contain("<Type>Line</Type>")
           .And.Contain("=Sum(Fields!Total.Value)").And.Contain("<GroupExpression>=Fields!Mes.Value</GroupExpression>");

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));
        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes());
    }

    private const string GaugeItem = """
        <GaugePanel Name="Medidor">
          <GaugePanelItems>
            <RadialGauge Name="RG1">
              <GaugeScales>
                <RadialScale>
                  <Maximum><Value>100</Value></Maximum>
                  <Minimum><Value>0</Value></Minimum>
                  <GaugePointers><RadialPointer><Value>=Fields!Velocidade.Value</Value></RadialPointer></GaugePointers>
                  <ScaleRanges>
                    <ScaleRange><StartValue><Value>0</Value></StartValue><EndValue><Value>50</Value></EndValue><BackgroundColor>#FF0000</BackgroundColor></ScaleRange>
                    <ScaleRange><StartValue><Value>50</Value></StartValue><EndValue><Value>100</Value></EndValue><BackgroundColor>#00FF00</BackgroundColor></ScaleRange>
                  </ScaleRanges>
                </RadialScale>
              </GaugeScales>
            </RadialGauge>
          </GaugePanelItems>
          <Top>5mm</Top><Left>5mm</Left><Width>50mm</Width><Height>50mm</Height>
        </GaugePanel>
        """;

    [Fact]
    public void Gauge_round_trips_value_equal()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(Wrap(GaugeItem)));

        var gauge = imported.ReportHeader!.Elements.OfType<GaugeElement>().Should().ContainSingle().Subject;
        gauge.Kind.Should().Be(GaugeKind.Radial);
        gauge.ValueExpression.Should().Be("Fields.Velocidade");
        gauge.MinimumExpression.Should().Be("0");
        gauge.MaximumExpression.Should().Be("100");
        gauge.Ranges.Should().HaveCount(2);

        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(imported));
        xml.Should().Contain("<GaugePanel").And.Contain("<RadialGauge").And.Contain("=Fields!Velocidade.Value")
           .And.Contain("<BackgroundColor>#FF0000</BackgroundColor>");

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));
        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes());
    }

    private const string SubreportItem = """
        <Subreport Name="Sub1">
          <ReportName>DetalheVendas</ReportName>
          <Parameters>
            <Parameter Name="Ano"><Value>=Parameters!Ano.Value</Value></Parameter>
            <Parameter Name="Cliente"><Value>=Fields!ClienteId.Value</Value></Parameter>
          </Parameters>
          <Top>5mm</Top><Left>5mm</Left><Width>80mm</Width><Height>40mm</Height>
        </Subreport>
        """;

    [Fact]
    public void Subreport_round_trips_value_equal()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(Wrap(SubreportItem)));

        var sub = imported.ReportHeader!.Elements.OfType<SubreportElement>().Should().ContainSingle().Subject;
        sub.ReportId.Should().Be("DetalheVendas");
        sub.ParameterBindings.Should().ContainKey("Ano").WhoseValue.Should().Be("Parameters.Ano");
        sub.ParameterBindings.Should().ContainKey("Cliente").WhoseValue.Should().Be("Fields.ClienteId");

        var xml = Encoding.UTF8.GetString(rdl.SaveToBytes(imported));
        xml.Should().Contain("<ReportName>DetalheVendas</ReportName>")
           .And.Contain("<Parameter Name=\"Ano\">").And.Contain("=Parameters!Ano.Value");

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));
        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes());
    }

    [Fact]
    public void Model_only_fields_with_no_rdl_counterpart_are_warned()
    {
        var def = new ReportDefinition("DV", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(60),
                new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new ChartElement
                    {
                        Title = "Vendas por Mês", ShowLegend = false,
                        Series = new EquatableArray<ChartSeries>(new[] { new ChartSeries("S", "Fields.Mes", "Sum(Fields.Total)") }),
                        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(100), Unit.FromMm(60)),
                    },
                    new SubreportElement
                    {
                        ReportId = "Sub", DataExpression = "Fields.Detalhe",
                        Bounds = new Rectangle(Unit.Zero, Unit.FromMm(62), Unit.FromMm(80), Unit.FromMm(20)),
                    },
                })),
        };

        var rdl = new RdlExporter();
        using var ms = new MemoryStream();
        rdl.Save(def, ms);
        var w = string.Join("\n", rdl.Warnings);

        w.Should().Contain("Title/ShowLegend");
        // DataExpression/InlineDefinition are no longer warned — they round-trip losslessly via <CustomProperties>
        // (see RdlCustomPropertiesRoundTripTests).
        w.Should().NotContain("InlineDefinition/DataExpression");
    }

    [Fact]
    public void A_literal_subreport_parameter_with_an_ampersand_round_trips()
    {
        // "R&D" is a literal, not an expression — it must NOT be folded to Concat(R, D) (the ValueOf-on-literal
        // class). A genuine expression binding still round-trips.
        var def = new ReportDefinition("S", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(20),
                new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new SubreportElement
                    {
                        ReportId = "Det",
                        ParameterBindings = new EquatableDictionary<string, string>(
                            new Dictionary<string, string> { ["Dept"] = "R&D", ["Ano"] = "Parameters.Ano" }),
                        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(20)),
                    },
                })),
        };

        var rdl = new RdlExporter();
        var sub = rdl.LoadFromBytes(rdl.SaveToBytes(def)).ReportHeader!.Elements.OfType<SubreportElement>().Single();
        sub.ParameterBindings["Dept"].Should().Be("R&D");           // literal preserved, not Concat(R, D)
        sub.ParameterBindings["Ano"].Should().Be("Parameters.Ano"); // genuine expression still round-trips
    }

    [Fact]
    public void An_empty_series_chart_keeps_its_kind()
    {
        var def = new ReportDefinition("C", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(40),
                new EquatableArray<ReportElement>(new ReportElement[]
                {
                    new ChartElement { Kind = ChartKind.Radar, Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(40)) },
                })),
        };

        var rdl = new RdlExporter();
        var chart = rdl.LoadFromBytes(rdl.SaveToBytes(def)).ReportHeader!.Elements.OfType<ChartElement>().Single();
        chart.Kind.Should().Be(ChartKind.Radar); // placeholder <Type> preserves Kind despite 0 series
        chart.Series.Should().BeEmpty();
    }

    private const string LinearGaugeItem = """
        <GaugePanel Name="LinearMed">
          <GaugePanelItems>
            <LinearGauge Name="LG1">
              <GaugeScales><LinearScale>
                <Maximum><Value>200</Value></Maximum>
                <Minimum><Value>0</Value></Minimum>
                <GaugePointers><LinearPointer><Value>=Fields!Nivel.Value</Value></LinearPointer></GaugePointers>
              </LinearScale></GaugeScales>
            </LinearGauge>
          </GaugePanelItems>
          <Top>5mm</Top><Left>5mm</Left><Width>80mm</Width><Height>20mm</Height>
        </GaugePanel>
        """;

    [Fact]
    public void Linear_gauge_round_trips_value_equal()
    {
        var rdl = new RdlExporter();
        var imported = rdl.LoadFromBytes(Encoding.UTF8.GetBytes(Wrap(LinearGaugeItem)));
        imported.ReportHeader!.Elements.OfType<GaugeElement>().Single().Kind.Should().Be(GaugeKind.Linear);

        var reimported = rdl.LoadFromBytes(rdl.SaveToBytes(imported));
        reimported.Should().BeEquivalentTo(imported,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes());
    }
}
