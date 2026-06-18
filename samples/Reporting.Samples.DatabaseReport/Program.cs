using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Reporting.CodeFirst;
using Reporting.DataSources.Sqlite;
using Reporting.Output.Csv;
using Reporting.Output.Excel;
using Reporting.Output.Html;
using Reporting.Output.Pdf;
using Reporting.Output.Svg;
using Reporting.Styling;

// End-to-end sample: a real (in-memory SQLite) database feeds a banded report.
// Demonstrates the complete pipeline:
//   1. Open a DbConnection (here SQLite — swap Npgsql/SqlClient/etc. with a one-line change).
//   2. Seed schema + data via plain SQL.
//   3. Bind a SqliteDataSource (streams DbDataReader rows) to a code-first report.
//   4. Pass report parameters into the SQL via the parameter dictionary (SQL injection-safe).
//   5. Paginate + export to PDF / HTML / XLSX / CSV / SVG.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

var outputDir = args.Length > 0 ? args[0] : "out";
Directory.CreateDirectory(outputDir);

Console.WriteLine($"OmniReport · Database sample → {Path.GetFullPath(outputDir)}");
Console.WriteLine();

// ─── 1. Boot SQLite in-memory + seed ────────────────────────────────────────────
// :memory: + a single open connection — each fresh connection to ':memory:' would
// see an empty DB. The data source borrows this same connection.
using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var seed = connection.CreateCommand())
{
    seed.CommandText = @"
        CREATE TABLE pedidos (
            id            INTEGER PRIMARY KEY,
            cliente       TEXT NOT NULL,
            produto       TEXT NOT NULL,
            quantidade    REAL NOT NULL,
            preco_unitario REAL NOT NULL,
            data          TEXT NOT NULL
        );

        INSERT INTO pedidos (id, cliente, produto, quantidade, preco_unitario, data) VALUES
            (1,  'Ana Beatriz',  'Caneta Bic Azul',       10, 2.50,  '2026-05-03'),
            (2,  'Ana Beatriz',  'Caderno Brochura',       1, 27.40, '2026-05-04'),
            (3,  'Ana Beatriz',  'Marcador de Texto',      3, 11.90, '2026-05-06'),
            (4,  'Beto Silva',   'Caneta Bic Vermelha',    5, 2.50,  '2026-05-07'),
            (5,  'Beto Silva',   'Borracha',               8, 1.20,  '2026-05-09'),
            (6,  'Carla Souza',  'Mochila Escolar',        1, 189.00,'2026-05-10'),
            (7,  'Carla Souza',  'Estojo',                 1, 35.50, '2026-05-11'),
            (8,  'Daniel Lima',  'Caderno Universitário',  2, 42.80, '2026-05-13'),
            (9,  'Daniel Lima',  'Lapiseira 0.7mm',        4, 18.90, '2026-05-14'),
            (10, 'Eva Pinto',    'Régua 30cm',             2, 5.40,  '2026-05-17');
    ";
    seed.ExecuteNonQuery();
}

Console.WriteLine("  ✓ Banco SQLite em memória criado e populado com 10 pedidos.");

// ─── 2. Bind the database to a code-first report ────────────────────────────────
var dataInicio = new DateTime(2026, 5, 1);
var dataFim    = new DateTime(2026, 5, 31);

// Parameterized SQL — $params come from the dictionary, never string-concatenated.
var query = @"
    SELECT cliente, produto, quantidade, preco_unitario,
           quantidade * preco_unitario AS total,
           data
      FROM pedidos
     WHERE data BETWEEN $dataInicio AND $dataFim
     ORDER BY cliente, data";

var vendasDataSource = new SqliteDataSource(
    name: "Vendas",
    openConnection: connection,
    sql: query,
    parameters: new Dictionary<string, object?>
    {
        ["$dataInicio"] = dataInicio.ToString("yyyy-MM-dd"),
        ["$dataFim"]    = dataFim.ToString("yyyy-MM-dd"),
    });

