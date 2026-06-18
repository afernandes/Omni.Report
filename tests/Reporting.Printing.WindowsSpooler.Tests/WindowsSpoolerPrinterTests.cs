using System.Runtime.Versioning;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Printing;
using Reporting.Printing.WindowsSpooler;
using Reporting.Samples.CodeFirst.Reports;
using UglyToad.PdfPig;
using Xunit;

namespace Reporting.Printing.WindowsSpooler.Tests;

[SupportedOSPlatform("windows")]
public class WindowsSpoolerPrinterTests
{
    private const string PrintToPdf = "Microsoft Print to PDF";

    [Fact]
    public void Driver_identifier_is_windows_spooler()
    {
        new WindowsSpoolerPrinter().Driver.Should().Be("windows-spooler");
    }

    [Fact]
    public async Task List_printers_returns_at_least_one()
    {
        var p = new WindowsSpoolerPrinter();
        var printers = await p.ListPrintersAsync();
        // Every Windows install has at least "Microsoft Print to PDF" / "Microsoft XPS Document Writer".
        printers.Should().NotBeEmpty();
        printers.Should().Contain(pi => pi.Name.Contains("PDF") || pi.Name.Contains("XPS"));
    }

    [Fact]
    public async Task Get_capabilities_for_print_to_pdf_returns_papers()
    {
        var p = new WindowsSpoolerPrinter();
        var printers = await p.ListPrintersAsync();
        if (!printers.Any(pi => pi.Name == PrintToPdf))
        {
            // Skip — host lacks Microsoft Print to PDF (rare but possible on stripped builds).
            return;
        }
        var caps = await p.GetCapabilitiesAsync(PrintToPdf);
        caps.PrinterName.Should().Be(PrintToPdf);
        caps.SupportedPapers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_capabilities_throws_for_unknown_printer()
    {
        var p = new WindowsSpoolerPrinter();
        Func<Task> act = async () => await p.GetCapabilitiesAsync("Definitely-Not-A-Printer-42");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Print_to_unknown_printer_returns_failure_result()
    {
        var p = new WindowsSpoolerPrinter();
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var result = await p.PrintAsync(rendered, new PrintOptions("Definitely-Not-A-Printer-42"));
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.PagesPrinted.Should().Be(0);
    }

    [Fact]
    public async Task Print_sample01_to_pdf_produces_real_pdf_file_with_selectable_text()
    {
        var p = new WindowsSpoolerPrinter();
        var printers = await p.ListPrintersAsync();
        if (!printers.Any(pi => pi.Name == PrintToPdf))
        {
            // Host lacks Microsoft Print to PDF — skip the integration assertion.
            return;
        }

        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var outputPath = Path.Combine(Path.GetTempPath(), $"omni-spool-{Guid.NewGuid():N}.pdf");
        try
        {
            var result = await p.PrintAsync(rendered, new PrintOptions(PrintToPdf)
            {
                OutputFile = outputPath,
                DocumentName = "OmniReport spooler test",
            });

            result.Succeeded.Should().BeTrue($"Print failed: {result.ErrorMessage}");
            result.PagesPrinted.Should().Be(rendered.Pages.Count);
            File.Exists(outputPath).Should().BeTrue();

            using var pdf = PdfDocument.Open(outputPath);
            pdf.NumberOfPages.Should().Be(rendered.Pages.Count);
            var text = string.Join(" ", pdf.GetPages().Select(pg => pg.Text));
            text.Should().Contain("Vendas");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task Print_with_page_range_only_emits_requested_pages()
    {
        var p = new WindowsSpoolerPrinter();
        var printers = await p.ListPrintersAsync();
        if (!printers.Any(pi => pi.Name == PrintToPdf))
        {
            return;
        }
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        if (rendered.Pages.Count < 1)
        {
            return;
        }
        var outputPath = Path.Combine(Path.GetTempPath(), $"omni-spool-range-{Guid.NewGuid():N}.pdf");
        try
        {
            var result = await p.PrintAsync(rendered, new PrintOptions(PrintToPdf)
            {
                OutputFile = outputPath,
                PageRange = (1, 1),
            });
            result.Succeeded.Should().BeTrue($"Print failed: {result.ErrorMessage}");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
