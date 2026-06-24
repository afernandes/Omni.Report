using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The runtime parameter prompt dialog (shown on Preview/F5 when a report has parameters). Asserts initial-render
/// markup and the OnConfirmed callback payload — no DOM re-query after an event, so it is not flaky.
/// </summary>
public class ParameterPromptDialogTests : Bunit.BunitContext
{
    public ParameterPromptDialogTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<ParameterPromptDialog> Open(
        IReadOnlyList<DesignerParameter> ps,
        IReadOnlyDictionary<string, string?>? initial = null,
        Action<(IReadOnlyDictionary<string, string?> Values, bool Remember)>? onConfirmed = null)
        => Render<ParameterPromptDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Parameters, ps)
            .Add(x => x.InitialValues, initial)
            .Add(x => x.OnConfirmed, onConfirmed ?? (_ => { })));

    [Fact]
    public void Hidden_parameters_are_not_prompted()
    {
        var cut = Open(new[]
        {
            new DesignerParameter("visivel", DesignerFieldType.Text, "x"),
            new DesignerParameter("oculto", DesignerFieldType.Text, "y") { Hidden = true },
        });
        cut.FindAll("#pp-visivel").Should().HaveCount(1);
        cut.FindAll("#pp-oculto").Should().BeEmpty("hidden params are driven by default/query, never prompted");
    }

    [Fact]
    public void Each_type_gets_its_matching_input()
    {
        var cut = Open(new[]
        {
            new DesignerParameter("n", DesignerFieldType.Number, "1") { Required = false },
            new DesignerParameter("d", DesignerFieldType.Date) { Required = false },
            new DesignerParameter("t", DesignerFieldType.Text) { Required = false },
        });
        cut.Find("input#pp-n").GetAttribute("type").Should().Be("number");
        cut.Find("input#pp-d").GetAttribute("type").Should().Be("date");
        cut.Find("input#pp-t").GetAttribute("type").Should().Be("text");
    }

    [Fact]
    public void Static_available_values_render_a_dropdown_with_none_only_when_optional()
    {
        var required = new[] { new DesignerParameter("s", DesignerFieldType.Text, "A") { Required = true, AvailableValuesText = "A|Ativo\nB|Baixado" } };
        Open(required).Find("select#pp-s").QuerySelectorAll("option").Length.Should().Be(2, "required → no (nenhum) option");

        var optional = new[] { new DesignerParameter("s", DesignerFieldType.Text, "A") { Required = false, AvailableValuesText = "A|Ativo\nB|Baixado" } };
        Open(optional).Find("select#pp-s").QuerySelectorAll("option").Length.Should().Be(3, "optional → adds (nenhum)");
    }

    [Fact]
    public void A_required_blank_parameter_disables_executar()
    {
        var cut = Open(new[] { new DesignerParameter("obrig", DesignerFieldType.Text) { Required = true } }); // no default
        var executar = cut.FindAll("button").First(b => b.TextContent.Contains("Executar"));
        executar.HasAttribute("disabled").Should().BeTrue("a required parameter with no value blocks the run");
        cut.Markup.Should().Contain("obrigatórios");
    }

    [Fact]
    public void A_required_parameter_with_a_default_runs_and_confirms_the_values()
    {
        (IReadOnlyDictionary<string, string?> Values, bool Remember)? got = null;
        var cut = Open(
            new[] { new DesignerParameter("obrig", DesignerFieldType.Text, "preenchido") { Required = true } },
            onConfirmed: r => got = r);

        var executar = cut.FindAll("button").First(b => b.TextContent.Contains("Executar"));
        executar.HasAttribute("disabled").Should().BeFalse();
        executar.Click();

        got.Should().NotBeNull();
        got!.Value.Values["obrig"].Should().Be("preenchido");
        got.Value.Remember.Should().BeTrue("the remember checkbox defaults on");
    }

    [Fact]
    public void The_prompt_text_is_used_as_the_label_when_present()
    {
        var cut = Open(new[] { new DesignerParameter("ano", DesignerFieldType.Number, "2026") { Required = false, Prompt = "Ano de referência" } });
        cut.Markup.Should().Contain("Ano de referência");
    }
}
