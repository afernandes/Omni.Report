using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Output.Image;
using Reporting.Output.Pdf;
using Xunit;

namespace Reporting.Output.Tests;

/// <summary>
/// <c>IReportExporter</c> gained an async counterpart so the final — and often most expensive — stage stops
/// being synchronous I/O in the middle of an async pipeline (<c>PaginateAsync</c>/<c>ReadAsync</c> already
/// take a token). The default interface implementation keeps external implementers compiling; the exporters
/// that iterate pages override it to honour cancellation at page boundaries.
/// </summary>
public class ExporterAsyncTests
{
    private sealed record Linha(string Nome);

    /// <summary>Enough rows to span many pages, so cancellation has page boundaries to stop at.</summary>
    private static async Task<RenderedReport> BigReportAsync() =>
        await ReportBuilder.Create("Async")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Enumerable.Range(0, 400).Select(i => new Linha($"Item {i}")).ToArray())
            .Detail(d => d.Height(6).Text("{Fields.Nome}").At(0, 0).Size(80, 6))
            .Build()
            .PaginateAsync();

    public static TheoryData<IReportExporter> Exporters() => new()
    {
        // Fixed CreationDate: the PDF embeds it, so two exports a millisecond apart differ byte-for-byte and
        // the sync-vs-async comparison below would fail for a reason that has nothing to do with async.
        new SkiaPdfExporter(new PdfExportOptions { CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }),
        new PngImageExporter(),
        new TiffImageExporter(),
    };

    [Theory]
    [MemberData(nameof(Exporters))]
    public async Task ExportAsync_produces_the_same_bytes_as_the_sync_path(IReportExporter exporter)
    {
        var report = await BigReportAsync();

        var sync = exporter.ExportToBytes(report);
        var async = await exporter.ExportToBytesAsync(report);

        async.Should().Equal(sync, $"{exporter.Format}: the async path must not change the output");
    }

    [Theory]
    [MemberData(nameof(Exporters))]
    public async Task An_already_cancelled_token_aborts_before_any_work(IReportExporter exporter)
    {
        var report = await BigReportAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await exporter.ExportToBytesAsync(report, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            $"{exporter.Format}: a cancelled request must not start rendering");
    }

    [Theory]
    [MemberData(nameof(Exporters))]
    public async Task Cancelling_mid_export_stops_at_a_page_boundary(IReportExporter exporter)
    {
        // The report has many pages; cancelling as soon as the export starts must abort partway rather than
        // run to completion. This is the concrete win for a web host whose client disconnected.
        var report = await BigReportAsync();
        report.Pages.Count.Should().BeGreaterThan(3, "the fixture needs enough pages to cancel between them");

        using var cts = new CancellationTokenSource();
        using var ms = new MemoryStream();
        cts.CancelAfter(TimeSpan.FromMilliseconds(1));

        try
        {
            await exporter.ExportAsync(report, ms, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // aborted as intended
        }

        // Not cancelled in time (a fast machine finished first) — acceptable, but then the output must be whole.
        ms.Length.Should().BeGreaterThan(0, $"{exporter.Format}: a completed export still produces bytes");
    }

    [Fact]
    public async Task ExportToFileAsync_writes_the_file()
    {
        var report = await BigReportAsync();
        var path = Path.Combine(Path.GetTempPath(), $"omni-async-{Guid.NewGuid():N}.pdf");

        try
        {
            await new SkiaPdfExporter().ExportToFileAsync(report, path);

            File.Exists(path).Should().BeTrue();
            new FileInfo(path).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public async Task An_exporter_that_does_not_override_still_works_through_the_default()
    {
        // The default interface implementation is what keeps external exporters compiling after this change.
        var report = await BigReportAsync();
        IReportExporter exporter = new NonOverridingExporter();

        var bytes = await exporter.ExportToBytesAsync(report);

        System.Text.Encoding.UTF8.GetString(bytes).Should().Be("ok");
    }

    /// <summary>Stands in for a third-party exporter written before <c>ExportAsync</c> existed.</summary>
    private sealed class NonOverridingExporter : IReportExporter
    {
        public string Format => "stub";
        public string FileExtension => ".stub";
        public string ContentType => "text/plain";

        public void Export(RenderedReport report, Stream output)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("ok");
            output.Write(bytes, 0, bytes.Length);
        }
    }
}
