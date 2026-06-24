using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Parameters;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Round-trip of a cascading (dependent) parameter — <c>ParameterAvailableValues.FilterField</c> + <c>DependsOn</c>
/// — across .repx and .repjson with full structural equality.
/// </summary>
public class CascadingParameterRoundTripTests
{
    private static ReportDefinition ReportWithCascadingParam()
    {
        var cidade = new ReportParameter("Cidade", typeof(string), "Cidade", Required: false,
            AvailableValues: ParameterAvailableValues.FromCascadingQuery("Cidades", "Nome", "Estado", "Estado", "Nome"));
        var detail = new DetailBand(Unit.FromMm(6), EquatableArray.Create<ReportElement>(new TextBoxElement
        {
            Expression = "{Fields.X}",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(6)),
        }));
        return new ReportDefinition("Cascading", Paper.PageSetup.A4Portrait, detail)
        {
            Parameters = EquatableArray.Create(cidade),
        };
    }

    [Fact]
    public void Repx_round_trips_the_cascade()
    {
        var def = ReportWithCascadingParam();
        var s = new RepxSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void Repjson_round_trips_the_cascade()
    {
        var def = ReportWithCascadingParam();
        var s = new RepJsonSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def);
    }

    [Fact]
    public void The_loaded_parameter_keeps_filter_field_and_depends_on()
    {
        var s = new RepxSerializer();
        var back = s.LoadFromBytes(s.SaveToBytes(ReportWithCascadingParam()));
        var av = back.Parameters[0].AvailableValues!;
        av.FilterField.Should().Be("Estado");
        av.DependsOn.Should().Be("Estado");
        av.IsCascading.Should().BeTrue();
    }
}
