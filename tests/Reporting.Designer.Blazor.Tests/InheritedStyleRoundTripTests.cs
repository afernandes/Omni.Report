using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// A null <c>Style.Font</c> / <c>Style.ForeColor</c> means "inherit from the named style or theme". The
/// editors need a concrete value to show, so the view-model materialises a placeholder — but opening and
/// saving in the Designer must NOT freeze that placeholder as a literal, or the element silently stops
/// following its named style (#181-183/#188-189).
/// </summary>
public class InheritedStyleRoundTripTests
{
    private static TextBoxElement Box(Style style) => new()
    {
        Id = "t1",
        Expression = "Fields.X",
        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(40), Unit.FromMm(6)),
        Style = style,
    };

    [Fact]
    public void An_inherited_font_and_colour_stay_null_through_an_untouched_round_trip()
    {
        var vm = ElementViewModel.FromElement(Box(new Style(BasedOn: "Corpo")));

        // The editors still show something usable…
        vm.ForeColor.Should().Be(Color.Black, "the colour editor needs a concrete placeholder");
        vm.FontFamily.Should().Be("Arial");

        // …but the model keeps saying "inherit".
        var style = vm.ToElement().Style;
        style.ForeColor.Should().BeNull("an untouched colour must stay inherited");
        style.Font.Should().BeNull("an untouched font must stay inherited");
        style.BasedOn.Should().Be("Corpo", "the named-style link survives");
    }

    [Fact]
    public void Setting_the_colour_materialises_only_the_colour()
    {
        var vm = ElementViewModel.FromElement(Box(new Style(BasedOn: "Corpo")));

        vm.ForeColor = Color.Red;

        var style = vm.ToElement().Style;
        style.ForeColor.Should().Be(Color.Red, "the author set it explicitly");
        style.Font.Should().BeNull("the font was not touched, so it keeps inheriting");
    }

    [Theory]
    [InlineData("family")]
    [InlineData("size")]
    [InlineData("bold")]
    [InlineData("italic")]
    [InlineData("underline")]
    [InlineData("strikethrough")]
    public void Touching_any_font_editor_materialises_the_font(string editor)
    {
        var vm = ElementViewModel.FromElement(Box(new Style(BasedOn: "Corpo")));

        switch (editor)
        {
            case "family": vm.FontFamily = "Verdana"; break;
            case "size": vm.FontSize = 14; break;
            case "bold": vm.IsBold = true; break;
            case "italic": vm.IsItalic = true; break;
            case "underline": vm.IsUnderline = true; break;
            case "strikethrough": vm.IsStrikethrough = true; break;
        }

        var style = vm.ToElement().Style;
        style.Font.Should().NotBeNull($"changing the {editor} editor is an explicit font choice");
        style.ForeColor.Should().BeNull("the colour was not touched");
    }

    [Fact]
    public void An_explicit_font_and_colour_survive_unchanged()
    {
        var explicitStyle = new Style(Font: new Font("Verdana", 14, FontStyle.Bold), ForeColor: Color.Red);
        var vm = ElementViewModel.FromElement(Box(explicitStyle));

        var style = vm.ToElement().Style;
        style.ForeColor.Should().Be(Color.Red);
        style.Font!.Family.Should().Be("Verdana");
        style.Font.Size.Should().Be(14);
        style.Font.Style.Should().Be(FontStyle.Bold);
    }

    [Fact]
    public void A_partially_inherited_style_keeps_each_facet_independent()
    {
        // Explicit colour, inherited font — the two flags must not be coupled.
        var vm = ElementViewModel.FromElement(Box(new Style(ForeColor: Color.Red, BasedOn: "Corpo")));

        var style = vm.ToElement().Style;
        style.ForeColor.Should().Be(Color.Red, "it was explicit in the source");
        style.Font.Should().BeNull("the font was not, so it keeps inheriting");
    }

    [Fact]
    public void A_brand_new_element_still_gets_explicit_defaults()
    {
        // Only elements LOADED from a model can be "inherited"; one created in the toolbox keeps today's
        // behaviour of writing its concrete defaults, so this fix changes nothing for new content.
        var vm = new ElementViewModel(DesignerElementKind.TextBox, "novo");

        var style = vm.ToElement().Style;
        style.ForeColor.Should().NotBeNull("a fresh element has no named style to inherit from");
        style.Font.Should().NotBeNull();
    }
}
