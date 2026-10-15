using System.Text;
using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Drill-down survives an RDL round-trip: <see cref="ReportElement.ToggleItemId"/> becomes
/// <c>&lt;Visibility&gt;&lt;ToggleItem&gt;</c>, and under a toggle RDL's <c>&lt;Hidden&gt;</c> carries only the initial
/// state (<see cref="ReportElement.InitiallyHidden"/>). The import side was fixed in #217; these pin the export side.
/// </summary>
public class RdlToggleRoundTripTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SaveToBytes_ElementoComToggleItem_PreservaVinculoEEstadoInicial(bool initiallyHidden)
    {
        var rdl = new RdlExporter();

        var element = RoundTrip(rdl, new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            ToggleItemId = "Cabecalho",
            InitiallyHidden = initiallyHidden,
        }, out var xml);

        xml.Should().Contain("<ToggleItem>Cabecalho</ToggleItem>");
        element.ToggleItemId.Should().Be("Cabecalho");
        element.InitiallyHidden.Should().Be(initiallyHidden);
        element.Visible.Should().BeTrue("starting collapsed is not the same as hidden for good");
    }

    [Fact]
    public void SaveToBytes_ElementoComToggleEOcultoPermanente_PreservaVisibleViaCustomProperty()
    {
        var rdl = new RdlExporter();

        var element = RoundTrip(rdl, new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            ToggleItemId = "Cabecalho",
            Visible = false,
        }, out var xml);

        xml.Should().Contain("<Name>omni:Visible</Name>");
        element.Visible.Should().BeFalse();
        element.InitiallyHidden.Should().BeFalse("Hidden under a toggle carries only the initial state");
        element.ToggleItemId.Should().Be("Cabecalho");
    }

    [Fact]
    public void SaveToBytes_ElementoOcultoSemToggle_ContinuaComoHiddenSemCustomProperty()
    {
        var rdl = new RdlExporter();

        var element = RoundTrip(rdl, new TextBoxElement
        {
            Expression = "Fields.Detalhe",
            Bounds = new Rectangle(Unit.Zero, Unit.FromMm(8), Unit.FromMm(60), Unit.FromMm(6)),
            Visible = false,
        }, out var xml);

        xml.Should().NotContain("omni:Visible").And.NotContain("<ToggleItem>");
        element.Visible.Should().BeFalse();
    }

    private static TextBoxElement RoundTrip(RdlExporter rdl, TextBoxElement element, out string xml)
    {
        var definition = new ReportDefinition("Drill", PageSetup.A4Portrait, DetailBand.Empty)
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(20),
                new EquatableArray<ReportElement>(new ReportElement[] { element })),
        };
        var bytes = rdl.SaveToBytes(definition);
        xml = Encoding.UTF8.GetString(bytes);
        return rdl.LoadFromBytes(bytes).ReportHeader!.Elements.OfType<TextBoxElement>().Single();
    }
}
