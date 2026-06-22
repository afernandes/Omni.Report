// ─────────────────────────────────────────────────────────────────────────────────────────
// Exemplo: LER (importar) um relatório SSRS .rdl, paginar com dados e exportar.
//
//   1. RdlImporter().Import(stream)  →  ReportDefinition (modelo nativo do OmniReport)
//   2. inspeciona a estrutura importada + os avisos de perda (Metadata["ImportWarnings"])
//   3. fornece os dados que o .rdl referencia (DataSet "Vendas") via fonte in-memory
//   4. ReportPaginator().PaginateAsync(...)  →  RenderedReport
//   5. exporta para PDF + HTML
//
// Uso:  dotnet run --project samples/Reporting.Samples.RdlImport -- [caminho-do.rdl] [pasta-saida]
// ─────────────────────────────────────────────────────────────────────────────────────────
using Reporting;                       // ReportDefinition
using Reporting.Bands;                 // IBand
using Reporting.DataSources;           // DataSourceRegistry
using Reporting.DataSources.Enumerable; // EnumerableDataSource<T>
using Reporting.Elements;              // ReportElement
using Reporting.Layout;                // ReportPaginator, PaginationRequest
using Reporting.Output.Html;           // SvgHtmlExporter
using Reporting.Output.Pdf;            // SkiaPdfExporter, PdfExportOptions
using Reporting.Serialization;         // RdlImporter

var rdlPath = args.Length > 0 ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "sample-report.rdl");
var outDir = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "out");
Directory.CreateDirectory(outDir);

if (!File.Exists(rdlPath))
{
    Console.Error.WriteLine($"Arquivo .rdl não encontrado: {rdlPath}");
    return 1;
}

// ── 1. Importar o .rdl ──────────────────────────────────────────────────────────────────
Console.WriteLine($"Lendo  {rdlPath}");
ReportDefinition def;
using (var stream = File.OpenRead(rdlPath))
{
    def = new RdlImporter().Import(stream, reportName: Path.GetFileNameWithoutExtension(rdlPath));
}
// Alternativa a partir de uma string XML:  new RdlImporter().ImportXml(File.ReadAllText(rdlPath));

// ── 2. Inspecionar a estrutura importada + avisos ───────────────────────────────────────
Console.WriteLine($"\nRelatório importado: \"{def.Name}\"");
Console.WriteLine($"  Página     : {def.PageSetup.PageWidth.ToMm():0}×{def.PageSetup.PageHeight.ToMm():0} mm");
if (def.Metadata.TryGetValue("Language", out var lang)) Console.WriteLine($"  Cultura    : {lang}");
if (def.Metadata.TryGetValue("Description", out var desc)) Console.WriteLine($"  Descrição  : {desc}");
Console.WriteLine($"  Parâmetros : {def.Parameters.Count}");

Console.WriteLine("  Bandas     :");
foreach (var (name, band) in Bands(def))
{
    if (band is null || band.Elements.Count == 0) continue;
    var kinds = string.Join(", ", band.Elements.Select(e => e.GetType().Name.Replace("Element", "")));
    Console.WriteLine($"    • {name,-13} {band.Elements.Count} elemento(s): {kinds}");
}

// Perdas de importação (nunca descarte silencioso): RdlImporter as coleta em Metadata["ImportWarnings"].
if (def.Metadata.TryGetValue("ImportWarnings", out var warnings) && !string.IsNullOrEmpty(warnings))
{
    Console.WriteLine("\n  ⚠ Avisos de importação:");
    foreach (var w in warnings.Split(" | ")) Console.WriteLine($"    - {w}");
}
else
{
    Console.WriteLine("\n  ✓ Sem perdas de importação.");
}

// ── 3. Fornecer os dados que o .rdl referencia ──────────────────────────────────────────
var vendas = new[]
{
    new Venda("Ana Souza",    "Caneta esferográfica", 25.00m),
    new Venda("Ana Souza",    "Caderno universitário", 27.40m),
    new Venda("Bruno Lima",   "Lápis 2B",             10.80m),
    new Venda("Bruno Lima",   "Borracha branca",       4.50m),
    new Venda("Carla Dias",   "Mochila escolar",     159.90m),
};
var registry = new DataSourceRegistry();
registry.Register(new EnumerableDataSource<Venda>("Vendas", vendas));

// ── 4. Paginar o relatório importado com os dados ───────────────────────────────────────
var rendered = await new ReportPaginator().PaginateAsync(new PaginationRequest
{
    Definition = def,
    DataSources = registry,
    PrimaryDataSource = "Vendas",
});

// ── 5. Exportar ─────────────────────────────────────────────────────────────────────────
var pdf = Path.Combine(outDir, "vendas-importado.pdf");
var html = Path.Combine(outDir, "vendas-importado.html");
new SkiaPdfExporter(new PdfExportOptions { Title = def.Name }).ExportToFile(rendered, pdf);
new SvgHtmlExporter(new HtmlExportOptions { Title = def.Name }).ExportToFile(rendered, html);

Console.WriteLine($"\nGerado: {rendered.Pages.Count} página(s)");
Console.WriteLine($"  PDF : {pdf}");
Console.WriteLine($"  HTML: {html}");
return 0;

// Lista nomeada das bandas de um ReportDefinition (para o resumo acima).
static IEnumerable<(string Name, IBand? Band)> Bands(ReportDefinition d)
{
    yield return ("ReportHeader", d.ReportHeader);
    yield return ("PageHeader", d.PageHeader);
    foreach (var g in d.Groups)
    {
        yield return ($"Group:{g.Name}", g);
    }
    yield return ("Detail", d.Detail);
    yield return ("PageFooter", d.PageFooter);
    yield return ("ReportFooter", d.ReportFooter);
}

// Linha de detalhe que o .rdl espera (DataSet "Vendas" com Cliente/Produto/Total).
record Venda(string Cliente, string Produto, decimal Total);
