using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Designer round-trip preservation for named styles (and the analogous report-level Metadata): the Designer VM
/// has no dedicated editor for these yet, so opening and re-saving must not silently drop them. Plus the
/// element-level <see cref="Style.BasedOn"/> must survive ToElement/LoadFrom like the other Style fields.
/// </summary>
public class NamedStylesDesignerTests
{
    [Fact]
    public void Build_preserves_loaded_metadata_and_named_styles()
    {
        var def = new ReportDefinition("r", PageSetup.A4Portrait, DetailBand.Empty)
        {
            Metadata = new EquatableDictionary<string, string>(new Dictionary<string, string> { ["Language"] = "pt-BR" }),
            NamedStyles = new EquatableDictionary<string, Style>(new Dictionary<string, Style>
            {
                ["titulo"] = new Style(ForeColor: Color.FromRgb(0, 0, 128)),
            }),
        };

        var rebuilt = ReportDefinitionViewModel.FromDefinition(def).Build();

        rebuilt.Metadata.ContainsKey("Language").Should().BeTrue("Metadata must not be dropped on save");
        rebuilt.Metadata["Language"].Should().Be("pt-BR");
        rebuilt.NamedStyles.ContainsKey("titulo").Should().BeTrue("named styles must survive load→save");
        rebuilt.NamedStyles["titulo"].ForeColor.Should().Be(Color.FromRgb(0, 0, 128));
    }

    [Fact]
    public void Editing_an_element_does_not_drop_based_on()
    {
        var vm = ElementViewModel.FromElement(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(10)),
            Expression = "x",
            Style = new Style(BasedOn: "titulo"),
        });
        vm.BasedOn.Should().Be("titulo", "BasedOn is captured on load");

        var fmt = PropertyGridDescriptors.For(typeof(TextBoxElement)).First(d => d.Path == "Style.Format");
        vm.ApplyMetaSet(fmt, "C2"); // edit an unrelated property → round-trips ToElement/LoadFrom

        vm.BasedOn.Should().Be("titulo", "BasedOn survives editing another property");
        vm.ToElement().Style.BasedOn.Should().Be("titulo");
    }

    // A report VM with named styles and a single element that inherits one of them via BasedOn.
    private static ReportDefinitionViewModel ReportWith(Dictionary<string, Style> namedStyles, string? elementBasedOn)
    {
        var detail = new DetailBand(Unit.FromMm(10), EquatableArray.Create<ReportElement>(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(10)),
            Expression = "x",
            Style = new Style(BasedOn: elementBasedOn),
        }));
        var def = new ReportDefinition("r", PageSetup.A4Portrait, detail)
        {
            NamedStyles = new EquatableDictionary<string, Style>(namedStyles),
        };
        return ReportDefinitionViewModel.FromDefinition(def);
    }

    [Fact]
    public void Renaming_a_named_style_repoints_element_references()
    {
        var vm = ReportWith(new() { ["titulo"] = new Style(ForeColor: Color.FromRgb(0, 0, 128)) }, elementBasedOn: "titulo");

        vm.RenameNamedStyle("titulo", "cabecalho");

        vm.NamedStyleNames.Should().Contain("cabecalho").And.NotContain("titulo");
        vm.Bands.SelectMany(b => b.Elements).First().BasedOn.Should().Be("cabecalho");
        vm.Build().NamedStyles.ContainsKey("cabecalho").Should().BeTrue("the rename persists to the model");
    }

    [Fact]
    public void Deleting_a_named_style_clears_element_references()
    {
        var vm = ReportWith(new() { ["titulo"] = new Style(ForeColor: Color.FromRgb(0, 0, 128)) }, elementBasedOn: "titulo");

        vm.RemoveNamedStyle("titulo");

        vm.NamedStyleNames.Should().BeEmpty();
        vm.Bands.SelectMany(b => b.Elements).First().BasedOn.Should().BeNull("a deleted style leaves no dangling reference");
    }

    [Fact]
    public void Renaming_updates_named_style_chains()
    {
        var vm = ReportWith(
            new() { ["titulo"] = new Style(ForeColor: Color.FromRgb(0, 0, 128)), ["sub"] = new Style(BasedOn: "titulo") },
            elementBasedOn: null);

        vm.RenameNamedStyle("titulo", "base");

        vm.Build().NamedStyles["sub"].BasedOn.Should().Be("base", "a style based on the renamed one follows it");
    }

    [Fact]
    public void Renaming_to_an_existing_name_is_a_no_op()
    {
        var vm = ReportWith(
            new() { ["a"] = new Style(ForeColor: Color.FromRgb(1, 1, 1)), ["b"] = new Style(ForeColor: Color.FromRgb(2, 2, 2)) },
            elementBasedOn: "a");

        vm.RenameNamedStyle("a", "b"); // would collide with the existing "b" → rejected

        vm.NamedStyleNames.Should().Contain("a").And.Contain("b");
        vm.Bands.SelectMany(b => b.Elements).First().BasedOn.Should().Be("a", "nothing changed");
    }
}
