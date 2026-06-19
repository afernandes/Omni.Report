using System.Linq;
using FluentAssertions;
using Reporting.Bands;
using Reporting.CodeFirst;
using Reporting.Common;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Guards that a band's visibility (static <c>Visible</c> + RDL <c>&lt;Visibility&gt;</c> expression)
/// is authorable in the designer — it used to be hard-coded to <c>Visible: true / null</c> when the
/// band view-model was built, so a conditionally-hidden band could neither be created nor edited.
/// </summary>
public class DesignerBandVisibilityTests
{
    [Fact]
    public void Band_visibility_round_trips_through_the_designer()
    {
        // A valid PageSetup from a built report, with a conditionally-hidden report header injected.
        var baseDef = ReportBuilder.Create("R").Page(p => p.A4().Portrait()).Build().Definition;
        var def = baseDef with
        {
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(10),
                EquatableArray<ReportElement>.Empty,
                Visible: true, VisibleExpression: "Parameters.Detalhado = true"),
        };

        // Load → the band view-model surfaces the visibility expression for editing.
        var vm = ReportDefinitionViewModel.FromDefinition(def);
        var band = vm.Bands.First(b => b.Kind == DesignerBandKind.ReportHeader);
        band.BandVisibleExpr.Should().Be("Parameters.Detalhado = true");
        band.BandVisible.Should().BeTrue();

        // Edit both + rebuild → they persist on the definition.
        band.BandVisible = false;
        var def2 = vm.Build();
        def2.ReportHeader!.Visible.Should().BeFalse("the band's static visibility must be settable in the designer");
        def2.ReportHeader.VisibleExpression.Should().Be("Parameters.Detalhado = true");
    }
}
