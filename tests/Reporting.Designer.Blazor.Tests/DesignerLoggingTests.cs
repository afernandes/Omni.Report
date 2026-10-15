using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Layout;
using Reporting.Output.Pdf;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Designer failures reach the host's logging pipeline as structured events — not <c>Console.Error</c> — and carry
/// no report content; failures the author can act on (opening a file) also show in the alert banner.
/// </summary>
public sealed class DesignerLoggingTests : BunitContext
{
    private readonly CapturingLoggerProvider _logs = new();

    public DesignerLoggingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging(builder => builder.AddProvider(_logs));
    }

    [Fact]
    public async Task Export_ExportadorQueFalha_RegistraEventoEstruturadoComOFormato()
    {
        Services.AddSingleton<IExporterRegistry>(new ExporterRegistry(
            [new ExporterDescriptor(new FailingExporter(), "Quebrado", "Sempre falha", "file")], []));
        var state = new DesignerState();
        state.Parameters.Clear();
        state.Report.Name = "Folha de Pagamento - Maria Souza";
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));

        designer.FindAll("button").First(b => b.TextContent.Trim() == "Arquivo").Click();
        await designer.InvokeAsync(() => designer.FindAll(".dropdown-item")
            .Single(i => i.TextContent.Contains("Exportar Quebrado")).Click());

        designer.WaitForAssertion(() => _logs.Entries.Should().ContainSingle(e => e.EventId == 4),
            TimeSpan.FromSeconds(10));
        var entry = _logs.Entries.Single(e => e.EventId == 4);
        entry.Level.Should().Be(LogLevel.Error);
        entry.State.Should().Contain(new KeyValuePair<string, object?>("Format", "quebrado"));
        entry.Exception.Should().BeOfType<IOException>();
        entry.Message.Should().NotContain("Maria", "log events carry no report content");
    }

    [Fact]
    public void Abrir_RepxInvalido_MostraAlertaERegistraEvento()
    {
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, new DesignerState()));

        designer.FindAll("button").First(b => b.TextContent.Trim() == "Arquivo").Click();
        designer.FindComponents<InputFile>()
            .First(c => c.Instance.AdditionalAttributes?["accept"] as string == ".repx")
            .UploadFiles(InputFileContent.CreateFromText("isto não é um repx", "corrompido.repx"));

        designer.WaitForAssertion(() =>
            designer.Find(".designer-alert").TextContent.Should().Contain("corrompido.repx"), TimeSpan.FromSeconds(5));
        _logs.Entries.Should().ContainSingle(e => e.EventId == 1 && e.Exception != null);
    }

    private sealed class FailingExporter : Reporting.Output.Pdf.IReportExporter
    {
        public string Format => "quebrado";
        public string FileExtension => ".bin";
        public string ContentType => "application/octet-stream";
        public void Export(RenderedReport report, Stream output) => throw new IOException("disco cheio");
    }

    private sealed record LogEntry(int EventId, LogLevel Level, string Message, Exception? Exception,
        IReadOnlyList<KeyValuePair<string, object?>> State);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries
        {
            get { lock (_entries) return _entries.ToList(); }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
                lock (owner._entries)
                {
                    owner._entries.Add(new(eventId.Id, logLevel, formatter(state, exception), exception, values));
                }
            }
        }
    }
}
