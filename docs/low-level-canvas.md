# Canvas low-level (desenho direto)

O OmniReport oferece **três níveis de autoria**, do mais alto ao mais baixo:

1. **Designer visual** (`<ReportDesigner />`) — arrasta-e-solta, salva `.repx`.
2. **Code-first fluente** (`ReportBuilder.Create(...)`) — bandas e elementos imutáveis em C#.
3. **Canvas low-level** (`IRenderingContext`) — você fala **direto** com a superfície de
   desenho, posicionando cada primitivo em milímetros. Sem `ReportDefinition`, sem bandas,
   sem paginador.

Este guia cobre o nível 3 — a "saída de emergência" que dá controle pixel-perfect quando o
modelo bandado não encaixa.

## Quando usar

- **Artefatos sob medida** que não são "relatórios bandados": certificados, crachás,
  etiquetas, rótulos de envio, diplomas, fichas.
- **Layouts computados proceduralmente**: calendários, mapas de assentos, plantas, agendas —
  onde a posição de cada elemento é calculada, e bandas dariam mais trabalho do que ajudam.
- **Embutir renderização** como primitivo dentro de outro renderer (ex.: um serviço de
  etiquetas que emite a arte direto na superfície).

Para relatórios orientados a dados (listas, agrupamentos, totais), continue no code-first ou
no designer — eles existem exatamente para isso.

## O contrato

Tudo passa por três interfaces pequenas em `Reporting.Rendering`:

```csharp
public interface IRenderingContext : IDisposable
{
    void BeginPage(PageSetup pageSetup);
    void EndPage();

    void DrawText(string text, Rectangle bounds, TextStyle style);
    void DrawLine(Point from, Point to, PenStyle pen);
    void DrawRectangle(Rectangle bounds, PenStyle? pen, BrushStyle? fill);
    void DrawEllipse(Rectangle bounds, PenStyle? pen, BrushStyle? fill);
    void DrawImage(ReadOnlySpan<byte> imageData, Rectangle bounds);
    void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill);

    Size MeasureText(string text, TextStyle style, Unit? maxWidth = null);
}

public interface ITextMeasurer            // medir sem possuir um contexto inteiro
{
    Size Measure(string text, TextStyle style, Unit? maxWidth = null);
}

public interface IPathBuilder             // composição de paths vetoriais
{
    IPathBuilder MoveTo(Point point);
    IPathBuilder LineTo(Point point);
    IPathBuilder QuadraticTo(Point control, Point end);
    IPathBuilder CubicTo(Point c1, Point c2, Point end);
    IPathBuilder Arc(Rectangle bounds, double startAngleDegrees, double sweepDegrees);
    IPathBuilder Close();
}
```

| Primitivo | Assinatura | Notas |
|---|---|---|
| Texto | `DrawText(text, bounds, style)` | alinhamento H/V e word-wrap vêm do `TextStyle` |
| Linha | `DrawLine(from, to, pen)` | |
| Retângulo | `DrawRectangle(bounds, pen?, fill?)` | passe `pen: null` p/ só preencher, `fill: null` p/ só contornar |
| Elipse | `DrawEllipse(bounds, pen?, fill?)` | |
| Imagem | `DrawImage(bytes, bounds)` | PNG/JPEG em bytes |
| Path | `DrawPath(build, pen?, fill?)` | `build` recebe um `IPathBuilder`; paths podem ser contornados **e** preenchidos |
| Medição | `MeasureText(text, style)` | devolve `Size` (largura/altura em `Unit`) — use p/ centralizar/alinhar à direita |

Coordenadas são sempre em **`Unit`** (milímetros via `Unit.FromMm`, pontos via
`Unit.FromPoint`). O `PageSetup` é a **única** coisa que o motor "sabe" — todo o resto são
primitivos posicionados por você.

## Exemplo mínimo

Um crachá de uma página, desenhado direto no canvas e salvo como PNG:

