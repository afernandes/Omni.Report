using Bunit;
using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The generic dictionary editor renders a key/value row per entry of an
/// <c>EquatableDictionary&lt;string,string&gt;</c> property (e.g. a subreport's parameter bindings),
/// with add (unique default key) / remove — no hand-coded dict UI.
/// </summary>
public class PropertyGridDictEditorTests : Bunit.BunitContext
{
    public PropertyGridDictEditorTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static (ElementViewModel Vm, PropertyGridDescriptor Desc) SubreportWithOneParam()
    {
        var vm = ElementViewModel.FromElement(new SubreportElement
        {
            Id = "s1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(40)),
            ParameterBindings = new EquatableDictionary<string, string>(new Dictionary<string, string>
            {
                ["clienteId"] = "Fields.Id",
            }),
        });
        var desc = PropertyGridDescriptors.For(typeof(SubreportElement)).Single(d => d.Name == "ParameterBindings");
        return (vm, desc);
    }

    [Fact]
    public void Renders_a_key_value_row_per_entry()
    {
        var (vm, desc) = SubreportWithOneParam();

        var cut = Render<PropertyGridDictEditor>(p => p
            .Add(x => x.Element, vm)
            .Add(x => x.Descriptor, desc));

        cut.Markup.Should().Contain("clienteId", "the key renders");
        cut.Markup.Should().Contain("Fields.Id", "the value renders");
    }

    [Fact]
    public void Add_appends_an_entry_with_a_unique_default_key()
    {
        var (vm, desc) = SubreportWithOneParam();
        var cut = Render<PropertyGridDictEditor>(p => p
            .Add(x => x.Element, vm)
            .Add(x => x.Descriptor, desc));

        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar")).Click();

        ((SubreportElement)vm.ToElement()).ParameterBindings.Count.Should().Be(2, "a fresh entry is added");
    }

    private static (ElementViewModel Vm, PropertyGridDescriptor Desc) SubreportWith(params (string Key, string Value)[] entries)
    {
        var vm = ElementViewModel.FromElement(new SubreportElement
        {
            Id = "s1",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(40)),
            ParameterBindings = new EquatableDictionary<string, string>(
                entries.ToDictionary(e => e.Key, e => e.Value)),
        });
        var desc = PropertyGridDescriptors.For(typeof(SubreportElement)).Single(d => d.Name == "ParameterBindings");
        return (vm, desc);
    }

    [Fact]
    public void Editing_one_entry_keeps_every_other_row_present()
    {
        // Regression: an EquatableDictionary enumerates in hash order, so re-deriving rows on each keystroke
        // re-shuffled/dropped them. With local rows + a stable @key, editing one value leaves the rest intact.
        var (vm, desc) = SubreportWith(("a", "1"), ("b", "2"), ("c", "3"));
        var cut = Render<PropertyGridDictEditor>(p => p.Add(x => x.Element, vm).Add(x => x.Descriptor, desc));

        var valueInputs = cut.FindAll("input[placeholder='valor / expressão']");
        valueInputs.Count.Should().Be(3);
        valueInputs[0].Change("99");

        ((SubreportElement)vm.ToElement()).ParameterBindings.Count.Should().Be(3, "editing one entry must not drop the others");
    }

    [Fact]
    public void Clearing_a_key_keeps_the_row_visible_for_retyping()
    {
        // Regression: clearing a key to retype used to discard the whole row (and its value) on commit.
        // The row stays visible (just not persisted) so the value survives until a new key is typed.
        var (vm, desc) = SubreportWith(("clienteId", "Fields.Id"));
        var cut = Render<PropertyGridDictEditor>(p => p.Add(x => x.Element, vm).Add(x => x.Descriptor, desc));

        cut.FindAll("input[placeholder='chave']")[0].Change(string.Empty);

        cut.FindAll("input[placeholder='valor / expressão']").Count.Should().Be(1, "the row stays visible while the key is empty");
        cut.FindAll("input[placeholder='valor / expressão']")[0].GetAttribute("value").Should().Be("Fields.Id", "its value is not lost");
    }
}
