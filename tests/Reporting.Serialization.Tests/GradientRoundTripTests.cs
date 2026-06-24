using System.Text;
using FluentAssertions;
using Reporting;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Serialization;
using Reporting.Styling;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Round-trip of the two-colour background gradient (Style.BackColor → Style.BackColorEnd along
/// Style.BackgroundGradient) across all three serializers: .repx and .repjson (full structural equality)
/// and .rdl (BackgroundColor / BackgroundGradientType / BackgroundGradientEndColor, both directions).
/// </summary>
public class GradientRoundTripTests
{
    private static readonly Color Start = Color.FromRgb(0xC2, 0x41, 0x0E);
    private static readonly Color End = Color.FromRgb(0x1E, 0x3A, 0x8A);

    private static ReportDefinition ReportWith(Style style)
    {
        var detail = new Bands.DetailBand(Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Expression = "{Fields.Total:C}",
                Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(8)),
                Style = style,
            }));
        return new ReportDefinition("Gradient test", Paper.PageSetup.A4Portrait, detail);
    }

    private static Style Gradient(BackgroundGradientType kind)
        => new(BackColor: Start, BackColorEnd: End, BackgroundGradient: kind);

    [Theory]
    [InlineData(BackgroundGradientType.TopBottom)]
    [InlineData(BackgroundGradientType.LeftRight)]
    [InlineData(BackgroundGradientType.Center)]
    [InlineData(BackgroundGradientType.DiagonalLeft)]
    [InlineData(BackgroundGradientType.DiagonalRight)]
    public void Repx_round_trips_the_gradient(BackgroundGradientType kind)
    {
        var def = ReportWith(Gradient(kind));
        var s = new RepxSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Theory]
    [InlineData(BackgroundGradientType.TopBottom)]
    [InlineData(BackgroundGradientType.DiagonalLeft)]
    public void Repjson_round_trips_the_gradient(BackgroundGradientType kind)
    {
        var def = ReportWith(Gradient(kind));
        var s = new RepJsonSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void Rdl_export_writes_the_gradient_elements()
    {
        var def = ReportWith(Gradient(BackgroundGradientType.TopBottom));
        var xml = Encoding.UTF8.GetString(new RdlExporter().SaveToBytes(def));
        xml.Should().Contain("BackgroundGradientType").And.Contain("TopBottom");
        xml.Should().Contain("BackgroundGradientEndColor");
    }

    [Fact]
    public void Rdl_imports_a_background_gradient()
    {
        const string rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="t1">
                  <Style>
                    <BackgroundColor>#C2410E</BackgroundColor>
                    <BackgroundGradientType>TopBottom</BackgroundGradientType>
                    <BackgroundGradientEndColor>#1E3A8A</BackgroundGradientEndColor>
                  </Style>
                </Textbox>
              </ReportItems></Body>
            </Report>
            """;
        var style = new RdlImporter().ImportXml(rdl).ReportHeader!.Elements.Single().Style;
        style.BackgroundGradient.Should().Be(BackgroundGradientType.TopBottom);
        style.BackColor.Should().Be(Start);
        style.BackColorEnd.Should().Be(End);
    }

    [Fact]
    public void A_solid_style_keeps_gradient_none_and_no_end_color()
    {
        var def = ReportWith(new Style(BackColor: Start));
        var s = new RepxSerializer();
        var back = s.LoadFromBytes(s.SaveToBytes(def));
        var style = back.Detail.Elements[0].Style;
        style.BackgroundGradient.Should().Be(BackgroundGradientType.None);
        style.BackColorEnd.Should().BeNull();
        back.Should().Be(def, "a solid fill round-trips unchanged — no gradient noise");
    }
}
