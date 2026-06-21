using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Layout.Tabular;
using Reporting.Output.Pdf;

namespace Reporting.Output.Docx;

/// <summary>
/// Exports a <see cref="RenderedReport"/> to an editable Word <c>.docx</c> document using DocumentFormat.OpenXml.
/// </summary>
/// <remarks>
/// <para>Strategy (mirrors the XLSX exporter): the paginated report is quantized into a row/column grid by
/// <see cref="LayoutPrimitiveGrid"/>, then emitted as a single bordered Word table — one
/// <see cref="TableRow"/> per grid row, one <see cref="TableCell"/> per column. Row styling follows the
/// detected <see cref="RowKind"/> (header shaded + bold, group header coloured, subtotal/total bold).</para>
/// <para>This is the flow/tabular tier — the output is a clean, editable table honouring each cell's
/// already-formatted text (currency/percent come pre-formatted in the primitive). Absolute pixel-perfect
/// positioning (text boxes) and embedded images/charts are deferred follow-ups.</para>
/// </remarks>
public sealed class DocxExporter : IReportExporter
{
    private const string GroupHeaderColor = "C2410C"; // matches the XLSX exporter
    private const string HeaderShade = "D9D9D9";

    private readonly DocxExportOptions _options;

    public DocxExporter(DocxExportOptions? options = null)
    {
        _options = options ?? DocxExportOptions.Default;
    }

    public string Format => "docx";
    public string FileExtension => ".docx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        using var doc = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document);
        doc.PackageProperties.Title = _options.Title ?? report.Name;
        if (_options.Author is not null)
        {
            doc.PackageProperties.Creator = _options.Author;
        }

        var main = doc.AddMainDocumentPart();
        main.Document = new Document();
        var body = main.Document.AppendChild(new Body());

        if (_options.IncludeHeading && !string.IsNullOrWhiteSpace(report.Name))
        {
            body.AppendChild(Heading(report.Name));
        }

        // Visual elements (charts/gauges/sparklines/indicators/data bars/barcodes/maps) are VECTOR primitives
        // the Word table can't hold — the layout engine flags them with LayoutPrimitive.IsVisual. Each such
        // element (grouped by SourceElementId) is rasterised to a PNG below AND excluded from the table grid so
        // its axis/legend text isn't duplicated as stray rows. Using the explicit flag (not geometry sniffing)
        // correctly covers bar charts (rectangles) and avoids mistaking a Tablix's border rects for a visual.
        var visualIds = _options.RasterizeVisuals ? CollectVisualElementIds(report) : new HashSet<string>();

        var gridReport = visualIds.Count == 0 ? report : WithoutPrimitives(report, visualIds);
        var grid = LayoutPrimitiveGrid.Build(gridReport);
        if (grid.Rows.Count > 0)
        {
            body.AppendChild(BuildTable(grid));
        }

        // Inline raster images (logo/photo/signature) — emitted as standalone paragraphs after the table.
        // Dedupe by exact bytes so a repeating page-header/footer logo collapses to a single image.
        var seen = new HashSet<Reporting.Common.EquatableArray<byte>>();
        uint drawingId = 1;
        foreach (var page in report.Pages)
        {
            foreach (var img in page.Primitives.OfType<DrawImagePrimitive>())
            {
                if (img.Data.Count > 0 && seen.Add(img.Data)
                    && DocxImageWriter.AppendInlineImage(main, body, img, drawingId))
                {
                    drawingId++;
                }
            }
        }

        // Rasterise each visual element's region to a PNG and embed it (reuses the inline-image writer).
        if (visualIds.Count > 0)
        {
            foreach (var (region, prims) in VisualRegions(report, visualIds))
            {
                var png = Reporting.Output.Image.RegionRasterizer.RenderRegionPng(prims, region, _options.RasterizeDpi);
                var synthetic = new DrawImagePrimitive
                {
                    Bounds = region,
                    Data = new Reporting.Common.EquatableArray<byte>(png),
                    Sizing = Reporting.Elements.ImageSizing.Native,
                };
                if (DocxImageWriter.AppendInlineImage(main, body, synthetic, drawingId))
                {
                    drawingId++;
                }
            }
        }

