using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The Tablix pagination properties (<c>MinColumnWidth</c> / <c>RepeatColumnHeaders</c> / <c>KeepTogether</c>)
/// surface in the metadata-driven "Paginação" section of the PropertyGrid (via their <c>[PropertyGrid]</c>
/// annotations) and round-trip through the opaque Tablix model.
/// </summary>
public class TablixPaginationPropertyGridTests : Bunit.BunitContext
{
    public TablixPaginationPropertyGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Pagination_props_render_in_the_meta_section_for_a_tablix()
    {
        var vm = new ElementViewModel(DesignerElementKind.Tablix, "t1");

        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, vm));

        cut.Markup.Should().Contain("Paginação", "the metadata category header");
        cut.Markup.Should().Contain("Largura mín. de coluna", "the MinColumnWidth editor row");
        cut.Markup.Should().Contain("Repetir cabeçalho de coluna", "the RepeatColumnHeaders editor row");
        cut.Markup.Should().Contain("Manter junto", "the KeepTogether editor row");
    }

    [Fact]
    public void Setting_MinColumnWidth_via_the_meta_section_round_trips_to_the_model()
    {
        var vm = new ElementViewModel(DesignerElementKind.Tablix, "t1");
        var descriptor = PropertyGridDescriptors.For(typeof(TablixElement))
            .First(d => d.Name == nameof(TablixElement.MinColumnWidth));

        vm.ApplyMetaSet(descriptor, Unit.FromMm(25));

        ((TablixElement)vm.ToElement()).MinColumnWidth
            .Should().Be(Unit.FromMm(25), "the metadata edit persists onto the opaque Tablix model");
    }
}
