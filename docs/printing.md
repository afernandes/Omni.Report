# Printing

OmniReport separa **rendering** de **printing**. O paginator produz primitivos
vetoriais (`LayoutPrimitive`); cada `IReportPrinter` consome esses primitivos e fala
com o subsistema nativo de impressão da plataforma.

## Interface

```csharp
public interface IReportPrinter
{
    string Name { get; }
    IReadOnlyList<PrinterInfo> ListPrinters();
    PrinterCapabilities GetCapabilities(string printerName);
    Task<PrintResult> PrintAsync(
        ReportDefinition report,
        IReportDataSource? dataSource,
        ReportExpressionContext? context,
        PrintOptions options,
        CancellationToken ct = default);
}
```

`PrintOptions` cobre `PrinterName`, `PaperSize`, `Duplex`, `Copies`, `PrinterBin`,
`PrintRange`.

## Implementações disponíveis

### 1. Windows Spooler (`Reporting.Printing.WindowsSpooler`)

TFM `net10.0-windows`. Usa `System.Drawing.Printing.PrintDocument`. Para cada página,
instancia um `GdiRenderingContext` que envolve o `Graphics` do `PrintDocument` —
**texto fica vetorial no spool** (não rasterizado), preservando qualidade em
impressoras de alta resolução.

```csharp
services.AddSingleton<IReportPrinter,
    Reporting.Printing.WindowsSpooler.WindowsSpoolerPrinter>();

var printer = sp.GetRequiredService<IReportPrinter>();
foreach (var info in printer.ListPrinters())
{
    Console.WriteLine($"{info.Name} — {info.Status}");
}

await printer.PrintAsync(report, vendas, ctx, new PrintOptions
{
    PrinterName = "HP LaserJet M404n",
    Copies = 2,
    Duplex = DuplexMode.Vertical,
});
```

### 2. ESC/POS térmica (`Reporting.Printing.EscPos`)

Cross-plat. Renderiza cada página em raster Skia 203 dpi, converte em comandos
`GS v 0` (raster bit image). Cobre cabeçalho, texto, QR code (via comando ESC/POS
nativo quando suportado pelo modelo), corte de papel.

Transportes suportados:

```csharp
// TCP (impressora de rede):
var transport = new TcpEscPosTransport("192.168.1.50", 9100);

// Serial COM (USB virtual):
var transport = new SerialEscPosTransport("COM3", baudRate: 9600);

// Stream genérico (arquivo, mock, etc):
var transport = new StreamEscPosTransport(File.Create("out.escpos.bin"));

var printer = new EscPosPrinter(transport, paperWidthMm: 80);
await printer.PrintAsync(cupomDef, dataSource, ctx, PrintOptions.Default);
```

Largura de papel: `58` ou `80` mm (default 80). Cálculo de pixels: `dpi × mm / 25.4`.

### 3. Android Print Framework (`Reporting.Printing.Android`)

Dual mode:

- **Stub** (`net10.0`): lança `PlatformNotSupportedException`. Permite consumir a
  package em apps multi-TFM sem ifdef.
- **Real** (`net10.0-android`): habilitado por `OMNIREPORT_BUILD_ANDROID=true` no
  csproj. Usa `Android.Print.PrintManager` + `PrintDocumentAdapter`. Páginas são
  renderizadas em PDF in-memory (SkiaPdfExporter) e entregues ao framework.

```csharp
#if ANDROID
builder.Services.AddSingleton<IReportPrinter>(sp =>
    new Reporting.Printing.Android.AndroidPrintFrameworkPrinter(
        global::Android.App.Application.Context));
#endif
```

## Arquitetura: por que não rasterizar para o spooler?

Rotear PDF/PNG ao spooler funciona, mas:

- Texto vira pixels → bordas serrilhadas em laser de 1200 dpi
- Cores chapadas viram dithering desnecessário
- Spool grande (relatório de 20 páginas raster ≈ 200 MB)

`WindowsSpoolerPrinter` replica primitivos em GDI direto no `Graphics` do
`PrintDocument`. O driver de impressão recebe instruções vetoriais e otimiza
para a resolução nativa do dispositivo.

ESC/POS térmica é exceção: bobinas 203 dpi não fazem distinção entre texto
vetorial e raster, então rasterizar é mais simples e equivalente em qualidade.

## Pickando bandejas

```csharp
var caps = printer.GetCapabilities("HP LaserJet M404n");
foreach (var bin in caps.PaperSources)
{
    Console.WriteLine($"{bin.Id}: {bin.Name}");
}

await printer.PrintAsync(report, source, ctx, new PrintOptions
{
    PrinterName = "HP LaserJet M404n",
    PrinterBin = "Tray 2",
});
```

## Testando sem hardware

- **Windows**: imprima em "Microsoft Print to PDF" ou "Microsoft XPS Document
  Writer" — ambos são impressoras virtuais com nome estável.
- **ESC/POS**: use `StreamEscPosTransport` apontando para um arquivo `.bin` e
  inspecione com o emulador online [receipt-printer-emulator](https://receipt-printer-emulator.web.app/).
- **Android**: o emulador Android Studio inclui "Save as PDF" no diálogo do
  Print Framework.
