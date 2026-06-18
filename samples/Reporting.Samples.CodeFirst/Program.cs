using System.Diagnostics;
using System.Globalization;
using Reporting.CodeFirst;
using Reporting.Expressions.Roslyn;
using Reporting.Layout;
using Reporting.Output.Csv;
using Reporting.Output.Excel;
using Reporting.Output.Html;
using Reporting.Output.Json;
using Reporting.Output.Markdown;
using Reporting.Output.Pdf;
using Reporting.Output.Svg;
using Reporting.Printing;
using Reporting.Printing.EscPos;
using Reporting.Rendering.Skia;
using Reporting.Samples.CodeFirst.Reports;
using Reporting.Serialization;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

// Register the bundled vector map shapes so Sample14's Map can resolve ShapeSet("brazil").
Reporting.Maps.MapShapes.RegisterBuiltIns();

var outputDir = args.Length > 0 ? args[0] : "out";
Directory.CreateDirectory(outputDir);

Console.WriteLine($"OmniReport · gerando samples em: {Path.GetFullPath(outputDir)}");
Console.WriteLine();

var samples = new (string Name, Func<Report> Build)[]
{
    ("01-vendas-por-cliente",  () => Sample01_VendasPorCliente.Build()),
    ("02-espelho-produtos",    () => Sample02_EspelhoProdutos.Build()),
    ("03-relatorio-caixa",     () => Sample03_RelatorioCaixa.Build()),
    ("04-cupom-nfce",          () => Sample04_CupomNfce.Build()),
    // My-FyiReporting-style data providers — each sample reads from a different source.
    ("06-json-pedidos",        () => Sample06_JsonPedidos.Build()),
    ("07-xml-rss-feed",        () => Sample07_XmlRssFeed.Build()),
    ("08-webservice-api",      () => Sample08_WebServiceApi.Build()),
    ("09-filesystem-logs",     () => Sample09_FileSystemLogs.Build()),
    // ADO.NET / SQLite — canonical database-backed report path. Demonstrates the same
    // streaming pipeline used by the production-grade Postgres/SqlServer/MySql providers.
    ("11-adonet-sqlite",       () => Sample11_AdoNetSqlite.Build()),
    // Novos controles visuais: gráficos (barras/linhas/pizza), KPIs (gauge/data-bar/sparkline/
    // indicator), Tablix e Map.
    ("12-dashboard",           () => Sample12_Dashboard.Build()),
    ("13-tabela-produtos",     () => Sample13_TabelaProdutos.Build()),
    ("14-mapa-filiais",        () => Sample14_MapaFiliais.Build()),
};

