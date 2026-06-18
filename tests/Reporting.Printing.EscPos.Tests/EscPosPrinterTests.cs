using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Printing;
using Reporting.Printing.EscPos;
using Reporting.Samples.CodeFirst.Reports;
using Xunit;

namespace Reporting.Printing.EscPos.Tests;

public class EscPosPrinterTests
{
    [Fact]
    public void Driver_identifier_is_esc_pos()
    {
        var p = new EscPosPrinter(new StreamEscPosTransport(new MemoryStream()));
        p.Driver.Should().Be("esc-pos");
    }

    [Fact]
    public async Task List_printers_returns_singleton_transport_entry()
    {
        var p = new EscPosPrinter(new StreamEscPosTransport(new MemoryStream()));
        var printers = await p.ListPrintersAsync();
        printers.Should().HaveCount(1);
        printers[0].Driver.Should().Be("esc-pos");
    }

    [Fact]
    public async Task Get_capabilities_returns_thermal_papers()
    {
        var p = new EscPosPrinter(new StreamEscPosTransport(new MemoryStream()));
        var caps = await p.GetCapabilitiesAsync("any");
        caps.SupportedPapers.Should().Contain(ps => ps.Name == "Thermal58" || ps.Name == "Thermal80");
        caps.SupportsDuplex.Should().BeFalse();
        caps.SupportsColor.Should().BeFalse();
    }

    [Fact]
    public async Task Cupom_nfce_print_emits_reset_then_raster_then_cut()
    {
        var report = await Sample04_CupomNfce.Build().PaginateAsync();
        using var buffer = new MemoryStream();
        var transport = new StreamEscPosTransport(buffer, leaveOpen: true);
        var printer = new EscPosPrinter(transport);

        var result = await printer.PrintAsync(report, new PrintOptions("esc-pos"));
        result.Succeeded.Should().BeTrue($"PrintAsync failed: {result.ErrorMessage}");
        result.PagesPrinted.Should().Be(report.Pages.Count);

        var bytes = buffer.ToArray();

        // Starts with ESC @ (reset).
        bytes.Take(2).Should().Equal([0x1B, (byte)'@']);

        // Contains at least one GS v 0 raster header (per page).
        bool hasRaster = false;
        for (int i = 0; i < bytes.Length - 3; i++)
        {
            if (bytes[i] == 0x1D && bytes[i + 1] == (byte)'v' && bytes[i + 2] == (byte)'0' && bytes[i + 3] == 0)
            {
                hasRaster = true;
                break;
            }
        }
        hasRaster.Should().BeTrue("the page must be encoded as a GS v 0 raster image");

        // Ends with a cut command: GS V 0 or GS V 65 n.
        var tail = bytes.Skip(bytes.Length - 4).ToArray();
        bool endsWithCut =
            (tail[^3] == 0x1D && tail[^2] == (byte)'V' && tail[^1] == 0x00) ||
            (tail[^4] == 0x1D && tail[^3] == (byte)'V' && tail[^2] == 65);
        endsWithCut.Should().BeTrue("the output must finish with a paper cut command");
    }

    [Fact]
    public async Task Raster_width_matches_80mm_dots_when_paper_is_thermal80()
    {
        // 80mm roll = 576 dots = 72 bytes wide.
        var report = await Sample04_CupomNfce.Build().PaginateAsync();
        using var buffer = new MemoryStream();
        var printer = new EscPosPrinter(new StreamEscPosTransport(buffer, leaveOpen: true));
        var result = await printer.PrintAsync(report, new PrintOptions("esc-pos"));
        result.Succeeded.Should().BeTrue();

        var bytes = buffer.ToArray();
        // Locate the first GS v 0 header.
        int idx = -1;
        for (int i = 0; i < bytes.Length - 7; i++)
        {
            if (bytes[i] == 0x1D && bytes[i + 1] == (byte)'v' && bytes[i + 2] == (byte)'0')
            {
                idx = i;
                break;
            }
        }
        idx.Should().BeGreaterThan(-1);
        int widthBytes = bytes[idx + 4] | (bytes[idx + 5] << 8);
        widthBytes.Should().Be(72, "80mm roll has 576 dots = 72 bytes/row");
    }

    [Fact]
    public async Task Print_failure_returns_print_result_failure()
    {
        var report = await Sample04_CupomNfce.Build().PaginateAsync();
        // ThrowingTransport simulates a broken connection.
        var printer = new EscPosPrinter(new ThrowingTransport());
        var result = await printer.PrintAsync(report, new PrintOptions("esc-pos"));
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boom");
    }

    [Fact]
    public async Task Forced_dot_width_overrides_auto_detection()
    {
        var report = await Sample04_CupomNfce.Build().PaginateAsync();
        using var buffer = new MemoryStream();
        var printer = new EscPosPrinter(
            new StreamEscPosTransport(buffer, leaveOpen: true),
            new EscPosPrinterOptions { ForcedDotWidth = 384 }); // 58mm roll
        var result = await printer.PrintAsync(report, new PrintOptions("esc-pos"));
        result.Succeeded.Should().BeTrue();
        var bytes = buffer.ToArray();
        int idx = Array.IndexOf(bytes, (byte)0x1D);
        // Find the first GS v 0 header that has 48 byte-width (384 / 8).
        bool found384 = false;
        for (int i = 0; i < bytes.Length - 7; i++)
        {
            if (bytes[i] == 0x1D && bytes[i + 1] == (byte)'v' && bytes[i + 2] == (byte)'0')
            {
                int widthBytes = bytes[i + 4] | (bytes[i + 5] << 8);
                if (widthBytes == 48)
                {
                    found384 = true;
                    break;
                }
            }
        }
        found384.Should().BeTrue();
    }

    [Fact]
    public async Task Feed_dots_before_cut_uses_GS_V_65()
    {
        var report = await Sample04_CupomNfce.Build().PaginateAsync();
        using var buffer = new MemoryStream();
        var printer = new EscPosPrinter(
            new StreamEscPosTransport(buffer, leaveOpen: true),
            new EscPosPrinterOptions { FeedDotsBeforeCut = 50 });
        await printer.PrintAsync(report, new PrintOptions("esc-pos"));
        var bytes = buffer.ToArray();
        // Last 4 bytes should be: GS V 65 50
        bytes[^4..].Should().Equal([0x1D, (byte)'V', 65, 50]);
    }

    private sealed class ThrowingTransport : IEscPosTransport
    {
        public string Name => "throwing";
        public Task SendAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
            => Task.FromException(new IOException("boom: transport broken"));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
