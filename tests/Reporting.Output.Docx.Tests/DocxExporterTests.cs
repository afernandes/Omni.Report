using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Reporting.Output.Docx;
using Reporting.Output.Pdf;
using Reporting.Samples.CodeFirst.Reports;
using Xunit;

namespace Reporting.Output.Docx.Tests;

public class DocxExporterTests
{
    private static IEnumerable<string> CellTexts(WordprocessingDocument doc) =>
        doc.MainDocumentPart!.Document.Body!.Descendants<TableCell>()
            .Select(c => c.InnerText);

    [Fact]
    public async Task Exports_sample01_vendas_into_a_word_table()
    {
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        var bytes = new DocxExporter().ExportToBytes(rendered);

        bytes.Should().NotBeEmpty();

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        // Exactly one table carries the grid.
        doc.MainDocumentPart!.Document.Body!.Descendants<Table>().Should().ContainSingle();

        var texts = CellTexts(doc).ToList();
        texts.Should().Contain(s => s.Contains("Produto"), "the column header is a table cell");
        texts.Should().Contain(s => s.Contains("Ana Beatriz"), "a detail value is a table cell");
    }

    [Fact]
    public async Task Generated_docx_is_schema_valid()
    {
        // The exporter's whole point is a file Word opens WITHOUT a repair prompt — assert real OOXML validity
        // (the InnerText/Bold assertions above pass even on a malformed-but-readable document).
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(rendered));
        using var doc = WordprocessingDocument.Open(ms, false);