foreach (var (name, build) in samples)
{
    var sw = Stopwatch.StartNew();
    var report = build();
    var rendered = await report.PaginateAsync();

    // PNG: rasterized snapshot of page 1 (handy for visual diffs / READMEs)
    using var renderer = new SkiaRenderingContext();
    RenderedReportPlayer.Play(rendered, renderer);

    var pngPath = Path.Combine(outputDir, name + "-page1.png");
    var pdfPath = Path.Combine(outputDir, name + ".pdf");
    var xlsxPath = Path.Combine(outputDir, name + ".xlsx");
    var htmlPath = Path.Combine(outputDir, name + ".html");
    var svgPath = Path.Combine(outputDir, name + ".svg");
    var csvPath = Path.Combine(outputDir, name + ".csv");
    var mdPath = Path.Combine(outputDir, name + ".md");
    var jsonPath = Path.Combine(outputDir, name + ".json");
    var repxPath = Path.Combine(outputDir, name + ".repx");
    var repJsonPath = Path.Combine(outputDir, name + ".repjson");

    File.WriteAllBytes(pngPath, renderer.GetPagePng(0));

    // Vector-native PDF (text remains selectable; preferred over the rasterized variant).
    new SkiaPdfExporter(new PdfExportOptions
    {
        Author = "OmniReport",
        Subject = report.Definition.Name,
        Creator = "OmniReport Samples",
    }).ExportToFile(rendered, pdfPath);

    // XLSX with live =SUM() formulas for subtotal/total rows.
    new ExcelExporter(new ExcelExportOptions
    {
        Author = "OmniReport",
        Title = report.Definition.Name,
    }).ExportToFile(rendered, xlsxPath);

    // Vector SVG: single composite document with all pages stacked. Open directly in
    // any browser; text remains selectable.
    new SvgExporter(new SvgExportOptions
    {
        Title = report.Definition.Name,
    }).ExportToFile(rendered, svgPath);

    // HTML wraps the same SVG exporter — one <svg> per page inside an HTML envelope.
    new SvgHtmlExporter(new HtmlExportOptions
    {
        Title = report.Definition.Name,
    }).ExportToFile(rendered, htmlPath);

    // CSV — RFC 4180, UTF-8 BOM for Excel pt-BR, invariant-culture decimal in detail rows.
    new CsvExporter().ExportToFile(rendered, csvPath);

    // Markdown — GitHub-flavored, group headers promoted to H2.
    new MarkdownExporter(new MarkdownExportOptions
    {
        Title = report.Definition.Name,
    }).ExportToFile(rendered, mdPath);

    // JSON — structured dump of every primitive; mm units by default.
    new JsonExporter().ExportToFile(rendered, jsonPath);

    // Serialization (round-trippable XML + JSON)
    new RepxSerializer().SaveToFile(report.Definition, repxPath);
    new RepJsonSerializer().SaveToFile(report.Definition, repJsonPath);

    // Cupom térmico: também dumpa os bytes ESC/POS gerados (sem hardware) para inspeção.
    string? escposPath = null;
    if (report.Definition.PageSetup.IsContinuous)
    {
        escposPath = Path.Combine(outputDir, name + ".escpos.bin");
        await using var fs = File.Create(escposPath);
        await using var transport = new StreamEscPosTransport(fs, leaveOpen: true);
        var escposPrinter = new EscPosPrinter(transport);
        var escposResult = await escposPrinter.PrintAsync(rendered, new PrintOptions("esc-pos"));
        if (!escposResult.Succeeded)
        {
            Console.WriteLine($"    ⚠ ESC/POS dump falhou: {escposResult.ErrorMessage}");
            escposPath = null;
        }
    }

    sw.Stop();
    Console.WriteLine($"  ✓ {name} · {rendered.Pages.Count} pág(s) · {sw.ElapsedMilliseconds} ms");
    Console.WriteLine($"      png:     {pngPath}");
    Console.WriteLine($"      pdf:     {pdfPath}");
    Console.WriteLine($"      xlsx:    {xlsxPath}");
    Console.WriteLine($"      svg:     {svgPath}");
    Console.WriteLine($"      html:    {htmlPath}");
    Console.WriteLine($"      csv:     {csvPath}");
    Console.WriteLine($"      md:      {mdPath}");
    Console.WriteLine($"      json:    {jsonPath}");
    Console.WriteLine($"      repx:    {repxPath}");
    Console.WriteLine($"      repjson: {repJsonPath}");
    if (escposPath is not null)
    {
        Console.WriteLine($"      escpos:  {escposPath}");
    }
}
// ─── Sample 05: canvas low-level ─────────────────────────────────────────────
// Different code path from the others — it doesn't build a `Report` domain model
// nor run the paginator. The sample drives the renderer's IRenderingContext /
// ITextMeasurer interfaces directly, drawing each primitive (text, line, path,
// rectangle) with explicit coordinates. See Sample05_CanvasLowLevel.cs for the why.
//
// The exact same Render(ctx, measurer) call is issued twice against two different
// IRenderingContext implementations:
//   • SkiaRenderingContext       → raster bitmaps → PNG snapshots per page.
//   • SkiaPdfRenderingContext    → SKDocument-backed → vector PDF with selectable text.
{
    var name = "05-canvas-low-level";
    var sw = Stopwatch.StartNew();

    // PNGs (raster snapshot of each page, handy for visual diffs / READMEs).
    using var raster = new SkiaRenderingContext();
    Sample05_CanvasLowLevel.Render(raster, raster);  // same instance is ITextMeasurer
    var png1Path = Path.Combine(outputDir, name + "-page1.png");
    var png2Path = Path.Combine(outputDir, name + "-page2.png");
    File.WriteAllBytes(png1Path, raster.GetPagePng(0));
    File.WriteAllBytes(png2Path, raster.GetPagePng(1));

    // Vector PDF: text remains selectable, shapes stay as paths. The drawing code
    // in Sample05_CanvasLowLevel.Render is unchanged — only the IRenderingContext
    // backend differs.
    var pdfPath = Path.Combine(outputDir, name + ".pdf");
    using (var pdfStream = File.Create(pdfPath))
    using (var pdf = new Reporting.Rendering.Skia.SkiaPdfRenderingContext(pdfStream, new SkiaSharp.SKDocumentPdfMetadata
    {
        Title = "OmniReport · Canvas Low-Level",
        Author = "OmniReport",
        Creator = "OmniReport Samples",
    }, leaveOpen: true))
    {
        Sample05_CanvasLowLevel.Render(pdf, pdf);
    }

    // XLSX via RecordingRenderingContext: a third backend that buffers each draw
    // call into a RenderedReport, which ExcelExporter then quantizes into a cell
    // grid (and auto-detects subtotal/total rows for live =SUM() formulas).
    // Text measurement is delegated to the raster context already at hand.
    var xlsxPath = Path.Combine(outputDir, name + ".xlsx");
    var recorder = new Reporting.Layout.RecordingRenderingContext(raster);
    Sample05_CanvasLowLevel.Render(recorder, recorder);
    var renderedFromCanvas = recorder.ToRenderedReport("canvas-low-level");
    new ExcelExporter(new ExcelExportOptions
    {
        Author = "OmniReport",
        Title = "Canvas Low-Level",
    }).ExportToFile(renderedFromCanvas, xlsxPath);

    // SVG (standalone, all pages stacked) + HTML (envelope around the SVG per page).
    var svgPath = Path.Combine(outputDir, name + ".svg");
    new SvgExporter(new SvgExportOptions { Title = "Canvas Low-Level" })
        .ExportToFile(renderedFromCanvas, svgPath);

    var htmlPath = Path.Combine(outputDir, name + ".html");
    new SvgHtmlExporter(new HtmlExportOptions { Title = "Canvas Low-Level" })
        .ExportToFile(renderedFromCanvas, htmlPath);

    var csvPath = Path.Combine(outputDir, name + ".csv");
    new CsvExporter().ExportToFile(renderedFromCanvas, csvPath);
    var mdPath = Path.Combine(outputDir, name + ".md");
    new MarkdownExporter(new MarkdownExportOptions { Title = "Canvas Low-Level" })
        .ExportToFile(renderedFromCanvas, mdPath);
    var jsonPath = Path.Combine(outputDir, name + ".json");
    new JsonExporter().ExportToFile(renderedFromCanvas, jsonPath);

    sw.Stop();
    Console.WriteLine($"  ✓ {name} · {raster.Pages.Count} pág(s) · {sw.ElapsedMilliseconds} ms");
    Console.WriteLine($"      png1: {png1Path}");
    Console.WriteLine($"      png2: {png2Path}");
    Console.WriteLine($"      pdf:  {pdfPath}");
    Console.WriteLine($"      xlsx: {xlsxPath}");
    Console.WriteLine($"      svg:  {svgPath}");
    Console.WriteLine($"      html: {htmlPath}");
    Console.WriteLine($"      csv:  {csvPath}");
    Console.WriteLine($"      md:   {mdPath}");
    Console.WriteLine($"      json: {jsonPath}");
}

