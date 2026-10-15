using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Layout;
using Reporting.Output;
using Reporting.Output.Pdf;
using Reporting.Output.Xml;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The export flow reflects what the host registered and never fails silently: the File menu lists the registered
/// exporters (not a fixed list), the preview refuses clicks visibly while an export runs, and a failure reaches the
/// author instead of only the server's stderr.
/// </summary>
public sealed class ExportFlowTests : BunitContext
{
    [Fact]
    public void TopBar_MenuArquivo_ListaExatamenteOsExportadoresRegistrados()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ExporterDescriptor[] exporters =
        [
            new(new SkiaPdfExporter(), "PDF", "Portable Document Format", "file-text"),
            new(new XmlExporter(), "XML", "Exportar como XML", "file-code"),
        ];
        var topBar = Render<TopBar>(p => p.Add(x => x.Commands, DesignerCommandCatalog.Build(exporters))
            .AddCascadingValue(new DesignerMenuCoordinator()));

        topBar.FindAll("button").First(b => b.TextContent.Trim() == "Arquivo").Click();

        var exportItems = topBar.FindAll(".dropdown-item").Select(i => i.TextContent.Trim())
            .Where(t => t.StartsWith("Exportar", StringComparison.Ordinal)).ToList();
        exportItems.Should().BeEquivalentTo(["Exportar PDF", "Exportar XML", "Exportar RDL (.rdl)"],
            "DOCX, CSV and the other formats were not registered, so they must not appear");
    }

    [Fact]
    public void PreviewMode_ExportacaoEmAndamento_DesabilitaExportarEImprimir()
    {
        ExporterDescriptor[] exporters = [new(new XmlExporter(), "XML", "Exportar como XML", "file-code")];
        var preview = Render<PreviewMode>(p => p
            .Add(x => x.Pages, [new byte[] { 1 }])
            .Add(x => x.Exporters, exporters)
            .Add(x => x.IsBusy, true));

        preview.FindAll("button[title='Exportar como XML']").Should().ContainSingle()
            .Which.HasAttribute("disabled").Should().BeTrue();
        preview.FindAll("button").Single(b => b.TextContent.Contains("Imprimir")).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ReportDesigner_ExportadorQueFalha_MostraAlertaComOMotivo()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IExporterRegistry>(new ExporterRegistry(
            [new ExporterDescriptor(new FailingExporter(), "Quebrado", "Sempre falha", "file")], []));
        var state = new DesignerState();
        state.Parameters.Clear(); // the sample parameters would open the prompt and wait for the author
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));

        designer.FindAll("button").First(b => b.TextContent.Trim() == "Arquivo").Click();
        await designer.InvokeAsync(() => designer.FindAll(".dropdown-item")
            .Single(i => i.TextContent.Contains("Exportar Quebrado")).Click());

        designer.WaitForAssertion(() =>
            designer.Find(".designer-alert").TextContent.Should().Contain("Quebrado").And.Contain("disco cheio"),
            TimeSpan.FromSeconds(10));
    }

    private sealed class FailingExporter : IReportExporter
    {
        public string Format => "quebrado";
        public string FileExtension => ".bin";
        public string ContentType => "application/octet-stream";
        public void Export(RenderedReport report, Stream output) => throw new IOException("disco cheio");
    }
}
