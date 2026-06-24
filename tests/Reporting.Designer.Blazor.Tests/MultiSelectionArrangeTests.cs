using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The multi-selection panel's "Organizar" section wires the <see cref="Arrange"/> geometry to buttons.
/// Asserts the element view-models' positions AFTER a synchronous click (model state, not a DOM re-query),
/// so it is not subject to bUnit re-render timing.
/// </summary>
public class MultiSelectionArrangeTests : Bunit.BunitContext
{
    public MultiSelectionArrangeTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static ElementViewModel El(int x, int y, int w, int h) => ElementViewModel.FromElement(new LabelElement
    {
        Id = $"l{x}_{y}",
        Bounds = new Rectangle(new Unit(x), new Unit(y), new Unit(w), new Unit(h)),
        Text = "x",
    });

    [Fact]
    public void Clicking_align_left_moves_every_element_to_the_min_left()
    {
        var sel = new[] { El(100, 0, 50, 10), El(40, 20, 30, 10), El(200, 40, 20, 10) };
        var cut = Render<MultiSelectionPanel>(p => p.Add(x => x.Selection, sel));

        cut.FindAll("button[aria-label='Alinhar à esquerda']")[0].Click();

        sel.Select(e => e.X.Mils).Should().AllBeEquivalentTo(40, "all left edges snap to the leftmost");
        sel.Select(e => e.Y.Mils).Should().Equal(0, 20, 40); // Y untouched
    }

    [Fact]
    public void Clicking_align_bottom_aligns_bottom_edges()
    {
        var sel = new[] { El(0, 0, 10, 10), El(0, 50, 10, 30) }; // bottoms 10, 80 → 80
        var cut = Render<MultiSelectionPanel>(p => p.Add(x => x.Selection, sel));

        cut.FindAll("button[aria-label='Alinhar à base']")[0].Click();

        (sel[0].Y.Mils + sel[0].Height.Mils).Should().Be(80);
        (sel[1].Y.Mils + sel[1].Height.Mils).Should().Be(80);
    }

    [Fact]
    public void Distribute_appears_only_with_three_plus_and_evens_the_spacing()
    {
        var two = new[] { El(0, 0, 10, 10), El(50, 0, 10, 10) };
        Render<MultiSelectionPanel>(p => p.Add(x => x.Selection, two))
            .FindAll("button[aria-label='Distribuir horizontalmente']").Should().BeEmpty("distribute needs 3+");

        var three = new[] { El(0, 0, 10, 10), El(30, 0, 10, 10), El(100, 0, 10, 10) };
        var cut = Render<MultiSelectionPanel>(p => p.Add(x => x.Selection, three));
        var btn = cut.FindAll("button[aria-label='Distribuir horizontalmente']");
        btn.Should().HaveCount(1);
        btn[0].Click();

        three.Select(e => e.X.Mils).OrderBy(v => v).Should().Equal(0, 50, 100);
    }
}
