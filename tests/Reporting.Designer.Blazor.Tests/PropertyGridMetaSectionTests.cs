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

        cut.Markup.Should().Contain("Propriedades");
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
}
