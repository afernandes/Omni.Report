using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Serialization;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Guards that report parameters defined in the designer are <b>persisted</b> — built into the
/// definition and reloaded with every field. Regression for the bug where <c>BuildDefinition</c>
/// dropped the parameter list entirely (only data sources/relations were carried), so a designer
/// could declare a parameter but never save it.
/// </summary>
public class DesignerParameterRoundTripTests
{
    [Fact]
    public void Report_parameters_are_persisted_and_reload_with_every_field()
    {
        var state = new DesignerState();
        state.Parameters.Clear();
        state.Parameters.Add(new DesignerParameter("ano", DesignerFieldType.Number, "2026")
        {
            Prompt = "Ano de referência",
            Required = true,
            AllowMultiple = false,
        });

        // Build → the definition must carry the full parameter (it used to be dropped).
        var definition = state.BuildDefinition();
        definition.Parameters.Should().ContainSingle("the report parameter must survive BuildDefinition");
        var p = definition.Parameters[0];
        p.Name.Should().Be("ano");
        p.ValueType.Should().Be(typeof(double));
        p.Prompt.Should().Be("Ano de referência");
        p.Required.Should().BeTrue();

        // Save → load into a fresh designer → every field surfaces again for editing.
        var bytes = new RepxSerializer().SaveToBytes(definition);
        var reloaded = new DesignerState();
        reloaded.Load(bytes);

        reloaded.Parameters.Should().ContainSingle();
        var back = reloaded.Parameters[0];
        back.Name.Should().Be("ano");
        back.Type.Should().Be(DesignerFieldType.Number);
        back.Prompt.Should().Be("Ano de referência");
        back.Required.Should().BeTrue();
        back.DefaultValue.Should().Be("2026");
    }

    [Fact]
    public void Parameter_available_values_persist_through_designer_and_repx()
    {
        var state = new DesignerState();
        state.Parameters.Clear();
        state.Parameters.Add(new DesignerParameter("status", DesignerFieldType.Text, "A")
        {
            Prompt = "Situação",
            AvailableValuesText = "A|Ativo\nI|Inativo", // static "value|label" lines
        });
        state.Parameters.Add(new DesignerParameter("cliente", DesignerFieldType.Text)
        {
            AvailableValuesDataSet = "Clientes", // query-driven
            AvailableValuesValueField = "Id",
            AvailableValuesLabelField = "Nome",
        });

        var definition = state.BuildDefinition();
        definition.Parameters[0].AvailableValues!.Values.Select(v => v.Value).Should().Equal("A", "I");
        definition.Parameters[0].AvailableValues!.Values[1].Label.Should().Be("Inativo");
        definition.Parameters[1].AvailableValues!.IsQuery.Should().BeTrue();

        // Through .repx and back into a fresh designer, every Available Values field surfaces again.
        var reloaded = new DesignerState();
        reloaded.Load(new RepxSerializer().SaveToBytes(definition));

        var status = reloaded.Parameters[0];
        status.AvailableValuesText.Should().Be("A|Ativo\nI|Inativo");
        var cliente = reloaded.Parameters[1];
        cliente.AvailableValuesDataSet.Should().Be("Clientes");
        cliente.AvailableValuesValueField.Should().Be("Id");
        cliente.AvailableValuesLabelField.Should().Be("Nome");
    }
}