var report = ReportBuilder
    .Create("Vendas por Cliente (SQLite)")
    .Page(p => p.A4().Portrait().Margins(20))
    .Parameters(p => p
        .Add<DateTime>("DataInicio", prompt: "Data inicial", defaultValue: dataInicio)
        .Add<DateTime>("DataFim",    prompt: "Data final",   defaultValue: dataFim))
    // DataSource overload that takes any IReportDataSource — the SQLite streams here.
    .DataSource("Vendas", vendasDataSource)
    .ReportHeader(h => h.Height(30)
        .Text("Relatório de Vendas (origem: SQLite)")
            .At(0, 0).Size(170, 10).Font("Arial", 16, FontStyle.Bold).Center()
        .Text("Período: {Parameters.DataInicio:dd/MM/yyyy} a {Parameters.DataFim:dd/MM/yyyy}")
            .At(0, 12).Size(170, 6).Center()
        .Line().From(0, 22).To(170, 22).Thickness(0.5))
    .PageHeader(h => h.Height(8)
        .Label("Cliente / Produto").At(0, 0).Size(80, 6).Bold()
        .Label("Qtd").At(82, 0).Size(20, 6).Bold().AlignRight()
        .Label("Preço Unit.").At(104, 0).Size(30, 6).Bold().AlignRight()
        .Label("Total").At(136, 0).Size(34, 6).Bold().AlignRight()
        .Line().From(0, 6).To(170, 6).Thickness(0.25))
    .Group("PorCliente", "Fields.cliente", g => g
        .Header(h => h.Height(8)
            .Text("Cliente: {Fields.cliente}").At(0, 1).Size(170, 6)
                .Font("Arial", 11, FontStyle.Bold)
                .Color(Color.FromHex("#C2410C")))
        .Detail(d => d.Height(6)
            .Text("{Fields.produto}").At(2, 0).Size(78, 6)
            .Text("{Fields.quantidade:N2}").At(82, 0).Size(20, 6).AlignRight()
            .Text("{Fields.preco_unitario:C}").At(104, 0).Size(30, 6).AlignRight()
            .Text("{Fields.total:C}").At(136, 0).Size(34, 6).AlignRight())
        .Footer(f => f.Height(8)
            .Line().From(0, 0).To(170, 0).Thickness(0.25)
            .Text("Subtotal {Fields.cliente}: {Sum(Fields.total, 'Group'):C}")
                .At(0, 1).Size(170, 6).AlignRight().Bold()))
    .ReportFooter(f => f.Height(15)
        .Line().From(0, 0).To(170, 0).Thickness(0.5)
        .Text("Total geral: {Sum(Fields.total):C}")
            .At(0, 2).Size(170, 10).Bold().AlignRight().Font("Arial", 11, FontStyle.Bold))
    .PageFooter(f => f.Height(8)
        .Text("Página {Page.Number} de {Page.Total}")
            .At(0, 1).Size(170, 6).AlignRight().Color(Color.Gray))
    .Build();

Console.WriteLine("  ✓ Report code-first construído, bindado à fonte SQLite.");

// ─── 3. Paginate + export ───────────────────────────────────────────────────────
var sw = Stopwatch.StartNew();
var rendered = await report.PaginateAsync();
sw.Stop();
Console.WriteLine($"  ✓ Paginação: {rendered.Pages.Count} pág(s) · {sw.ElapsedMilliseconds} ms");

var name = "vendas-sqlite";
var pdfPath  = Path.Combine(outputDir, name + ".pdf");
var htmlPath = Path.Combine(outputDir, name + ".html");
var xlsxPath = Path.Combine(outputDir, name + ".xlsx");
var csvPath  = Path.Combine(outputDir, name + ".csv");
var svgPath  = Path.Combine(outputDir, name + ".svg");

new SkiaPdfExporter(new PdfExportOptions { Title = report.Definition.Name }).ExportToFile(rendered, pdfPath);
new SvgHtmlExporter(new HtmlExportOptions { Title = report.Definition.Name }).ExportToFile(rendered, htmlPath);
new ExcelExporter(new ExcelExportOptions { Title = report.Definition.Name }).ExportToFile(rendered, xlsxPath);
new CsvExporter().ExportToFile(rendered, csvPath);
new SvgExporter(new SvgExportOptions { Title = report.Definition.Name }).ExportToFile(rendered, svgPath);

Console.WriteLine();
Console.WriteLine("  Artefatos gerados:");
Console.WriteLine($"    pdf:  {pdfPath}");
Console.WriteLine($"    html: {htmlPath}");
Console.WriteLine($"    xlsx: {xlsxPath}");
Console.WriteLine($"    csv:  {csvPath}");
Console.WriteLine($"    svg:  {svgPath}");
Console.WriteLine();
Console.WriteLine("Concluído.");
