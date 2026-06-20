using System.Reflection;
using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The element toolbox is discovered by reflection from the <see cref="ToolboxElementAttribute"/> on
/// <see cref="DesignerElementKind"/> — so adding a new annotated kind registers it with no markup edits.
/// These lock that wiring (and the invariants the component tests rely on, e.g. exactly 8 draggable basics).
/// </summary>
public class ToolboxCatalogTests
{
    [Fact]
    public void Every_annotated_kind_is_discovered_exactly_once()
    {
        var annotated = typeof(DesignerElementKind)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Count(f => f.GetCustomAttribute<ToolboxElementAttribute>() is not null);

        var items = ToolboxCatalog.Groups.SelectMany(g => g.Items).ToList();

        items.Select(i => i.Kind).Should().OnlyHaveUniqueItems();
        items.Should().HaveCount(annotated, "the catalog is built purely from the attribute annotations");
        items.Should().Contain(i => i.Kind == DesignerElementKind.Gauge && i.Label == "Gauge" && i.Icon == "gauge");

        // Every enum value must be annotated, else a new kind silently vanishes from the toolbox.
        annotated.Should().Be(Enum.GetValues<DesignerElementKind>().Length,
            "every DesignerElementKind needs [ToolboxElement] or it won't appear in the palette");
    }

    [Fact]
    public void Groups_follow_the_canonical_order_and_basics_are_draggable()
    {
        ToolboxCatalog.Groups.Select(g => g.Name)
            .Should().ContainInOrder("Básicos", "Dados", "Gráficos", "Avançados");

        var basics = ToolboxCatalog.Groups.Single(g => g.Name == "Básicos");
        basics.Items.Should().OnlyContain(i => i.Draggable, "the basic shapes are the draggable ones");
        basics.Items.Should().HaveCount(8);
        basics.Items[0].Kind.Should().Be(DesignerElementKind.Label, "component tests address the basic group by index");
        basics.Items[1].Kind.Should().Be(DesignerElementKind.TextBox);

        // Non-basic groups are click-to-add, not draggable.
        ToolboxCatalog.Groups.Where(g => g.Name != "Básicos")
            .SelectMany(g => g.Items)
            .Should().OnlyContain(i => !i.Draggable);
    }
}
