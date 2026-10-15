using System.Collections.ObjectModel;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>Small defects seen in the browser (ROADMAP item 22): an encoded entity in a placeholder, a tab that kept
/// "Untitled.repx", an English type name under Portuguese labels, and allowed values offered for dates.</summary>
public sealed class SmallVisualDefectsTests : BunitContext
{
    public SmallVisualDefectsTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void ParametersList_PlaceholderDosValoresPermitidos_TemQuebraDeLinhaNaoEntidade()
    {
        var list = Render<ParametersList>(p => p.Add(x => x.Parameters,
            new ObservableCollection<DesignerParameter> { new("Status", DesignerFieldType.Text) }));

        list.Find("textarea").GetAttribute("placeholder").Should().Be("1|Ativo\n0|Inativo");
    }

    [Fact]
    public void ParametersList_TiposDeParametro_AparecemEmPortugues()
    {
        var list = Render<ParametersList>(p => p.Add(x => x.Parameters,
            new ObservableCollection<DesignerParameter> { new("Inicio", DesignerFieldType.Date) }));

        list.FindAll("option").Select(o => o.TextContent).Should().Equal("Texto", "Número", "Data", "Sim/Não", "Moeda");
    }

    [Fact]
    public void ParametersList_ParametroData_NaoOfereceValoresPermitidos()
    {
        var list = Render<ParametersList>(p => p.Add(x => x.Parameters,
            new ObservableCollection<DesignerParameter> { new("Inicio", DesignerFieldType.Date) }));

        list.Markup.Should().NotContain("Valores permitidos");
        list.FindAll("textarea").Should().BeEmpty();
    }

    [Fact]
    public void ParameterPrompt_TipoDoParametro_AparecePortugues()
    {
        var prompt = Render<ParameterPromptDialog>(p => p
            .Add(x => x.Visible, true)
            .Add(x => x.Parameters, [new DesignerParameter("Inicio", DesignerFieldType.Date)]));

        prompt.Find(".pp-type").TextContent.Should().Be("Data");
    }

    [Fact]
    public void LoadDefinition_RelatorioComNome_RenomeiaAAba()
    {
        var state = new DesignerState();
        var definition = new DesignerState(new ReportDefinitionViewModel("Vendas por Cliente")).BuildDefinition();

        state.LoadDefinition(definition);

        state.ActiveTab.FileName.Should().Be("Vendas por Cliente.repx");
    }

    [Fact]
    public void AbrirArquivo_Repx_AbaFicaComONomeDoArquivo()
    {
        var state = new DesignerState();
        var bytes = new DesignerState().Save();
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));

        designer.FindAll("button").First(b => b.TextContent.Trim() == "Arquivo").Click();
        designer.FindComponents<InputFile>()
            .First(c => c.Instance.AdditionalAttributes?["accept"] as string == ".repx")
            .UploadFiles(InputFileContent.CreateFromBinary(bytes, "folha-mensal.repx"));

        designer.WaitForAssertion(() => state.ActiveTab.FileName.Should().Be("folha-mensal.repx"), TimeSpan.FromSeconds(5));
    }
}
