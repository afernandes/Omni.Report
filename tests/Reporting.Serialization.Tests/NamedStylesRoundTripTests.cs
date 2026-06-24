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
/// Round-trip of named/reusable styles: the report-level <c>NamedStyles</c> table and an element's
/// <see cref="Style.BasedOn"/> reference (including a chained named style that itself has a BasedOn), across
/// .repx and .repjson with full structural equality — plus byte-stability for reports without named styles.
/// </summary>
public class NamedStylesRoundTripTests
{
    private static ReportDefinition ReportWithNamedStyles()
    {
        var baseStyle = new Style(
            ForeColor: Color.FromRgb(0xC2, 0x41, 0x0E),
            BackColor: Color.FromRgb(0x1E, 0x3A, 0x8A),
            HorizontalAlignment: HorizontalAlignment.Center);
        var heading = new Style(Font: new Font("Georgia", 14, FontStyle.Bold), BasedOn: "base"); // chained
        var detail = new Bands.DetailBand(Unit.FromMm(8), EquatableArray.Create<ReportElement>(new TextBoxElement
        {
            Expression = "{Fields.Total:C}",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(8)),
            Style = new Style(BasedOn: "heading"),
        }));
        return new ReportDefinition("Named test", Paper.PageSetup.A4Portrait, detail)
        {
            NamedStyles = new EquatableDictionary<string, Style>(new Dictionary<string, Style>
            {
                ["base"] = baseStyle,
                ["heading"] = heading,
            }),
        };
    }

    [Fact]
    public void Repx_round_trips_named_styles_and_based_on()
    {
        var def = ReportWithNamedStyles();
        var s = new RepxSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void Repjson_round_trips_named_styles_and_based_on()
    {
        var def = ReportWithNamedStyles();
        var s = new RepJsonSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void A_report_without_named_styles_round_trips_unchanged()
    {
        var detail = new Bands.DetailBand(Unit.FromMm(8), EquatableArray.Create<ReportElement>(new TextBoxElement
        {
            Expression = "x",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(8)),
        }));
        var def = new ReportDefinition("plain", Paper.PageSetup.A4Portrait, detail);
        var s = new RepxSerializer();
        var back = s.LoadFromBytes(s.SaveToBytes(def));
        back.NamedStyles.Count.Should().Be(0, "no spurious NamedStyles node");
        back.Should().Be(def);
    }
}