// ─── Sample 10: low-level providers (no ReportBuilder) ──────────────────────
// Dumps the first three rows of every IReportDataSource to the console + a text
// file. Useful to verify the providers are wired correctly without going through
// the band/render pipeline. The SAME provider instances would be the data behind
// Sample06..Sample09 — this just bypasses the report layer.
{
    var name = "10-low-level-providers";
    var txtPath = Path.Combine(outputDir, name + ".txt");
    await using var fileWriter = new StreamWriter(txtPath);
    await Sample10_LowLevelProviders.RunAsync(fileWriter);
    Console.WriteLine($"  ✓ {name} · dump dos 4 providers · {txtPath}");
    // Also dump to the console so a developer running the sample sees the shapes
    // immediately, without having to open the file.
    Console.WriteLine();
    await Sample10_LowLevelProviders.RunAsync(Console.Out);
}

// ─── Sample 15: custom C# Code via the opt-in Roslyn package ────────────────
// Code.X(...) in expressions is resolved by compiling Sample 15's CodeSource with Roslyn and
// wiring the resolver into the PaginationRequest. The core engine carries no code-execution
// dependency; this opt-in path executes embedded C# — only use with trusted report sources.
{
    var name = "15-codigo-customizado";
    var sw = Stopwatch.StartNew();
    var report = Sample15_CodigoCustomizado.Build();
    var paginator = new ReportPaginator();
    var request = new PaginationRequest
    {
        Definition = report.Definition,
        DataSources = report.DataSources,
        CodeFunctionResolver = RoslynCode.CreateResolver(Sample15_CodigoCustomizado.CodeSource),
    };
    var rendered = await paginator.PaginateAsync(request);

    using var renderer = new SkiaRenderingContext();
    RenderedReportPlayer.Play(rendered, renderer);
    var pngPath = Path.Combine(outputDir, name + "-page1.png");
    File.WriteAllBytes(pngPath, renderer.GetPagePng(0));

    var pdfPath = Path.Combine(outputDir, name + ".pdf");
    new SkiaPdfExporter(new PdfExportOptions { Author = "OmniReport", Subject = report.Definition.Name, Creator = "OmniReport Samples" })
        .ExportToFile(rendered, pdfPath);

    var htmlPath = Path.Combine(outputDir, name + ".html");
    new SvgHtmlExporter(new HtmlExportOptions { Title = report.Definition.Name }).ExportToFile(rendered, htmlPath);

    var repxPath = Path.Combine(outputDir, name + ".repx");
    new RepxSerializer().SaveToFile(report.Definition, repxPath);

    sw.Stop();
    Console.WriteLine($"  ✓ {name} · {rendered.Pages.Count} pág(s) · {sw.ElapsedMilliseconds} ms (Code/Roslyn opt-in)");
    Console.WriteLine($"      png:  {pngPath}");
    Console.WriteLine($"      pdf:  {pdfPath}");
    Console.WriteLine($"      html: {htmlPath}");
    Console.WriteLine($"      repx: {repxPath}");
}

Console.WriteLine();
Console.WriteLine("Concluído.");
