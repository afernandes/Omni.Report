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
}
