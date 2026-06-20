using Bunit;
using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The generic list editor renders each <c>EquatableArray&lt;T&gt;</c> item from its own
/// <c>[PropertyGrid]</c> fields (recursive metadata) and supports add/remove — so a list-of-records
/// property gets a full editor with no hand-coded list UI.
/// </summary>
public class PropertyGridListEditorTests : Bunit.BunitContext
{
    public PropertyGridListEditorTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static (ElementViewModel Vm, PropertyGridDescriptor Desc) GaugeWithTwoRanges()
    {
        var vm = ElementViewModel.FromElement(new GaugeElement
        {
            Id = "g1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(40)),
            Ranges = new EquatableArray<GaugeRange>(new[]
            {
                new GaugeRange("0", "60", "#22c55e"),
                new GaugeRange("60", "100", "#dc2626"),
            }),
        });
        var desc = PropertyGridDescriptors.For(typeof(GaugeElement)).Single(d => d.Name == "Ranges");
        return (vm, desc);
    }

    [Fact]
    public void Renders_one_row_per_item_with_its_field_editors_and_an_add_button()
    {
        var (vm, desc) = GaugeWithTwoRanges();

        var cut = Render<PropertyGridListEditor>(p => p
            .Add(x => x.Element, vm)
            .Add(x => x.Descriptor, desc));

        cut.Markup.Should().Contain("Adicionar");
        cut.FindAll("input[type=color]").Count.Should().Be(2, "one colour field rendered per GaugeRange item");
    }

    [Fact]
    public void Add_appends_a_default_item_and_remove_drops_one()
    {
        var (vm, desc) = GaugeWithTwoRanges();
        var cut = Render<PropertyGridListEditor>(p => p
            .Add(x => x.Element, vm)
            .Add(x => x.Descriptor, desc));

        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar")).Click();
        ((GaugeElement)vm.ToElement()).Ranges.Count.Should().Be(3, "add appends a fresh GaugeRange");

        cut.FindAll("button").First(b => b.TextContent.Trim() == "×").Click();
        ((GaugeElement)vm.ToElement()).Ranges.Count.Should().Be(2, "remove drops the first item");
    }

    [Fact]
    public void Editing_an_item_field_updates_the_immutable_array()
    {
        var (vm, desc) = GaugeWithTwoRanges();
        var cut = Render<PropertyGridListEditor>(p => p
            .Add(x => x.Element, vm)
            .Add(x => x.Descriptor, desc));

        // the first text input is the first range's StartExpression
        cut.FindAll("input[type=text]").First().Change("10");

        ((GaugeElement)vm.ToElement()).Ranges[0].StartExpression.Should().Be("10");
    }

    [Fact]
    public void Removing_the_first_item_keeps_the_seconds_data_intact()
    {
        // With stable @key rows, removing the first row leaves the SECOND range untouched — not re-bound by
        // shifted position. Distinct start values (0 vs 60) make any mix-up visible.
        var (vm, desc) = GaugeWithTwoRanges();
        var cut = Render<PropertyGridListEditor>(p => p
            .Add(x => x.Element, vm)
            .Add(x => x.Descriptor, desc));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "×").Click();

        var ranges = ((GaugeElement)vm.ToElement()).Ranges;
        ranges.Should().ContainSingle();
        ranges[0].StartExpression.Should().Be("60", "the survivor is the original second range, intact");
    }

    [Fact]
    public void A_translucent_colour_hex_renders_as_rgb_not_black()
    {
        // A loaded #AARRGGBB must show as #RRGGBB in <input type=color> (which can't take 8 digits), not
        // reset to #000000. The alpha-safe handling now lives in the shared ColorHexUtil, used by both editors.
        var vm = ElementViewModel.FromElement(new GaugeElement
        {
            Id = "g1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(40)),
            Ranges = new EquatableArray<GaugeRange>(new[] { new GaugeRange("0", "100", "#80FF0000") }),
        });
        var desc = PropertyGridDescriptors.For(typeof(GaugeElement)).Single(d => d.Name == "Ranges");
        var cut = Render<PropertyGridListEditor>(p => p.Add(x => x.Element, vm).Add(x => x.Descriptor, desc));

        cut.FindAll("input[type=color]")[0].GetAttribute("value")
            .Should().Be("#FF0000", "the 8-digit alpha hex is RGB-stripped for the colour input, not reset to black");
    }

    [Fact]
    public void Clearing_a_nullable_series_colour_reverts_it_to_null_for_the_auto_palette()
    {
        // ChartSeries.Color is Color? — null = "use the auto palette". The list editor's color-picker must
        // offer a clear (×) back to null, matching the metadata section (parity via the shared IsNullable).
        var vm = ElementViewModel.FromElement(new ChartElement
        {
            Id = "c1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(55)),
            Series = new EquatableArray<ChartSeries>(new[] { new ChartSeries("S", "Fields.cat", "Fields.val", Color.Red) }),
        });
        var desc = PropertyGridDescriptors.For(typeof(ChartElement)).Single(d => d.Name == "Series");
        var cut = Render<PropertyGridListEditor>(p => p.Add(x => x.Element, vm).Add(x => x.Descriptor, desc));

        cut.FindAll("button").First(b => (b.GetAttribute("title") ?? "").Contains("Limpar")).Click();

        ((ChartElement)vm.ToElement()).Series[0].Color.Should().BeNull("clearing reverts the series to the auto palette");
    }
}
