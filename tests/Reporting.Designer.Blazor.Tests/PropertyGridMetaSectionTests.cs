using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The metadata-driven section renders an editor per <c>[PropertyGrid]</c> property (chosen by type),
/// plus an <c>fx</c> toggle for bindable ones — entirely from reflection, with no per-kind markup. So a
/// shape shows its fill/corner editors and a line its orientation editor automatically.
/// </summary>
public class PropertyGridMetaSectionTests : Bunit.BunitContext
{
    public PropertyGridMetaSectionTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Renders_metadata_editors_for_a_shape_with_fx_toggles()
    {
        var cut = Render<PropertyGridMetaSection>(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Rectangle, "r1")));

        cut.Markup.Should().Contain("Forma", "descriptors are grouped under their category header");
        cut.Markup.Should().Contain("Preenchimento");
        cut.Markup.Should().Contain("Raio do canto");
        cut.Markup.Should().Contain(">fx<", "every bindable property gets an fx toggle");
    }

    [Fact]
    public void Renders_the_line_orientation_enum_editor_from_metadata()
    {
        var cut = Render<PropertyGridMetaSection>(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Line, "l1")));

        cut.Markup.Should().Contain("Orientação");
        cut.Markup.Should().Contain("TopLeftToBottomRight", "the enum editor lists the LineDirection values");
    }

    [Fact]
    public void Fx_button_requests_the_rich_expression_editor_for_the_property_path()
    {
        string? requestedPath = null;
        var cut = Render<PropertyGridMetaSection>(p => p
            .Add(x => x.Element, new ElementViewModel(DesignerElementKind.Rectangle, "r1"))
            .Add(x => x.OnEditExpression, EventCallback.Factory.Create<string>(this, path => requestedPath = path)));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "fx").Click();

        requestedPath.Should().Be("FillColor", "fx asks the host to open the Monaco editor for that property's binding");
    }

    [Fact]
    public void A_bound_property_previews_its_expression_with_a_clear_button()
    {
        var vm = new ElementViewModel(DesignerElementKind.Rectangle, "r1");
        vm.SetPropertyExpression("FillColor", "Fields.Cor");

        var cut = Render<PropertyGridMetaSection>(p => p.Add(x => x.Element, vm));

        cut.Markup.Should().Contain("Fields.Cor", "a bound property previews its expression instead of the static editor");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "×", "and offers a clear button to drop the binding");
    }

    [Fact]
    public void Text_element_renders_rich_appearance_editors_from_the_flattened_style()
    {
        var cut = Render<PropertyGridMetaSection>(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.TextBox, "t1")));

        cut.Markup.Should().Contain("Fonte", "the flattened Style.Font row");
        cut.Markup.Should().Contain("Cor do texto", "the flattened Style.ForeColor row");
        cut.Markup.Should().Contain("Alinhamento H");
        cut.Markup.Should().Contain(">B<", "the bold toggle of the rich font editor renders (not a generic textbox)");
    }

    [Fact]
    public void Text_element_renders_border_and_padding_editors_from_metadata()
    {
        var cut = Render<PropertyGridMetaSection>(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.TextBox, "t1")));

        cut.Markup.Should().Contain("Borda");
        cut.Markup.Should().Contain("Padding");
        cut.Markup.Should().Contain("Solid", "the border-style dropdown renders (not a generic editor)");
    }

    [Fact]
    public void Text_element_renders_the_format_preset_editor_from_metadata()
    {
        var cut = Render<PropertyGridMetaSection>(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.TextBox, "t1")));

        cut.Markup.Should().Contain("Formato");
        cut.Markup.Should().Contain("Moeda", "the rich format preset dropdown renders (not a generic textbox)");
    }
}
