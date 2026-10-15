using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Output.Csv;
using Reporting.Output.Html;
using Reporting.Output.Json;
using Reporting.Output.Markdown;
using Reporting.Output.Svg;
using Reporting.Output.Xml;
using Xunit;

namespace Reporting.Output.Tests;

/// <summary>
/// <c>ExportAsync</c> is the path a web host uses: it must never write synchronously to the destination (ASP.NET Core
/// refuses synchronous I/O on a response body), must produce the same bytes as <c>Export</c>, and — for the
/// exporters that write page by page — must stop when the client gives up instead of finishing the document.
/// </summary>
public sealed class AsyncIoExportTests
{
    private sealed record Linha(string Nome, decimal Valor);

    private static Task<RenderedReport> ReportAsync(int rows) =>
        ReportBuilder.Create("Async IO")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Enumerable.Range(0, rows).Select(i => new Linha($"Item {i}", i * 1.5m)).ToArray())
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 6).Text("{Fields.Valor}").At(90, 0).Size(40, 6))
            .Build()
            .PaginateAsync();

    public static TheoryData<IReportExporter> AllExporters() => new()
    {
        new CsvExporter(),
        new JsonExporter(),
        new XmlExporter(),
        new MarkdownExporter(new MarkdownExportOptions { IncludeFrontMatter = false }),
        new SvgExporter(),
        new SvgHtmlExporter(),
    };

    // Write page by page (or row batch by row batch), so a cancellation lands mid-document.
    public static TheoryData<IReportExporter> SteppedExporters() => new()
    {
        new CsvExporter(),
        new JsonExporter(),
        new XmlExporter(),
        new SvgHtmlExporter(),
    };

    [Theory]
    [MemberData(nameof(AllExporters))]
    public async Task ExportAsync_MesmoRelatorio_ProduzOMesmoDocumentoQueExport(IReportExporter exporter)
    {
        var report = await ReportAsync(600);

        var sync = Comparable(exporter, exporter.ExportToBytes(report));
        var async = Comparable(exporter, await exporter.ExportToBytesAsync(report));

        async.Should().Equal(sync, $"{exporter.Format}: the async path must not change the output");
    }

    // Skia numbers SVG clip ids from a process-wide counter, so an export running concurrently in another test shifts
    // them; each id is renamed by its order of appearance, which is what the document actually depends on.
    private static byte[] Comparable(IReportExporter exporter, byte[] bytes)
    {
        if (exporter is not SvgExporter)
        {
            return bytes;
        }
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var normalized = System.Text.RegularExpressions.Regex.Replace(text, @"(?<=id=""|url\(#|href=""#)[^"")]+",
            match => names.TryGetValue(match.Value, out var name) ? name : names[match.Value] = "id" + names.Count);
        return System.Text.Encoding.UTF8.GetBytes(normalized);
    }

    [Theory]
    [MemberData(nameof(AllExporters))]
    public async Task ExportAsync_DestinoSemEscritaSincrona_NaoEscreveSincronamente(IReportExporter exporter)
    {
        var report = await ReportAsync(600);
        var output = new AsyncOnlyStream();

        await exporter.ExportAsync(report, output);

        output.Length.Should().BeGreaterThan(0, $"{exporter.Format}: the document still reaches the destination");
    }

    [Theory]
    [MemberData(nameof(SteppedExporters))]
    public async Task ExportAsync_ClienteDesisteNoMeio_ParaSemTerminarODocumento(IReportExporter exporter)
    {
        var report = await ReportAsync(1500);
        var complete = exporter.ExportToBytes(report).Length;
        using var cancellation = new CancellationTokenSource();
        // The client disconnects as soon as the first bytes arrive.
        var output = new AsyncOnlyStream { OnWrite = cancellation.Cancel };

        var act = () => exporter.ExportAsync(report, output, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        output.Length.Should().BeLessThan(complete, $"{exporter.Format}: the rest of the document must not be written");
    }

    [Theory]
    [MemberData(nameof(AllExporters))]
    public async Task ExportAsync_TokenJaCancelado_NaoComeca(IReportExporter exporter)
    {
        var report = await ReportAsync(10);
        var output = new AsyncOnlyStream();

        var act = () => exporter.ExportAsync(report, output, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
        output.Length.Should().Be(0);
    }

    [Fact]
    public async Task WriteInStepsAsync_DestinoCancelaDuranteAEscrita_NaoFormataOProximoPasso()
    {
        using var cancellation = new CancellationTokenSource();
        var output = new AsyncOnlyStream { OnWrite = cancellation.Cancel };
        int stepsFormatted = 0;

        IEnumerable<int> Steps(Stream buffer)
        {
            for (int step = 0; step < 3; step++)
            {
                stepsFormatted++;
                buffer.WriteByte((byte)step);
                yield return step;
            }
        }

        var act = () => AsyncExport.WriteInStepsAsync(output, Steps, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stepsFormatted.Should().Be(1, "the cancellation raised by the first write must stop before the second step");
    }

    /// <summary>A destination that, like an ASP.NET Core response body, refuses synchronous writes.</summary>
    private sealed class AsyncOnlyStream : Stream
    {
        private readonly MemoryStream _buffer = new();
        public Action? OnWrite { get; init; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.Length;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => throw new InvalidOperationException("Synchronous operations are disallowed.");
        public override void Write(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _buffer.Write(buffer.Span);
            OnWrite?.Invoke();
            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _buffer.Dispose();
            base.Dispose(disposing);
        }
    }
}
