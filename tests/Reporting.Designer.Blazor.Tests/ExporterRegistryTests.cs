using FluentAssertions;
using Reporting.Designer.Blazor.Services;
using Reporting.Layout;
using Reporting.Output.Pdf;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Locks in the contract of <see cref="ExporterRegistry"/>: it must auto-wrap raw
/// <see cref="IReportExporter"/> registrations with sensible label/icon/order defaults
/// for the seven first-party formats, must let explicit <see cref="ExporterDescriptor"/>
/// registrations override the defaults (so hosts can customise label/icon without
/// re-implementing the exporter), must sort by Order, and must look up by format
/// case-insensitively. This is the surface the PreviewMode toolbar binds against, so
/// the behaviour needs to be stable.
/// </summary>
public class ExporterRegistryTests
{
    [Fact]
    public void Auto_wraps_known_pdf_with_primary_and_lowest_order()
    {
        var pdf = new FakeExporter("pdf", ".pdf", "application/pdf");

        var registry = new ExporterRegistry(
            explicitDescriptors: Array.Empty<ExporterDescriptor>(),
            rawExporters: new[] { pdf });

        registry.Descriptors.Should().HaveCount(1);
        var d = registry.Descriptors[0];
        d.Label.Should().Be("PDF");
        d.IsPrimary.Should().BeTrue();
        d.IconName.Should().Be("file-text");
        d.Order.Should().Be(10);
    }

    [Theory]
    [InlineData("pdf", "PDF", "file-text", 10, true)]
    [InlineData("xlsx", "Excel", "file-spreadsheet", 20, false)]
    [InlineData("html", "HTML", "file-code", 30, false)]
    [InlineData("svg", "SVG", "file-code", 40, false)]
    [InlineData("csv", "CSV", "file", 50, false)]
    [InlineData("json", "JSON", "file", 60, false)]
    [InlineData("markdown", "Markdown", "file", 70, false)]
    public void Auto_wraps_each_known_format_with_expected_defaults(
        string format, string expectedLabel, string expectedIcon, int expectedOrder, bool expectedPrimary)
    {
        var exporter = new FakeExporter(format, "." + format, "application/" + format);

        var registry = new ExporterRegistry(
            explicitDescriptors: Array.Empty<ExporterDescriptor>(),
            rawExporters: new[] { exporter });

        var d = registry.Descriptors.Single();
        d.Label.Should().Be(expectedLabel);
        d.IconName.Should().Be(expectedIcon);
        d.Order.Should().Be(expectedOrder);
        d.IsPrimary.Should().Be(expectedPrimary);
    }

    [Fact]
    public void Unknown_format_gets_generic_icon_and_high_order_so_it_sorts_last()
    {
        var weird = new FakeExporter("rtf", ".rtf", "application/rtf");

        var registry = new ExporterRegistry(
            explicitDescriptors: Array.Empty<ExporterDescriptor>(),
            rawExporters: new[] { weird });

        var d = registry.Descriptors.Single();
        d.Label.Should().Be("RTF");
        d.IconName.Should().Be("file");
        d.Order.Should().Be(1000);
    }

    [Fact]
    public void Descriptors_are_sorted_by_order()
    {
        // Register out of order to prove the registry sorts.
        var registry = new ExporterRegistry(
            explicitDescriptors: Array.Empty<ExporterDescriptor>(),
            rawExporters: new IReportExporter[]
            {
                new FakeExporter("markdown", ".md", "text/markdown"),
                new FakeExporter("pdf", ".pdf", "application/pdf"),
                new FakeExporter("csv", ".csv", "text/csv"),
                new FakeExporter("xlsx", ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            });

        registry.Descriptors.Select(d => d.Format)
            .Should().Equal("pdf", "xlsx", "csv", "markdown");
    }

    [Fact]
    public void Explicit_descriptor_wins_over_auto_wrapped_for_same_format()
    {
        var pdf = new FakeExporter("pdf", ".pdf", "application/pdf");
        var customLabel = new ExporterDescriptor(
            pdf, Label: "Documento", Description: "PDF customizado",
            IconName: "star", Order: 5, IsPrimary: false);

        var registry = new ExporterRegistry(
            explicitDescriptors: new[] { customLabel },
            rawExporters: new[] { pdf });

        registry.Descriptors.Should().HaveCount(1);
        var d = registry.Descriptors[0];
        d.Label.Should().Be("Documento");
        d.IconName.Should().Be("star");
        d.IsPrimary.Should().BeFalse();
        d.Order.Should().Be(5);
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        var pdf = new FakeExporter("pdf", ".pdf", "application/pdf");
        var registry = new ExporterRegistry(
            explicitDescriptors: Array.Empty<ExporterDescriptor>(),
            rawExporters: new[] { pdf });

        registry.Find("pdf").Should().NotBeNull();
        registry.Find("PDF").Should().NotBeNull();
        registry.Find("Pdf").Should().NotBeNull();
    }

    [Fact]
    public void Find_returns_null_for_unknown_format()
    {
        var registry = new ExporterRegistry(
            explicitDescriptors: Array.Empty<ExporterDescriptor>(),
            rawExporters: new[] { new FakeExporter("pdf", ".pdf", "application/pdf") });

        registry.Find("xlsx").Should().BeNull();
        registry.Find("").Should().BeNull();
    }

    [Fact]
    public void Empty_registry_renders_no_buttons()
    {
        // Hosts that don't call AddDefaultExporters get an empty registry — the
        // PreviewMode toolbar binds against Descriptors.Count > 0 and hides the
        // export group entirely. This test pins that contract.
        var registry = new ExporterRegistry(
            explicitDescriptors: Array.Empty<ExporterDescriptor>(),
            rawExporters: Array.Empty<IReportExporter>());

        registry.Descriptors.Should().BeEmpty();
        registry.Find("pdf").Should().BeNull();
    }

    /// <summary>Minimal stand-in for an <see cref="IReportExporter"/> — the registry
    /// only reads <c>Format</c> for the auto-wrap switch, so <c>Export</c> is a no-op.</summary>
    private sealed class FakeExporter : IReportExporter
    {
        public FakeExporter(string format, string ext, string contentType)
        {
            Format = format;
            FileExtension = ext;
            ContentType = contentType;
        }

        public string Format { get; }
        public string FileExtension { get; }
        public string ContentType { get; }

        public void Export(RenderedReport report, Stream output) { /* no-op for tests */ }
    }
}