```csharp
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Styling;

// SkiaRenderingContext é IRenderingContext E ITextMeasurer ao mesmo tempo.
using var ctx = new SkiaRenderingContext();

ctx.BeginPage(PageSetup.A4Portrait);            // 210 × 297 mm

// 1) Faixa de cabeçalho (retângulo laranja preenchido, sem contorno)
ctx.DrawRectangle(
    new Rectangle(Unit.FromMm(20), Unit.FromMm(20), Unit.FromMm(170), Unit.FromMm(16)),
    pen: null,
    fill: new BrushStyle(Color.FromRgb(0xC2, 0x41, 0x0C)));

// 2) Título centralizado — meça o texto para centralizar com precisão
var titleStyle = new TextStyle(
    new Font("Arial", 20, FontStyle.Bold), Color.White, HorizontalAlignment.Center);
ctx.DrawText("CRACHÁ DE ACESSO",
    new Rectangle(Unit.FromMm(20), Unit.FromMm(24), Unit.FromMm(170), Unit.FromMm(10)),
    titleStyle);

// 3) Linha divisória
ctx.DrawLine(
    new Point(Unit.FromMm(20), Unit.FromMm(45)),
    new Point(Unit.FromMm(190), Unit.FromMm(45)),
    new PenStyle(Color.Gray, Unit.FromPoint(0.5)));

// 4) Um losango decorativo via path (contornado + preenchido)
ctx.DrawPath(b => b
        .MoveTo(new Point(Unit.FromMm(105), Unit.FromMm(55)))
        .LineTo(new Point(Unit.FromMm(110), Unit.FromMm(60)))
        .LineTo(new Point(Unit.FromMm(105), Unit.FromMm(65)))
        .LineTo(new Point(Unit.FromMm(100), Unit.FromMm(60)))
        .Close(),
    new PenStyle(Color.Black, Unit.FromPoint(0.7)),
    new BrushStyle(Color.FromRgb(0x96, 0x6C, 0x29)));

ctx.EndPage();

File.WriteAllBytes("cracha.png", ctx.GetPagePng(0));   // 1 byte[] por página
```

## Escreva o desenho uma vez, troque o backend

O mesmo código de desenho roda contra **qualquer** `IRenderingContext`. Extraia para um
método `Render(IRenderingContext ctx, ITextMeasurer measurer)` e aponte para o backend que
quiser — PNG, PDF vetorial ou planilha — sem mudar uma linha do desenho:

```csharp
static void Render(IRenderingContext ctx, ITextMeasurer measurer) { /* draws above */ }

// a) PNG raster (1 byte[] por página)
using var raster = new SkiaRenderingContext();
Render(raster, raster);
File.WriteAllBytes("cracha.png", raster.GetPagePng(0));

// b) PDF vetorial — texto selecionável, formas continuam paths
using var pdfStream = File.Create("cracha.pdf");
using (var pdf = new SkiaPdfRenderingContext(pdfStream,
           new SkiaSharp.SKDocumentPdfMetadata { Title = "Crachá", Author = "OmniReport" }))
{
    Render(pdf, pdf);
}

// c) Excel — grava as chamadas de desenho e quantiza numa grade de células
var recorder = new Reporting.Layout.RecordingRenderingContext(raster);
Render(recorder, recorder);
var rendered = recorder.ToRenderedReport("cracha");
using var xlsx = File.Create("cracha.xlsx");
new Reporting.Output.Excel.ExcelExporter().Export(rendered, xlsx);   // Export grava num Stream
```

É a mesma flexibilidade que o pipeline bandado usa internamente: o paginador emite
primitivos, e cada renderer/exporter os consome. No nível low-level você simplesmente emite
os primitivos **você mesmo**.

## Fontes de dados low-level (sem o builder)

O nível baixo do **lado dos dados** é o contrato `IReportDataSource` — útil para alimentar um
dashboard próprio, despejar linhas num console ou qualquer consumidor que não seja um
relatório bandado. Todo provider (in-memory, `DataTable`, JSON, XML, REST, FileSystem, SQL)
expõe o **mesmo** streaming assíncrono, então o código consumidor é idêntico:

```csharp
IReportDataSource source = new JsonDataSource("Pedidos", new JsonDataSourceOptions
{
    FilePath = "pedidos.json",
    RootPath = "data.results",
});

await foreach (var record in source.ReadAsync(ct))
{
    // source.Schema.Fields já está populado após a primeira iteração
    var total = record["total"];
    // ... alimente seu consumidor (dashboard, fila, export custom)
}
```

Veja [`data-sources.md`](data-sources.md) para implementar um provider customizado.

## Samples

- [`Sample05_CanvasLowLevel`](../samples/Reporting.Samples.CodeFirst/Reports/Sample05_CanvasLowLevel.cs)
  — certificado + fatura desenhados direto no canvas, renderizados em PNG, PDF e XLSX a partir
  do **mesmo** `Render(ctx, measurer)`.
- [`Sample10_LowLevelProviders`](../samples/Reporting.Samples.CodeFirst/Reports/Sample10_LowLevelProviders.cs)
  — consome JSON/XML/FileSystem pelo contrato `IReportDataSource`, sem o builder.
