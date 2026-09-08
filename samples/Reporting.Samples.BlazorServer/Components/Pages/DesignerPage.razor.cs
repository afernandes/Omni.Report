using Microsoft.AspNetCore.Components;
using System.Net.Http;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using static Microsoft.AspNetCore.Components.Web.RenderMode;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Samples.BlazorServer;
using Reporting.Samples.BlazorServer.Components;
using Reporting.Viewer.Blazor;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Samples.CodeFirst.Reports;
using ReportDef = Reporting.ReportDefinition;
namespace Reporting.Samples.BlazorServer.Components.Pages;

public partial class DesignerPage
{
    private readonly DesignerState _state = new();
    private ReportDesigner? _designer;
    private string _status = "Pronto. Escolha um sample acima ou comece em branco.";

    private void LoadBlank()
    {
        _state.ReplaceActiveReport(new ReportDefinitionViewModel("Sem título"));
        _status = "Novo relatório em branco carregado.";
    }

    private void LoadSample(string name)
    {
        try
        {
            // Build the full Report (layout + its in-memory data registry), not just the
            // Definition — so the preview can render data-bound elements (charts/map/tablix)
            // populated, via DesignerState.PreviewDataRegistry.
            Reporting.CodeFirst.Report report = name switch
            {
                "vendas" => Sample01_VendasPorCliente.Build(),
                "espelho" => Sample02_EspelhoProdutos.Build(),
                "caixa" => Sample03_RelatorioCaixa.Build(),
                "nfce" => Sample04_CupomNfce.Build(),
                "dashboard" => Sample12_Dashboard.Build(),
                "tablix" => Sample13_TabelaProdutos.Build(),
                "mapa" => Sample14_MapaFiliais.Build(),
                "matriz" => Sample18_MatrizGrande.Build(),
                "crosstab-largo" => Sample19_CrosstabLargo.Build(),
                _ => Sample01_VendasPorCliente.Build(),
            };
            _state.LoadDefinition(report.Definition);
            var vm = _state.Report;
            _state.PreviewDataRegistry = report.DataSources; // feed the sample's data to the preview
            var elementCount = vm.Bands.Sum(b => b.Elements.Count);
            _status = $"Sample '{name}' carregado — {vm.Bands.Count} banda(s), {elementCount} elemento(s). " +
                      "Clique em Pré-visualizar para ver com dados.";
        }
        catch (Exception ex)
        {
            _status = $"Erro ao carregar '{name}': {ex.Message}";
        }
    }

    private void LoadSqliteSample()
    {
        try
        {
            DemoSqliteReport.SeedInto(_state);
            _status = $"DB SQLite carregado · 2 fontes (Clientes + Pedidos) · 1 relação · 2 parâmetros · arquivo: {DemoSqliteDatabase.Path}";
        }
        catch (Exception ex)
        {
            _status = $"Erro ao montar sample SQLite: {ex.Message}";
        }
    }

    private void OnReportSaved(byte[] bytes)
    {
        _status = $"Relatório salvo ({bytes.Length} bytes .repx).";
    }

}
