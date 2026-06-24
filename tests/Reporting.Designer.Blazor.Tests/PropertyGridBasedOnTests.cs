using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The PropertyGrid's "Estilo base" picker: when the report defines named styles, a text element can inherit one
/// via <see cref="Reporting.Styling.Style.BasedOn"/>. Asserts the picker lists the names, hides when there are
/// none, and that choosing one sets the element VM's BasedOn (model state after a synchronous change — not flaky).
/// </summary>
public class PropertyGridBasedOnTests : Bunit.BunitContext
{
    public PropertyGridBasedOnTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static ElementViewModel TextBoxVm() => ElementViewModel.FromElement(new TextBoxElement
    {
        Id = "tb",
        Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(10)),
        Expression = "x",
    });

    [Fact]
    public void The_picker_lists_the_named_styles_when_present()
    {
        var cut = Render<PropertyGrid>(p => p
            .Add(x => x.Element, TextBoxVm())
            .Add(x => x.NamedStyleNames, new[] { "titulo", "destaque" }));

        cut.Markup.Should().Contain("Estilo base");
        var options = cut.FindAll("option").Select(o => o.TextContent).ToList();
        options.Should().Contain("titulo").And.Contain("destaque").And.Contain("(nenhum)");
    }

    [Fact]
    public void The_picker_is_hidden_when_there_are_no_named_styles()
    {
        var cut = Render<PropertyGrid>(p => p
            .Add(x => x.Element, TextBoxVm())
            .Add(x => x.NamedStyleNames, Array.Empty<string>()));

        cut.Markup.Should().NotContain("Estilo base", "no named styles → no picker");
    }

    [Fact]
    public void Selecting_a_named_style_sets_the_element_based_on()
    {
        var vm = TextBoxVm();
        var cut = Render<PropertyGrid>(p => p
            .Add(x => x.Element, vm)
            .Add(x => x.NamedStyleNames, new[] { "titulo" }));

        // The BasedOn select is the one whose options include the named style.
        var select = cut.FindAll("select").First(s => s.InnerHtml.Contains("titulo"));
        select.Change("titulo");

        vm.BasedOn.Should().Be("titulo", "choosing a named style sets the element's BasedOn");
    }
}
