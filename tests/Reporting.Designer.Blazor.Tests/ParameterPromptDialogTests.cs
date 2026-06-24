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

    // ── Cascading (dependent) parameters: Estado → Cidade ─────────────────────────────────

    // A spy resolver: records each (param, parentValue) it is asked for and returns cities filtered by the
    // "estado" value passed in — mirroring what ParameterValueResolver does against a real dataset.
    private static (Func<DesignerParameter, IReadOnlyDictionary<string, string?>, Task<IReadOnlyList<Reporting.Parameters.ParameterValue>>> Resolve,
                    List<(string Param, string? Estado)> Calls) CidadeResolver()
    {
        var calls = new List<(string Param, string? Estado)>();
        Func<DesignerParameter, IReadOnlyDictionary<string, string?>, Task<IReadOnlyList<Reporting.Parameters.ParameterValue>>> resolve =
            (p, vals) =>
            {
                vals.TryGetValue("estado", out var estado);
                calls.Add((p.Name, estado));
                Reporting.Parameters.ParameterValue[] opts = p.Name == "cidade"
                    ? estado switch
                    {
                        "PR" => new[] { new Reporting.Parameters.ParameterValue("Curitiba") },
                        "RS" => new[] { new Reporting.Parameters.ParameterValue("Porto Alegre"), new Reporting.Parameters.ParameterValue("Caxias") },
                        _ => Array.Empty<Reporting.Parameters.ParameterValue>(),
                    }
                    : Array.Empty<Reporting.Parameters.ParameterValue>();
                return Task.FromResult<IReadOnlyList<Reporting.Parameters.ParameterValue>>(opts);
            };
        return (resolve, calls);
    }

    private static DesignerParameter Estado() =>
        new("estado", DesignerFieldType.Text) { Required = false, AvailableValuesText = "RS|RS\nPR|PR" };

    private static DesignerParameter Cidade() =>
        new("cidade", DesignerFieldType.Text)
        {
            Required = false,
            AvailableValuesDataSet = "Cidades",
            AvailableValuesValueField = "Nome",
            AvailableValuesFilterField = "Estado",
            AvailableValuesDependsOn = "estado",
        };

    [Fact]
    public void Query_driven_options_are_resolved_when_the_dialog_opens()
    {
        var (resolve, calls) = CidadeResolver();
        Render<ParameterPromptDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Parameters, new[] { Estado(), Cidade() })
            .Add(x => x.InitialValues, new Dictionary<string, string?> { ["estado"] = "RS" })
            .Add(x => x.ResolveOptions, resolve));

        // On open, the dependent "cidade" is resolved with the seeded parent value ("RS").
        calls.Should().Contain(c => c.Param == "cidade" && c.Estado == "RS");
    }

    [Fact]
    public void Changing_the_parent_re_resolves_the_dependent_child()
    {
        var (resolve, calls) = CidadeResolver();
        var cut = Render<ParameterPromptDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Parameters, new[] { Estado(), Cidade() })
            .Add(x => x.ResolveOptions, resolve));

        calls.Clear(); // ignore the on-open resolution; focus on the parent change
        cut.Find("select#pp-estado").Change("PR");

        // The child is re-resolved with the NEW parent value (no DOM re-query — we assert the resolver spy).
        calls.Should().Contain(c => c.Param == "cidade" && c.Estado == "PR");
    }

    [Fact]
    public void Changing_the_parent_clears_a_child_value_outside_the_new_domain()
    {
        var (resolve, _) = CidadeResolver();
        (IReadOnlyDictionary<string, string?> Values, bool Remember)? got = null;
        var cut = Render<ParameterPromptDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Parameters, new[] { Estado(), Cidade() })
            // Start on RS with "Porto Alegre" chosen — a valid RS city.
            .Add(x => x.InitialValues, new Dictionary<string, string?> { ["estado"] = "RS", ["cidade"] = "Porto Alegre" })
            .Add(x => x.ResolveOptions, resolve)
            .Add(x => x.OnConfirmed, (Action<(IReadOnlyDictionary<string, string?>, bool)>)(r => got = r)));

        // Switch to PR → "Porto Alegre" is no longer a valid option, so the child pick is dropped.
        cut.Find("select#pp-estado").Change("PR");
        cut.FindAll("button").First(b => b.TextContent.Contains("Executar")).Click();

        got.Should().NotBeNull();
        got!.Value.Values["estado"].Should().Be("PR");
        got.Value.Values["cidade"].Should().BeNullOrEmpty("the previous city isn't valid under the new state");
    }

    [Fact]
    public void A_query_driven_child_renders_a_dropdown_even_with_an_empty_domain()
    {
        var (resolve, _) = CidadeResolver();
        // "cidade" depends on "estado", which hasn't been chosen → its domain is empty on open.
        var cut = Render<ParameterPromptDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Parameters, new[] { Estado(), Cidade() })
            .Add(x => x.ResolveOptions, resolve));

        // It must still be a validated <select> (not a free-text <input> that would accept arbitrary values).
        cut.FindAll("select#pp-cidade").Should().HaveCount(1, "a query-driven param always renders a dropdown");
        cut.FindAll("input#pp-cidade").Should().BeEmpty();
        cut.Markup.Should().Contain("defina \"estado\" primeiro");
    }

    [Fact]
    public void A_change_propagates_through_a_multi_level_cascade()
    {
        // Estado → Cidade → Bairro. Changing the top must ripple all the way down.
        var calls = new List<(string Param, string? Cidade)>();
        Func<DesignerParameter, IReadOnlyDictionary<string, string?>, Task<IReadOnlyList<Reporting.Parameters.ParameterValue>>> resolve =
            (p, vals) =>
            {
                vals.TryGetValue("estado", out var estado);
                vals.TryGetValue("cidade", out var cidade);
                if (p.Name == "bairro") { calls.Add(("bairro", cidade)); }
                Reporting.Parameters.ParameterValue[] opts = p.Name switch
                {
                    "cidade" => estado == "RS"
                        ? new[] { new Reporting.Parameters.ParameterValue("Porto Alegre") }
                        : Array.Empty<Reporting.Parameters.ParameterValue>(),
                    "bairro" => cidade == "Porto Alegre"
                        ? new[] { new Reporting.Parameters.ParameterValue("Centro") }
                        : Array.Empty<Reporting.Parameters.ParameterValue>(),
                    _ => Array.Empty<Reporting.Parameters.ParameterValue>(),
                };
                return Task.FromResult<IReadOnlyList<Reporting.Parameters.ParameterValue>>(opts);
            };

        var bairro = new DesignerParameter("bairro", DesignerFieldType.Text)
        {
            Required = false,
            AvailableValuesDataSet = "Bairros",
            AvailableValuesValueField = "Nome",
            AvailableValuesFilterField = "Cidade",
            AvailableValuesDependsOn = "cidade",
        };
        var cut = Render<ParameterPromptDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Parameters, new[] { Estado(), Cidade(), bairro })
            // Start fully populated down the chain (RS → Porto Alegre → Centro).
            .Add(x => x.InitialValues, new Dictionary<string, string?> { ["estado"] = "RS", ["cidade"] = "Porto Alegre", ["bairro"] = "Centro" })
            .Add(x => x.ResolveOptions, resolve));

        calls.Clear();
        // Change estado to PR → cidade empties (Porto Alegre invalid → cleared) → bairro must re-resolve too.
        cut.Find("select#pp-estado").Change("PR");

        calls.Should().Contain(c => c.Param == "bairro", "the grandchild re-resolves when the grandparent changes");
    }
}
