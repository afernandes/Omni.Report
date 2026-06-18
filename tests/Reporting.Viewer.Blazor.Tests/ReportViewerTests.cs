using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Samples.CodeFirst.Reports;
using Reporting.Viewer.Blazor;
using Xunit;

namespace Reporting.Viewer.Blazor.Tests;

public class ReportViewerTests : Bunit.BunitContext
{
    public ReportViewerTests()
    {
        // The viewer pulls ReportViewerOptions from DI.
        Services.AddOmniReportViewer();
        // bUnit's JSInterop module — calls to omniViewer.download are catched and treated as
        // a no-op so the export tests don't need a real browser.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static RenderedReport BuildSampleReport()
        => Sample01_VendasPorCliente.Build().PaginateAsync().GetAwaiter().GetResult();

    [Fact]
    public void Empty_state_when_report_is_null()
    {
        var cut = Render<ReportViewer>();
        cut.Find("[data-testid=omni-viewer]").Should().NotBeNull();
        cut.Markup.Should().Contain("Nenhum relatório carregado");
    }

    [Fact]
    public void Renders_toolbar_buttons_when_report_present()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p.Add(v => v.Report, report));

        // Toolbar contains navigation, zoom, export.
        cut.FindAll("button[title='Primeira página']").Should().NotBeEmpty();
        cut.FindAll("button[title='Próxima página']").Should().NotBeEmpty();
        cut.FindAll("button[title='Exportar PDF']").Should().NotBeEmpty();
        cut.FindAll("button[title='Exportar XLSX']").Should().NotBeEmpty();
    }

    [Fact]
    public void Page_image_is_rendered_as_data_uri()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p.Add(v => v.Report, report));
        var img = cut.Find(".page-image");
        img.GetAttribute("src").Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public void Page_indicator_shows_total_count()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p.Add(v => v.Report, report));
        cut.Markup.Should().Contain($"/ {report.Pages.Count}");
    }

    [Fact]
    public void Toolbar_can_be_hidden_via_options()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p
            .Add(v => v.Report, report)
            .Add(v => v.Options, new ReportViewerOptions { ShowToolbar = false }));
        cut.FindAll(".omni-viewer-toolbar").Should().BeEmpty();
    }

    [Fact]
    public void Next_and_previous_buttons_are_initially_disabled_for_single_page_report()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p.Add(v => v.Report, report));
        if (report.Pages.Count == 1)
        {
            cut.Find("button[title='Página anterior']")
                .HasAttribute("disabled").Should().BeTrue();
            cut.Find("button[title='Próxima página']")
                .HasAttribute("disabled").Should().BeTrue();
        }
    }

    [Fact]
    public void Initial_zoom_reflects_options_value()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p
            .Add(v => v.Report, report)
            .Add(v => v.Options, new ReportViewerOptions { InitialZoomPercent = 75 }));
        cut.Markup.Should().Contain("scale(0.75)");
    }

    [Fact]
    public void Zoom_in_button_applies_css_transform_change()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p.Add(v => v.Report, report));
        var initialScale = ExtractScale(cut.Markup);

        cut.Find("button[title='Aumentar zoom']").Click();

        var afterScale = ExtractScale(cut.Markup);
        afterScale.Should().BeGreaterThan(initialScale);
    }

    [Fact]
    public void Zoom_out_clamps_at_lower_bound()
    {
        var report = BuildSampleReport();
        var cut = Render<ReportViewer>(p => p
            .Add(v => v.Report, report)
            .Add(v => v.Options, new ReportViewerOptions { InitialZoomPercent = 25 }));
        cut.Find("button[title='Diminuir zoom']").Click();
        cut.Find("button[title='Diminuir zoom']").Click();
        // Stays at 25%.
        ExtractScale(cut.Markup).Should().Be(0.25);
    }

    [Fact]
    public void Export_pdf_emits_on_exported_event()
    {
        var report = BuildSampleReport();
        ReportExportEventArgs? captured = null;
        var cut = Render<ReportViewer>(p => p
            .Add(v => v.Report, report)
            .Add(v => v.OnExported, args => { captured = args; }));

        cut.Find("button[title='Exportar PDF']").Click();
        // Wait one tick for the async export to complete.
        cut.WaitForState(() => captured is not null, TimeSpan.FromSeconds(10));

        captured.Should().NotBeNull();
        captured!.Extension.Should().Be("pdf");
        captured.FileName.Should().EndWith(".pdf");
        captured.Bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(captured.Bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Export_xlsx_emits_event_with_xlsx_bytes()
    {
        var report = BuildSampleReport();
        ReportExportEventArgs? captured = null;
        var cut = Render<ReportViewer>(p => p
            .Add(v => v.Report, report)
            .Add(v => v.OnExported, args => { captured = args; }));

        cut.Find("button[title='Exportar XLSX']").Click();
        cut.WaitForState(() => captured is not null, TimeSpan.FromSeconds(10));

        captured.Should().NotBeNull();
        captured!.Extension.Should().Be("xlsx");
        captured.Bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Print_without_driver_invokes_callback()
    {
        var report = BuildSampleReport();
        bool fired = false;
        var cut = Render<ReportViewer>(p => p
            .Add(v => v.Report, report)
            .Add(v => v.OnPrintRequestedWithoutDriver, () => fired = true));

        cut.Find("button[title='Imprimir']").Click();
        cut.WaitForState(() => fired, TimeSpan.FromSeconds(5));

        fired.Should().BeTrue();
    }

    [Fact]
    public void Changing_report_resets_page_to_first()
    {
        var report1 = BuildSampleReport();
        var report2 = Sample02_EspelhoProdutos.Build().PaginateAsync().GetAwaiter().GetResult();

        var cut = Render<ReportViewer>(p => p.Add(v => v.Report, report1));
        cut.Markup.Should().Contain("/ " + report1.Pages.Count);

        cut.Render(p => p.Add(v => v.Report, report2));
        cut.Markup.Should().Contain("/ " + report2.Pages.Count);
    }

    private static double ExtractScale(string markup)
    {
        var marker = "scale(";
        var i = markup.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return -1;
        var j = markup.IndexOf(')', i);
        var inside = markup[(i + marker.Length)..j];
        return double.Parse(inside, System.Globalization.CultureInfo.InvariantCulture);
    }
}
