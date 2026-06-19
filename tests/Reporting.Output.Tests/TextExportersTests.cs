using System.Text;
using System.Text.Json;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Output.Csv;
using Reporting.Output.Html;
using Reporting.Output.Json;
using Reporting.Output.Markdown;
using Reporting.Output.Pdf;
using Reporting.Output.Svg;
using Xunit;

namespace Reporting.Output.Tests;

public sealed record Linha(string Nome, string Valor);
public sealed record ChartRow(string Mes, decimal Total);

/// <summary>
/// Coverage for the five text exporters that previously had none — SVG, HTML, CSV, JSON,
/// Markdown. Verifies the shared <see cref="IReportExporter"/> contract, each format's
/// structural invariants (RFC 4180 quoting, GFM tables, valid JSON, embedded SVG + print CSS),
/// and that chart geometry survives into the vector/structured formats.
/// </summary>
public class TextExportersTests
{
    // ── Shared report fixtures ───────────────────────────────────────────────────

    private static Report BuildGridReport()
    {
        // Values chosen to exercise CSV/Markdown escaping: a comma, a quote, and a pipe.
        var rows = new[]
        {
            new Linha("Ana, Beatriz", "1200"),
            new Linha("Aspas\"X", "50"),
            new Linha("Pipe|Z", "30"),
        };
        return ReportBuilder.Create("Relatório Teste")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", rows)
            .PageHeader(h => h.Height(8)
                .Label("Nome").At(0, 0).Size(60, 6)
                .Label("Valor").At(62, 0).Size(40, 6))
            .Detail(d => d.Height(6)
                .Text("{Fields.Nome}").At(0, 0).Size(60, 6)
                .Text("{Fields.Valor}").At(62, 0).Size(40, 6))
            .Build();
    }

    private static Report BuildChartReport(ChartKind kind)
    {
        var rows = new[]
        {
            new ChartRow("Jan", 100m),
            new ChartRow("Fev", 250m),
            new ChartRow("Mar", 175m),
        };
        return ReportBuilder.Create("Gráfico")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", rows)
            .ReportHeader(h => h.Height(80)
                .Chart(kind, "Vendas").At(0, 0).Size(170, 75)
                    .Series("Total", "Fields.Mes", "Fields.Total"))
            .Build();
    }

    private static async Task<string> ExportToStringAsync(IReportExporter exporter, Report report)
    {
        var rendered = await report.PaginateAsync();
        using var ms = new MemoryStream();
        exporter.Export(rendered, ms);
        return new UTF8Encoding(false).GetString(ms.ToArray());
    }

    // ── Contract ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task All_text_exporters_produce_output_and_sane_metadata()
    {
        var report = await BuildGridReport().PaginateAsync();
        IReportExporter[] exporters =
        [
            new SvgExporter(), new SvgHtmlExporter(), new CsvExporter(),
            new JsonExporter(), new MarkdownExporter(),
        ];

        foreach (var e in exporters)
        {
            e.Format.Should().NotBeNullOrWhiteSpace();
            e.FileExtension.Should().StartWith(".");
            e.ContentType.Should().NotBeNullOrWhiteSpace();

            using var ms = new MemoryStream();
            e.Export(report, ms);
            ms.Length.Should().BeGreaterThan(0, "{0} must emit content", e.Format);
        }
    }

    // ── SVG ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Svg_is_wellformed_svg_document()
    {
        var svg = await ExportToStringAsync(new SvgExporter(), BuildGridReport());
        svg.Should().Contain("<svg");
        svg.Should().Contain("</svg>");
    }

    [Fact]
    public async Task Svg_renders_chart_path_geometry()
    {
        var svg = await ExportToStringAsync(new SvgExporter(), BuildChartReport(ChartKind.Pie));
        // Pie wedges are emitted as DrawPolygon → DrawPath → an SVG <path>.
        svg.Should().Contain("<path");
    }

