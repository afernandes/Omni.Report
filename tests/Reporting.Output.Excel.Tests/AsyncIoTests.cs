using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Output;
using Xunit;

namespace Reporting.Output.Excel.Tests;

/// <summary>
/// The package is only complete at the end, so ExportAsync builds it in memory and then writes it to the destination
/// with asynchronous I/O only — what an ASP.NET Core response body requires.
/// </summary>
public sealed class AsyncIoTests
{
    private sealed record Linha(string Nome);

    private static Task<RenderedReport> ReportAsync() =>
        ReportBuilder.Create("Async IO")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Enumerable.Range(0, 200).Select(i => new Linha($"Item {i}")).ToArray())
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 6))
            .Build()
            .PaginateAsync();

    [Fact]
    public async Task ExportAsync_DestinoSemEscritaSincrona_EscrevePacoteCompleto()
    {
        var report = await ReportAsync();
        var output = new AsyncOnlyStream();

        await new ExcelExporter().ExportAsync(report, output);

        var bytes = output.ToArray();
        bytes.Length.Should().BeGreaterThan(4);
        bytes.AsSpan(0, 2).ToArray().Should().Equal((byte)'P', (byte)'K');
    }

    [Fact]
    public async Task ExportAsync_TokenJaCancelado_NaoEscreveNada()
    {
        var report = await ReportAsync();
        var output = new AsyncOnlyStream();

        var act = () => new ExcelExporter().ExportAsync(report, output, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
        output.ToArray().Should().BeEmpty();
    }

    /// <summary>A destination that, like an ASP.NET Core response body, refuses synchronous writes.</summary>
    private sealed class AsyncOnlyStream : Stream
    {
        private readonly MemoryStream _buffer = new();
        public byte[] ToArray() => _buffer.ToArray();
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
            _buffer.Write(buffer.Span);
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
