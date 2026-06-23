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
    public void Parameter_default_value_expression_persists_through_designer_and_repx()
    {
        // Regression: DesignerParameter ignored ReportParameter.DefaultValueExpression on both BuildDefinition
        // (ToReportParameter) and Load (From), so loading a report with an =expression default and re-saving in
        // the designer silently dropped it.
        var state = new DesignerState();
        state.Parameters.Clear();
        state.Parameters.Add(new DesignerParameter("dataRef", DesignerFieldType.Date)
        {
            DefaultValueExpression = "Today()",
            Prompt = "Data de referência",
        });

        // Build → the core parameter must carry the expression default.
        var definition = state.BuildDefinition();
        definition.Parameters.Should().ContainSingle();
        definition.Parameters[0].DefaultValueExpression.Should().Be("Today()");

        // Save → load into a fresh designer → the expression default surfaces again for editing.
        var bytes = new RepxSerializer().SaveToBytes(definition);
        var reloaded = new DesignerState();
        reloaded.Load(bytes);
        reloaded.Parameters.Should().ContainSingle();
        reloaded.Parameters[0].DefaultValueExpression.Should().Be("Today()");
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

    [Fact]
    public void Report_variables_persist_through_designer_and_repx()
    {
        var state = new DesignerState();
        state.Variables.Clear();
        state.Variables.Add(new DesignerVariable("Acumulado", "Sum(Fields.Total)",
            Reporting.Parameters.VariableScope.Report));

        var definition = state.BuildDefinition();
        definition.Variables.Should().ContainSingle();
        definition.Variables[0].Name.Should().Be("Acumulado");
        definition.Variables[0].Expression.Should().Be("Sum(Fields.Total)");

        var reloaded = new DesignerState();
        reloaded.Load(new RepxSerializer().SaveToBytes(definition));
        reloaded.Variables.Should().ContainSingle();
        reloaded.Variables[0].Expression.Should().Be("Sum(Fields.Total)");
        reloaded.Variables[0].Scope.Should().Be(Reporting.Parameters.VariableScope.Report);
    }

    [Fact]
    public void Parameter_metadata_flags_persist_through_designer_and_repx()
    {
        var state = new DesignerState();
        state.Parameters.Clear();
        state.Parameters.Add(new DesignerParameter("token", DesignerFieldType.Text)
        {
            Hidden = true,
            Nullable = true,
            AllowBlank = true,
        });

        var definition = state.BuildDefinition();
        definition.Parameters[0].Hidden.Should().BeTrue();
        definition.Parameters[0].Nullable.Should().BeTrue();
        definition.Parameters[0].AllowBlank.Should().BeTrue();

        var reloaded = new DesignerState();
        reloaded.Load(new RepxSerializer().SaveToBytes(definition));
        var p = reloaded.Parameters[0];
        p.Hidden.Should().BeTrue();
        p.Nullable.Should().BeTrue();
        p.AllowBlank.Should().BeTrue();
    }
}