    [Fact]
    public async Task Svg_has_explicit_viewbox()
    {
        var svg = await ExportToStringAsync(new SvgExporter(), BuildGridReport());
        svg.Should().Contain("viewBox=", "an explicit viewBox lets CSS scale the SVG without distortion");
    }

    // ── HTML ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Html_is_document_with_embedded_svg_and_print_css()
    {
        var html = await ExportToStringAsync(new SvgHtmlExporter(), BuildGridReport());
        html.Should().Contain("<html");
        html.Should().Contain("<svg");
        html.ToLowerInvariant().Should().Contain("print", "print CSS rules drive paged output");
    }

    // ── CSV ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Csv_quotes_fields_per_rfc4180()
    {
        var csv = await ExportToStringAsync(
            new CsvExporter(new CsvExportOptions { IncludeBom = false }), BuildGridReport());

        csv.Should().Contain("\"Ana, Beatriz\"", "a value with the delimiter must be quoted");
        csv.Should().Contain("\"Aspas\"\"X\"", "an embedded quote must be doubled and the field quoted");
        csv.Should().Contain("Pipe|Z", "a pipe is not special in CSV → left unquoted");
        csv.Should().Contain("Nome");
    }

    [Fact]
    public async Task Csv_bom_is_toggleable()
    {
        var rendered = await BuildGridReport().PaginateAsync();

        using var withBom = new MemoryStream();
        new CsvExporter(new CsvExportOptions { IncludeBom = true }).Export(rendered, withBom);
        withBom.ToArray()[..3].Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF });

        using var noBom = new MemoryStream();
        new CsvExporter(new CsvExportOptions { IncludeBom = false }).Export(rendered, noBom);
        noBom.ToArray()[0].Should().NotBe(0xEF);
    }

    [Fact]
    public async Task Csv_honours_custom_delimiter()
    {
        // With ';' as the delimiter, a comma is no longer special — "Ana, Beatriz" stays unquoted.
        var csv = await ExportToStringAsync(
            new CsvExporter(new CsvExportOptions { IncludeBom = false, Delimiter = ';' }), BuildGridReport());

        csv.Should().Contain(";", "the configured delimiter must separate fields");
        csv.Should().Contain("Ana, Beatriz", "a comma is not the delimiter → the field is not quoted");
        csv.Should().NotContain("\"Ana, Beatriz\"");
    }

    // ── JSON ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Json_is_valid_with_stable_schema()
    {
        var json = await ExportToStringAsync(new JsonExporter(), BuildGridReport());

        using var doc = JsonDocument.Parse(json); // throws if malformed
        var root = doc.RootElement;
        root.GetProperty("name").GetString().Should().Be("Relatório Teste");
        var pageCount = root.GetProperty("pageCount").GetInt32();
        pageCount.Should().BeGreaterThan(0);
        root.GetProperty("pages").GetArrayLength().Should().Be(pageCount);
        json.Should().Contain("\"type\": \"text\"");
        json.Should().Contain("Ana, Beatriz");
    }

    [Fact]
    public async Task Json_serializes_chart_polygons()
    {
        var json = await ExportToStringAsync(new JsonExporter(), BuildChartReport(ChartKind.Pie));
        json.Should().Contain("\"type\": \"polygon\"", "pie wedges must serialize to the polygon schema");
    }

    // ── Markdown ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Markdown_has_heading_table_and_escapes_pipes()
    {
        var md = await ExportToStringAsync(
            new MarkdownExporter(new MarkdownExportOptions { IncludeFrontMatter = false }), BuildGridReport());

        md.Should().Contain("# Relatório Teste");
        md.Should().Contain("| --- |", "a GFM table needs the alignment separator row");
        md.Should().Contain("Pipe\\|Z", "a pipe inside a cell must be escaped");
    }

    [Fact]
    public async Task Markdown_emits_yaml_front_matter_when_enabled()
    {
        var md = await ExportToStringAsync(
            new MarkdownExporter(new MarkdownExportOptions { IncludeFrontMatter = true }), BuildGridReport());

        md.Should().StartWith("---");
        md.Should().Contain("title:");
    }
}
