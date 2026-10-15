using System.Runtime.CompilerServices;
using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Layout;
using Reporting.Output;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// An export from the menu exports the report as it is now, or nothing: never the previous preview, never under
/// another report's name, never twice at once, and never silently — a cancellation nobody asked for is a failure the
/// author sees.
/// </summary>
public sealed class ExportConsistencyTests : BunitContext
{
    private readonly RecordingExporter _exporter = new();
    private readonly SwitchableSource _source = new(new EnumerableDataSource<string>("Dados", ["A"]));
    private readonly DesignerState _state = new();

    public ExportConsistencyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IExporterRegistry>(new ExporterRegistry(
            [new ExporterDescriptor(_exporter, "Registro", "Exporta para teste", "file")], []));
        _state.Parameters.Clear();
        _state.DataSources.Clear();
        _state.DataSources.Add(new DesignerDataSource("Dados", []));
        _state.PreviewDataRegistry = new DataSourceRegistry();
        _state.PreviewDataRegistry.Register(_source);
        _state.Report.Name = "Relatorio A";
    }

    [Fact]
    public async Task ExportarPeloMenu_PreparacaoFalhaDepoisDeUmPreviewValido_NaoExportaODocumentoAnterior()
    {
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, _state));
        await RunCommand(designer, "file.export.registro");
        _exporter.Calls.Should().Be(1);

        _source.Failure = new InvalidOperationException("banco fora do ar");
        await RunCommand(designer, "file.export.registro");

        _exporter.Calls.Should().Be(1, "the previous preview is not the report as it is now");
        designer.Find(".designer-alert").TextContent.Should().Contain("Falha no preview").And.Contain("banco fora do ar",
            "the preparation failure must stay on screen, not be cleared by the export");
    }

    [Fact]
    public async Task ExportarPeloMenu_SegundoPedidoDuranteAExportacao_NaoPreparaNemExportaDeNovo()
    {
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, _state));
        _exporter.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = RunCommand(designer, "file.export.registro");
        await _exporter.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var readsBefore = _source.Reads;

        // Bounded: if the busy guard is missing, these wait on the gated exporter instead of returning at once.
        await RunCommand(designer, "file.export.registro").WaitAsync(TimeSpan.FromSeconds(10));
        await RunCommand(designer, "file.print").WaitAsync(TimeSpan.FromSeconds(10));

        _source.Reads.Should().Be(readsBefore, "a request during an export must not prepare a competing preview");
        _exporter.Gate.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(10));
        _exporter.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Exportar_TrocaDeAbaDuranteAExportacao_NomeiaOArquivoPeloRelatorioExportado()
    {
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, _state));
        _exporter.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var export = RunCommand(designer, "file.export.registro");
        await _exporter.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await designer.InvokeAsync(() => _state.OpenNewDocument("Relatorio B"));
        _exporter.Gate.SetResult();
        await export.WaitAsync(TimeSpan.FromSeconds(10));

        JSInterop.Invocations["omniViewer.download"].Should().ContainSingle()
            .Which.Arguments[0].Should().Be("Relatorio A.reg");
    }

    [Fact]
    public async Task Preview_CancelamentoQueODesignerNaoPediu_MostraAlerta()
    {
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, _state));
        _source.Failure = new TaskCanceledException("tempo esgotado na fonte de dados");

        await RunCommand(designer, "view.preview");

        designer.Find(".designer-alert").TextContent.Should().Contain("Falha no preview").And.Contain("tempo esgotado");
    }

    [Fact]
    public async Task Exportar_ExportadorCancelaPorTempo_MostraAlerta()
    {
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, _state));
        _exporter.Failure = new TaskCanceledException("o exportador desistiu");

        await RunCommand(designer, "file.export.registro");

        designer.Find(".designer-alert").TextContent.Should().Contain("Falha na exportação").And.Contain("desistiu");
        JSInterop.Invocations.Select(i => i.Identifier).Should().NotContain("omniViewer.download");
    }

    [Fact]
    public void TopBar_ExportacaoEmAndamento_DesabilitaExportarEImprimirMasNaoORdl()
    {
        var topBar = Render<TopBar>(p => p
            .Add(x => x.Commands, DesignerCommandCatalog.Build(
                [new ExporterDescriptor(_exporter, "Registro", "Exporta para teste", "file")]))
            .Add(x => x.IsBusy, true)
            .AddCascadingValue(new DesignerMenuCoordinator()));

        topBar.FindAll("button").First(b => b.TextContent.Trim() == "Arquivo").Click();

        bool Disabled(string title) => topBar.FindAll(".dropdown-item").Single(i => i.TextContent.Contains(title))
            .HasAttribute("disabled");
        Disabled("Exportar Registro").Should().BeTrue();
        Disabled("Imprimir").Should().BeTrue();
        Disabled("Exportar RDL").Should().BeFalse("the RDL export serializes the definition and does not touch the preview");
    }

    // The palette runs the same dispatcher as the menus. A command still running can re-render the palette and
    // retire the handler id read for the item, so the item is looked up again.
    private Task RunCommand(IRenderedComponent<ReportDesigner> designer, string id) => designer.InvokeAsync(async () =>
    {
        for (var attempt = 1; ; attempt++)
        {
            _state.CommandPaletteOpen = true;
            try
            {
                await designer.Find($".cmdp-item[data-command-id='{id}']").ClickAsync(new MouseEventArgs());
                return;
            }
            catch (UnknownEventHandlerIdException) when (attempt < 5)
            {
            }
        }
    });

    private sealed class RecordingExporter : IReportExporter
    {
        private int _calls;
        public int Calls => _calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Gate { get; set; }
        public Exception? Failure { get; set; }
        public string Format => "registro";
        public string FileExtension => ".reg";
        public string ContentType => "application/octet-stream";
        public void Export(RenderedReport report, Stream output) => throw new NotSupportedException("Only ExportAsync is used.");

        public async Task ExportAsync(RenderedReport report, Stream output, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            Started.TrySetResult();
            if (Gate is { } gate)
            {
                await gate.Task;
            }
            if (Failure is { } failure)
            {
                throw failure;
            }
            output.WriteByte(1);
        }
    }

    private sealed class SwitchableSource(IReportDataSource source) : IReportDataSource
    {
        private int _reads;
        public int Reads => _reads;
        public Exception? Failure { get; set; }
        public string Name => source.Name;
        public IReportRecordSchema Schema => source.Schema;

        public async IAsyncEnumerable<IReportRecord> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _reads);
            if (Failure is { } failure)
            {
                throw failure;
            }
            await foreach (var row in source.ReadAsync(cancellationToken))
            {
                yield return row;
            }
        }
    }
}
