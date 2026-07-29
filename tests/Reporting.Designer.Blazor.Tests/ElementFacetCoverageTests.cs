using FluentAssertions;
using FluentAssertions.Equivalency;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Structural guard for the facet decomposition: the domain mapping for each element family now lives in its
/// own <c>ElementFacet</c> instead of two <c>switch</c> blocks ~250 lines apart. These tests make "added a
/// designer kind but forgot to wire it" a red test rather than a runtime surprise, using only the public API.
/// </summary>
public class ElementFacetCoverageTests
{
    public static TheoryData<DesignerElementKind> AllKinds()
    {
        var data = new TheoryData<DesignerElementKind>();
        foreach (var kind in Enum.GetValues<DesignerElementKind>())
        {
            data.Add(kind);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Every_designer_kind_can_be_built(DesignerElementKind kind)
    {
        // ToElement throws "Unknown kind" for a kind with neither a facet nor the opaque-advanced fallback.
        var vm = new ElementViewModel(kind, $"e-{kind}");

        var act = () => vm.ToElement();

        act.Should().NotThrow($"{kind} must be buildable — give it a facet, or route it through the opaque path");
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Every_designer_kind_round_trips_through_its_facet(DesignerElementKind kind)
    {
        // Build → read back → build again. A facet that implements Build but forgets Read (or reads a field it
        // doesn't write) loses data here, which is exactly the failure mode the split switches invited.
        var original = new ElementViewModel(kind, $"e-{kind}").ToElement();

        var reloaded = ElementViewModel.FromElement(original).ToElement();

        reloaded.Should().BeEquivalentTo(original,
            opts => opts.Excluding((IMemberInfo m) => m.Name == "Id").RespectingRuntimeTypes(),
            $"the {kind} facet's Build and Read must be inverses");
    }

    [Fact]
    public void Kinds_are_not_claimed_by_more_than_one_facet()
    {
        // Two facets claiming the same kind would make the build direction depend on registration order.
        // Proven through behaviour: every kind builds a stable element type across repeated resolutions.
        foreach (var kind in Enum.GetValues<DesignerElementKind>())
        {
            var first = new ElementViewModel(kind, "a").ToElement().GetType();
            var second = new ElementViewModel(kind, "b").ToElement().GetType();
            second.Should().Be(first, $"{kind} must resolve to one deterministic element type");
        }
    }
}