        main.Document.Save();
    }

    // A "visual" element id = a SourceElementId whose primitives are flagged IsVisual by the layout engine
    // (chart/gauge/sparkline/indicator/data bar/barcode/map). Used to both exclude them from the text grid
    // and rasterise them — independent of which geometry a given chart type happens to emit.
    private static HashSet<string> CollectVisualElementIds(RenderedReport report)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in report.Pages)
        {
            foreach (var p in page.Primitives)
            {
                if (p.IsVisual && p.SourceElementId is { Length: > 0 } id)
                {
                    ids.Add(id);
                }
            }
        }
        return ids;
    }

    private static RenderedReport WithoutPrimitives(RenderedReport report, HashSet<string> excludedIds)
    {
        var pages = report.Pages.Select(pg => new RenderedPage(
            pg.PageNumber,
            pg.PageSetup,
            new Reporting.Common.EquatableArray<LayoutPrimitive>(
                pg.Primitives.Where(p => p.SourceElementId is not { } id || !excludedIds.Contains(id)).ToArray())))
            .ToArray();
        return new RenderedReport(report.Name, new Reporting.Common.EquatableArray<RenderedPage>(pages));
    }

    // One entry per visual element: its union bounds (the rasterisation region) + the primitives to draw.
    private static IEnumerable<(Reporting.Geometry.Rectangle Region, List<LayoutPrimitive> Prims)> VisualRegions(
        RenderedReport report, HashSet<string> visualIds)
    {
        foreach (var page in report.Pages)
        {
            foreach (var group in page.Primitives
                .Where(p => p.SourceElementId is { } id && visualIds.Contains(id))
                .GroupBy(p => p.SourceElementId!))
            {
                var prims = group.ToList();
                yield return (UnionBounds(prims), prims);
            }
        }
    }

    private static Reporting.Geometry.Rectangle UnionBounds(List<LayoutPrimitive> prims)
    {
        var minX = prims.Min(p => p.Bounds.X);
        var minY = prims.Min(p => p.Bounds.Y);
        var maxX = prims.Max(p => p.Bounds.Right);
        var maxY = prims.Max(p => p.Bounds.Bottom);
        return new Reporting.Geometry.Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    // ── Table writer ──────────────────────────────────────────────────────────────

    private static Table BuildTable(LayoutPrimitiveGrid grid)
    {
        int columnCount = grid.ColumnCount;

        var table = new Table();
        // CT_TblPr child order is fixed: tblW BEFORE tblBorders; and CT_TblBorders order is
        // top, left, bottom, right, insideH, insideV. The SDK serializes in append order (no reordering),
        // so getting this wrong yields a .docx Word must "repair". Validated with OpenXmlValidator.
        table.AppendChild(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, // 100% of the page width
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        // CT_Tbl requires a <tblGrid> declaring the columns, right after <tblPr>.
        var tableGrid = new TableGrid();
        for (int c = 0; c < columnCount; c++)
        {
            tableGrid.AppendChild(new GridColumn());
        }
        table.AppendChild(tableGrid);

        foreach (var gridRow in grid.Rows)
        {
            var tr = new TableRow();
            for (int col = 0; col < columnCount; col++)
            {
                gridRow.Cells.TryGetValue(col, out var text);
                tr.AppendChild(BuildCell(text ?? string.Empty, gridRow.Kind));
            }
            table.AppendChild(tr);
        }
        return table;
    }

    private static TableCell BuildCell(string text, RowKind kind)
    {
        var runProps = new RunProperties();
        if (kind is RowKind.Header or RowKind.GroupHeader or RowKind.Subtotal or RowKind.Total)
        {
            runProps.AppendChild(new Bold());
        }
        if (kind == RowKind.GroupHeader)
        {
            runProps.AppendChild(new Color { Val = GroupHeaderColor });
        }

        var run = new Run();
        run.AppendChild(runProps);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        // Right-align numeric detail cells so figures line up (header/total text stays left).
        var paraProps = new ParagraphProperties();
        if (kind == RowKind.Detail && LayoutPrimitiveGrid.LooksLikeNumber(text))
        {
            paraProps.AppendChild(new Justification { Val = JustificationValues.Right });
        }
        var paragraph = new Paragraph(paraProps, run);

        var cell = new TableCell();
        if (kind == RowKind.Header)
        {
            cell.AppendChild(new TableCellProperties(
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = HeaderShade }));
        }
        // A Word table cell MUST contain at least one block-level element (paragraph).
        cell.AppendChild(paragraph);
        return cell;
    }

    private static Paragraph Heading(string text)
    {
        var runProps = new RunProperties(new Bold(), new FontSize { Val = "28" }); // 14pt (half-points)
        var run = new Run();
        run.AppendChild(runProps);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }
}
