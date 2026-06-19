using Bunit;
using FluentAssertions;
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
    public void Fx_toggle_reveals_the_expression_editor()
    {
        var cut = Render<PropertyGridMetaSection>(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Rectangle, "r1")));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "fx").Click();

        cut.Markup.Should().Contain("= expressão", "the fx toggle swaps the static editor for an expression input");
    }

    [Fact]
    public void Fx_open_state_does_not_leak_when_the_selected_element_changes()
    {
        var cut = Render<PropertyGridMetaSection>(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Rectangle, "a")));
        cut.FindAll("button").First(b => b.TextContent.Trim() == "fx").Click(); // open fx for a property of A
        cut.Markup.Should().Contain("= expressão");

        // Select a DIFFERENT element of the same kind (same property paths) — the open-fx marker must not carry over.
        cut.Render(p =>
            p.Add(x => x.Element, new ElementViewModel(DesignerElementKind.Rectangle, "b")));

        cut.Markup.Should().NotContain("= expressão", "the fx-open marker belongs to the previous element, not the new one");
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
}
