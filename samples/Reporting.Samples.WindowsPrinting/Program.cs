using System.Globalization;
using System.Runtime.Versioning;
using Reporting.CodeFirst;
using Reporting.Printing;
using Reporting.Printing.WindowsSpooler;
using Reporting.Samples.CodeFirst.Reports;

[assembly: SupportedOSPlatform("windows")]

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

// Usage:
//   dotnet run -- list                                                     → list printers
//   dotnet run -- "Microsoft Print to PDF" out\sample01.pdf                → print sample 1 to PDF
//   dotnet run -- "Microsoft XPS Document Writer" out\sample01.xps         → print sample 1 to XPS
//   dotnet run -- "EPSON LX-300 (Cópia 1)"                                 → real device

var printer = new WindowsSpoolerPrinter();

if (args.Length == 0 || string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
{
    var printers = await printer.ListPrintersAsync();
    Console.WriteLine($"Impressoras instaladas ({printers.Count}):");
    foreach (var p in printers)
    {
        var def = p.IsDefault ? " (padrão)" : string.Empty;
        Console.WriteLine($"  • {p.Name}{def} · status: {p.Status ?? "n/a"}");
    }
    return;
}

var printerName = args[0];
var outputFile = args.Length > 1 ? args[1] : null;

var report = await Sample01_VendasPorCliente.Build().PaginateAsync();
Console.WriteLine($"Imprimindo '{report.Name}' ({report.Pages.Count} página(s)) em '{printerName}'...");
if (outputFile is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile))!);
    Console.WriteLine($"  → redirecionando para arquivo: {outputFile}");
}

var result = await printer.PrintAsync(report, new PrintOptions(printerName)
{
    DocumentName = "OmniReport · Vendas por Cliente",
    OutputFile = outputFile,
    Copies = 1,
});

if (result.Succeeded)
{
    Console.WriteLine($"  ✓ {result.PagesPrinted} página(s) enviadas com sucesso.");
    if (result.OutputPath is not null)
    {
        Console.WriteLine($"  arquivo: {Path.GetFullPath(result.OutputPath)}");
    }
}
else
{
    Console.WriteLine($"  ✗ Falha: {result.ErrorMessage}");
    Environment.ExitCode = 1;
}
