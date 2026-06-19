using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Covers the Chart property editor (B4 slice 3): the PropertyGrid shows a "Gráfico" section for
/// chart elements with a series table, and hides it for other element kinds.
/// </summary>
public class ChartPropertyGridTests : Bunit.BunitContext
{
    public ChartPropertyGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Chart_editor_section_renders_for_a_chart_element()
    {
        var vm = new ElementViewModel(DesignerElementKind.Chart, "c1") { ChartTitle = "Vendas" };
        vm.ChartSeries.Add(new ChartSeriesRule { Name = "Receita" });

        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, vm));

        cut.Markup.Should().Contain("Gráfico", "the metadata category header");
        cut.Markup.Should().Contain("Séries", "the list editor row label");
        cut.Markup.Should().Contain("Receita", "the existing series renders in the list editor");
    }

    [Fact]
    public void Adding_a_series_via_the_list_editor_mutates_the_view_model()
    {
        var vm = new ElementViewModel(DesignerElementKind.Chart, "c1");
        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, vm));

        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar")).Click();

        vm.ChartSeries.Should().ContainSingle("the generic list editor appends a default series");
    }

    [Fact]
    public void Chart_section_is_absent_for_a_textbox()
    {
        var vm = new ElementViewModel(DesignerElementKind.TextBox, "t1") { Expression = "{Fields.X}" };

        var cut = Render<PropertyGrid>(p => p.Add(x => x.Element, vm));

        cut.Markup.Should().NotContain("Gráfico", "the Chart category only shows for chart elements");
    }
}
