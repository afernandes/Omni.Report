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
}
