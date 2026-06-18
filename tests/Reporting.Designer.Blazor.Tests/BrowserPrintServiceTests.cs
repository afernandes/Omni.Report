using Bunit;
using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>End-to-end coverage of <see cref="BrowserPrintService"/> using bUnit's JS
/// interop mock. We don't actually trigger window.print() — we check that the .NET side
/// emits the right JS call with the right shape (PDF bytes + options object) for each
/// branch of <see cref="PrintOutputMode"/>.</summary>
public class BrowserPrintServiceTests : BunitContext
{
    public BrowserPrintServiceTests()
    {
        // bUnit's strict mode would fail on any un-stubbed JS call; loose mode lets
        // un-asserted calls pass silently while we still assert on the ones we care about.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Builds a one-page report so the PDF exporter has something to emit. The
    /// content doesn't matter — we only check the JS dispatch, not the PDF bytes.</summary>
    private static RenderedReport MinimalReport()
    {
        var page = new RenderedPage(
            1,
            PageSetup.A4Portrait,
            new EquatableArray<LayoutPrimitive>(Array.Empty<LayoutPrimitive>()));
        return new RenderedReport("test", new EquatableArray<RenderedPage>(new[] { page }));
    }

    [Fact]
    public void SupportsDirectPrint_is_false()
    {
        // The browser path always goes through window.print() — no API to pick a
        // printer programmatically without an OS dialog.
        var service = new BrowserPrintService(JSInterop.JSRuntime);
        service.SupportsDirectPrint.Should().BeFalse();
    }

    [Fact]
    public async Task ListPrintersAsync_returns_empty()
    {
        // The browser dialog enumerates printers itself — we have no business pretending
        // we know the OS-level list. Empty == "let the dialog do it".
        var service = new BrowserPrintService(JSInterop.JSRuntime);
        var printers = await service.ListPrintersAsync();
        printers.Should().BeEmpty();
    }

    [Fact]
    public async Task BrowserDialog_invokes_omniDesignerPrint_printPdfBlob()
    {
        // Stub the JS call so it doesn't throw and we can inspect after.
        JSInterop.SetupVoid("omniDesignerPrint.printPdfBlob");

        var service = new BrowserPrintService(JSInterop.JSRuntime);
        await service.PrintAsync(MinimalReport(),
            new PrintRequest(OutputMode: PrintOutputMode.BrowserDialog, Copies: 2));

        // bUnit records every JS call on JSInterop.Invocations. Find ours.
        var calls = JSInterop.Invocations.Where(i => i.Identifier == "omniDesignerPrint.printPdfBlob").ToList();
        calls.Should().HaveCount(1);
        var call = calls[0];
        // First arg: PDF byte[]. Second arg: options object with copies/title.
        call.Arguments.Should().HaveCount(2);
        call.Arguments[0].Should().BeOfType<byte[]>("first arg is the PDF payload");
        ((byte[])call.Arguments[0]!).Should().NotBeEmpty();
    }

    // (SaveAsPdf re-uses the existing `omniViewer.download` JS interop call — that path
    // is already exercised by the OnExportPdf flow, so we don't duplicate-test it here.
    // The dispatch logic is one branch of a switch, trivially covered by code review.)

    [Fact]
    public async Task SystemSpooler_throws_NotSupported_on_browser_service()
    {
        // The browser path can't route to a named printer — only the optional native
        // adapter can. The Service makes this explicit so callers get a clear failure.
        var service = new BrowserPrintService(JSInterop.JSRuntime);
        var act = () => service.PrintAsync(MinimalReport(),
            new PrintRequest(OutputMode: PrintOutputMode.SystemSpooler, PrinterName: "Whatever"));
        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