        var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).ToList();
        errors.Should().BeEmpty(string.Join("\n", errors.Select(e => $"{e.Path?.XPath}: {e.Description}")));
    }

    [Fact]
    public async Task Header_row_cells_are_bold()
    {
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(rendered));
        using var doc = WordprocessingDocument.Open(ms, false);

        var firstRow = doc.MainDocumentPart!.Document.Body!.Descendants<Table>().First()
            .Elements<TableRow>().First();
        var headerCells = firstRow.Descendants<TableCell>()
            .Where(c => !string.IsNullOrWhiteSpace(c.InnerText))
            .ToList();
        headerCells.Should().NotBeEmpty("the header row has labelled columns");
        // Every non-empty header cell is bold.
        headerCells.Should().OnlyContain(c => c.Descendants<Bold>().Any(), "header cells are bold");
    }

    [Fact]
    public async Task Package_properties_are_set()
    {
        var rendered = await Sample02_EspelhoProdutos.Build().PaginateAsync();
        var bytes = new DocxExporter(new DocxExportOptions
        {
            Author = "Ana",
            Title = "Catálogo",
        }).ExportToBytes(rendered);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.PackageProperties.Title.Should().Be("Catálogo");
        doc.PackageProperties.Creator.Should().Be("Ana");
    }

    [Fact]
    public void Exporter_advertises_the_docx_format()
    {
        var x = new DocxExporter();
        x.Format.Should().Be("docx");
        x.FileExtension.Should().Be(".docx");
        x.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    // ── Inline images (camada 2, PR1) ─────────────────────────────────────────────

    // Minimal valid 1x1 PNG.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/QodAAAAAElFTkSuQmCC");
    // Minimal JPEG header bytes (FF D8 signature) + EOI; enough for the signature path + a valid image part.
    private static readonly byte[] TinyJpeg = { 0xFF, 0xD8, 0xFF, 0xD9 };

    private static Reporting.Layout.RenderedReport ReportWith(params Reporting.Layout.Primitives.LayoutPrimitive[] prims)
    {
        var page = new Reporting.Layout.RenderedPage(1, Reporting.Paper.PageSetup.A4Portrait,
            new Reporting.Common.EquatableArray<Reporting.Layout.Primitives.LayoutPrimitive>(prims));
        return new Reporting.Layout.RenderedReport("imgtest",
            new Reporting.Common.EquatableArray<Reporting.Layout.RenderedPage>(new[] { page }));
    }

    private static Reporting.Layout.Primitives.DrawImagePrimitive Image(byte[] bytes) => new()
    {
        Bounds = new Reporting.Geometry.Rectangle(
            Reporting.Geometry.Unit.FromMm(0), Reporting.Geometry.Unit.FromMm(0),
            Reporting.Geometry.Unit.FromMm(40), Reporting.Geometry.Unit.FromMm(30)),
        Data = new Reporting.Common.EquatableArray<byte>(bytes),
    };

    // ── Chart/visual rasterisation (camada 2, PR2) ────────────────────────────────

    // A line/area/pie chart emits a polygon; a BAR chart emits rectangles (no polygon). Both are flagged
    // IsVisual by the layout engine — so detection must key on IsVisual, NOT geometry.
    private static Reporting.Layout.Primitives.LayoutPrimitive ChartPolygon(string id) =>
        new Reporting.Layout.Primitives.DrawPolygonPrimitive
        {
            SourceElementId = id,
            IsVisual = true,
            Bounds = new Reporting.Geometry.Rectangle(
                Reporting.Geometry.Unit.FromMm(0), Reporting.Geometry.Unit.FromMm(0),
                Reporting.Geometry.Unit.FromMm(60), Reporting.Geometry.Unit.FromMm(40)),
            Points = new Reporting.Common.EquatableArray<Reporting.Geometry.Point>(new[]
            {
                new Reporting.Geometry.Point(Reporting.Geometry.Unit.FromMm(0), Reporting.Geometry.Unit.FromMm(40)),
                new Reporting.Geometry.Point(Reporting.Geometry.Unit.FromMm(20), Reporting.Geometry.Unit.FromMm(10)),
                new Reporting.Geometry.Point(Reporting.Geometry.Unit.FromMm(60), Reporting.Geometry.Unit.FromMm(40)),
            }),
            Fill = new Reporting.Rendering.BrushStyle(Reporting.Styling.Color.Blue),
        };

    private static Reporting.Layout.Primitives.LayoutPrimitive BarChartRect(string id) =>
        new Reporting.Layout.Primitives.DrawRectanglePrimitive
        {
            SourceElementId = id,
            IsVisual = true, // a bar — no polygon, but still a visual to rasterise
            Bounds = new Reporting.Geometry.Rectangle(
                Reporting.Geometry.Unit.FromMm(2), Reporting.Geometry.Unit.FromMm(10),
                Reporting.Geometry.Unit.FromMm(10), Reporting.Geometry.Unit.FromMm(30)),
            Fill = new Reporting.Rendering.BrushStyle(Reporting.Styling.Color.Blue),
        };

    private static Reporting.Layout.Primitives.LayoutPrimitive Text(string id, string text, double xMm, double yMm, bool visual = false) =>
        new Reporting.Layout.Primitives.DrawTextPrimitive
        {
            SourceElementId = id,
            IsVisual = visual,
            Text = text,
            Bounds = new Reporting.Geometry.Rectangle(
                Reporting.Geometry.Unit.FromMm(xMm), Reporting.Geometry.Unit.FromMm(yMm),
                Reporting.Geometry.Unit.FromMm(30), Reporting.Geometry.Unit.FromMm(6)),
            Style = Reporting.Rendering.TextStyle.Default,
        };

    [Fact]
    public void Chart_visual_is_rasterised_and_its_text_excluded_from_the_table()
    {
        // A chart = a SourceElementId group with a polygon + its own axis/legend text. It must rasterise to an
        // image and its text must NOT pollute the table; a normal cell's text stays in the table.
        var report = ReportWith(
            ChartPolygon("chart1"),
            Text("chart1", "EixoLabel", 0, 42),   // chart's own label — must NOT appear as a table row
            Text("cell", "ValorTabela", 0, 60));  // a normal table cell — must stay

        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(report));
        using var doc = WordprocessingDocument.Open(ms, false);

        doc.MainDocumentPart!.ImageParts.Should().ContainSingle("the chart rasterises to one PNG");
        var cellTexts = doc.MainDocumentPart.Document.Body!.Descendants<TableCell>().Select(c => c.InnerText).ToList();
        cellTexts.Should().Contain(s => s.Contains("ValorTabela"));
        cellTexts.Should().NotContain(s => s.Contains("EixoLabel"), "the chart's label is in the image, not the table");

        new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).Should().BeEmpty();
    }

    [Fact]
    public void Bar_chart_without_polygons_is_still_detected_via_IsVisual()
    {
        // Regression: keying detection on geometry (polygon) missed bar/scatter/stock charts. The IsVisual
        // flag covers a bar chart (rectangles only) — it rasterises and its label leaves the table.
        var report = ReportWith(
            BarChartRect("bar1"),
            Text("bar1", "BarLabel", 2, 42, visual: true),
            Text("cell", "ValorTabela", 0, 60));

        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(report));
        using var doc = WordprocessingDocument.Open(ms, false);

        doc.MainDocumentPart!.ImageParts.Should().ContainSingle("a polygon-free bar chart still rasterises");
        var cellTexts = doc.MainDocumentPart.Document.Body!.Descendants<TableCell>().Select(c => c.InnerText).ToList();
        cellTexts.Should().Contain(s => s.Contains("ValorTabela"));
        cellTexts.Should().NotContain(s => s.Contains("BarLabel"));
        new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).Should().BeEmpty();
    }

    [Fact]
    public void RasterizeVisuals_false_omits_the_chart()
    {
        var report = ReportWith(ChartPolygon("chart1"), Text("cell", "ValorTabela", 0, 60));
        var bytes = new DocxExporter(new DocxExportOptions { RasterizeVisuals = false }).ExportToBytes(report);
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.ImageParts.Should().BeEmpty("rasterisation disabled");
    }

    [Fact]
    public void Inline_png_image_is_embedded_as_an_image_part_and_drawing()
    {
        var bytes = new DocxExporter().ExportToBytes(ReportWith(Image(OnePixelPng)));
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        doc.MainDocumentPart!.ImageParts.Should().ContainSingle();
        var blip = doc.MainDocumentPart.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Single();
        blip.Embed!.Value.Should().NotBeNullOrEmpty();
        // The relationship id resolves to the embedded image part.
        doc.MainDocumentPart.GetPartById(blip.Embed!.Value!).Should().BeOfType<ImagePart>();
        // The inline drawing carries a positive extent within the page clamp.
        var extent = doc.MainDocumentPart.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();
        extent.Cx!.Value.Should().BeGreaterThan(0);
        extent.Cy!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Inline_image_keeps_the_docx_schema_valid()
    {
        // Two DISTINCT images (PNG + JPEG, so dedup doesn't collapse them) → two drawings with unique Ids.
        var bytes = new DocxExporter().ExportToBytes(ReportWith(Image(OnePixelPng), Image(TinyJpeg)));
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.ImageParts.Should().HaveCount(2);
        // Catches wrong CT_Inline child ordering and duplicate DocProperties Ids across the two images.
        var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).ToList();
        errors.Should().BeEmpty(string.Join("\n", errors.Select(e => $"{e.Path?.XPath}: {e.Description}")));
    }

    [Fact]
    public void Jpeg_bytes_use_a_jpeg_image_part()
    {
        var bytes = new DocxExporter().ExportToBytes(ReportWith(Image(TinyJpeg)));
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var part = doc.MainDocumentPart!.ImageParts.Single();
        part.ContentType.Should().Contain("jpeg");
    }

    [Fact]
    public void Bmp_bytes_use_a_bmp_part_not_a_mislabelled_png()
    {
        // Regression: arbitrary user/RDL bytes (BMP/GIF) must NOT be labelled image/png — Word embeds raw and
        // would render a broken image. The format is sniffed from the signature.
        var bmp = new byte[] { 0x42, 0x4D, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00 }; // "BM" + filler
        var bytes = new DocxExporter().ExportToBytes(ReportWith(Image(bmp)));
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.ImageParts.Single().ContentType.Should().Contain("bmp");
    }

    [Fact]
    public void Unsupported_format_is_skipped_not_corrupted()
    {
        // A format Word can't host raw (WebP "RIFF....WEBP") is dropped, not written as a corrupt png part.
        var webp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        var bytes = new DocxExporter().ExportToBytes(ReportWith(Image(webp)));
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.ImageParts.Should().BeEmpty();
        new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc.MainDocumentPart.Document)
            .Should().BeEmpty();
    }

    [Fact]
    public void Repeated_identical_image_is_emitted_once()
    {
        // A page-header logo present on every page must collapse to a single embedded image, not N copies.
        var logo = Image(OnePixelPng);
        var p1 = new Reporting.Layout.RenderedPage(1, Reporting.Paper.PageSetup.A4Portrait,
            new Reporting.Common.EquatableArray<Reporting.Layout.Primitives.LayoutPrimitive>(new[] { (Reporting.Layout.Primitives.LayoutPrimitive)logo }));
        var p2 = new Reporting.Layout.RenderedPage(2, Reporting.Paper.PageSetup.A4Portrait,
            new Reporting.Common.EquatableArray<Reporting.Layout.Primitives.LayoutPrimitive>(new[] { (Reporting.Layout.Primitives.LayoutPrimitive)logo }));
        var report = new Reporting.Layout.RenderedReport("dup",
            new Reporting.Common.EquatableArray<Reporting.Layout.RenderedPage>(new[] { p1, p2 }));

        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(report));
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.ImageParts.Should().ContainSingle("the identical logo is deduped across pages");
    }

    [Fact]
    public async Task Report_without_images_emits_no_drawing_and_stays_valid()
    {
        var rendered = await Sample01_VendasPorCliente.Build().PaginateAsync();
        using var ms = new MemoryStream(new DocxExporter().ExportToBytes(rendered));
        using var doc = WordprocessingDocument.Open(ms, false);
        doc.MainDocumentPart!.ImageParts.Should().BeEmpty();
        doc.MainDocumentPart.Document.Body!.Descendants<Drawing>().Should().BeEmpty();
    }
}
