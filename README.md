# OmniReport

[![CI](https://img.shields.io/badge/CI-passing-brightgreen)](.github/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/AndersonN.Omni.Report.Core.svg)](https://www.nuget.org/packages/AndersonN.Omni.Report.Core)
[![Tests](https://img.shields.io/badge/tests-1000%2B%20passing-success)](#)
[![Coverage](https://img.shields.io/badge/core%20coverage-≥80%25-success)](#)
[![RDL](https://img.shields.io/badge/RDL%20compat-~85%25-success)](docs/rdl-spec-compliance.md)

**Motor profissional de relatórios bandados para .NET 10**, com **três modalidades de autoria**
(code-first fluent, canvas low-level e designer visual Blazor — paridade total entre elas) e
pipeline de renderização pluggable (SkiaSharp, GDI/Windows, PDF vetorial, XLSX, **Word/.docx**,
HTML/SVG/CSV/JSON/Markdown, ESC/POS térmico, Android Print Framework). Gráficos nativos
(barras/linhas/pizza/área/dispersão/bolha/stock/radar), medidores KPI
(gauge/data-bar/sparkline/indicator), Tablix (tabela **+ matrix/pivô**), Rectangle como container,
mapas vetoriais e códigos de barras/QR; conectores de dados para SQL
(SQLite/PostgreSQL/SQL Server/MySQL), JSON, XML, REST e sistema de arquivos. Importa **.rdl do
SSRS** (~85% de conformidade) e tem round-trip próprio lossless (`.repx`/`.repjson`).

Equivalente em capacidade a Crystal Reports / SSRS / FastReport, original, MIT, com foco
em cenários brasileiros (PDV, NFC-e, DANFE, ABNT NBR 5891). Veja a
[comparação detalhada](docs/comparison.md) com o RDL oficial e outras engines.

## Galeria

### Designer visual (Blazor)

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/designer-dark.png">
  <source media="(prefers-color-scheme: light)" srcset="assets/designer-light.png">
  <img alt="Designer visual do OmniReport" src="assets/designer-light.png">
</picture>

### Saída do engine (renderização real dos samples)

| | |
|:--:|:--:|
| <img src="assets/sample-dashboard.png" width="360" alt="Dashboard"/><br/>**Dashboard** — gráficos + Gauge + Sparkline + Indicator + DataBar | <img src="assets/sample-map.png" width="360" alt="Mapa vetorial"/><br/>**Mapa vetorial** — Web Mercator + graticule + shapes |
| <img src="assets/sample-tablix.png" width="360" alt="Tablix"/><br/>**Tablix** — tabela bandada | <img src="assets/sample-nfce.png" width="220" alt="Cupom NFC-e"/><br/>**Cupom NFC-e** — térmico 80 mm (DANFE) |

> Imagens geradas por `dotnet run --project samples/Reporting.Samples.CodeFirst -- ./out` (PNG vetorial→raster via SkiaSharp).

## Sumário

- [Galeria](#galeria)
- [Arquitetura](#arquitetura)
- [Instalação](#instalação)
- [Quickstarts](#quickstarts)
  - [Primeiro relatório code-first em 5 minutos](#primeiro-relatório-code-first-em-5-minutos)
  - [Primeiro relatório no designer em 5 minutos](#primeiro-relatório-no-designer-em-5-minutos)
  - [Canvas low-level (sem bandas)](#canvas-low-level-sem-bandas)
  - [Hospedando em ASP.NET Core / Blazor / MAUI](#hospedando-em-aspnet-core--blazor--maui)
- [Samples](#samples)
- [Status](#status)
- [Documentação](#documentação) — [guia do usuário](docs/user-guide.md) · [guia do dev](docs/developer-guide.md) · [comparação](docs/comparison.md) · [apresentação HTML](docs/presentation.html)
- [Contribuindo](#contribuindo)

## Arquitetura

```mermaid
graph TB
    subgraph CoreLibs["Core libraries (TFM net10.0, sem deps de plataforma)"]
        Core[Reporting.Core<br/>domain model · charts · KPIs · RDL elements]
        Expr[Reporting.Expressions<br/>NCalc + agregados + templates]
        Data[Reporting.DataSources<br/>IEnumerable · DataTable · master-detail]
        Render[Reporting.Rendering<br/>IRenderingContext · IPathBuilder]
        Layout[Reporting.Layout<br/>two-pass paginator · Chart/KPI/Barcode]
        Codef[Reporting.CodeFirst<br/>fluent ReportBuilder]
        Serial[Reporting.Serialization<br/>.repx XML + .repjson JSON · round-trip]
        Bar[Reporting.Barcode<br/>1D + QR encoders]
        Print[Reporting.Printing<br/>IReportPrinter abstrações]
    end

    subgraph Optin["Add-ons opt-in (carregados só quando o host habilita)"]
        Roslyn[Reporting.Expressions.Roslyn<br/>compila bloco Code C# · só fontes confiáveis]
        Maps[Reporting.Maps<br/>shapes GeoJSON embutidos p/ basemap]
    end

    subgraph Providers["Data providers (pluggable)"]
        Ado[AdoNet → SQLite · PostgreSQL · SQL Server · MySQL]
        Semi[JSON · XML · WebService/REST · FileSystem]
    end

    subgraph Renderers["Renderers (pluggable)"]
        Skia[Reporting.Rendering.Skia<br/>SkiaPrimitiveRenderer · cross-plat]
        Gdi[Reporting.Rendering.Gdi<br/>net10.0-windows · System.Drawing.Graphics]
    end

    subgraph Outputs["Output exporters"]
        Pdf[Reporting.Output.Pdf<br/>SkiaPdfExporter · vetorial nativo]
        Xlsx[Reporting.Output.Excel<br/>ClosedXML · fórmulas =SUM]
        Texto[Output.Svg · Html · Csv · Json · Markdown]
    end

    subgraph Printers["Printer drivers"]
        WinSp[WindowsSpooler<br/>PrintDocument → GDI]
        EscPos[EscPos<br/>raster 203dpi → TCP/serial]
        Andr[Android<br/>PrintManager · stub condicional]
    end

    subgraph UI["UI components (Razor Class Libraries)"]
        Viewer[Reporting.Viewer.Blazor<br/>page navigator · zoom · export]
        Designer[Reporting.Designer.Blazor<br/>shell · canvas · property grid]
    end

    Host[Reporting.Hosting.AspNetCore<br/>AddReporting fluent DI]

    Core --> Expr & Data & Render & Layout & Codef & Serial & Print
    Bar --> Layout
    Expr --> Roslyn
    Core --> Maps
    Roslyn -.->|Code resolver| Layout
    Maps -.->|shapes GeoJSON| Layout
    Data --> Ado & Semi
    Render --> Skia & Gdi
    Layout --> Skia & Gdi
    Skia --> Pdf & Texto
    Layout --> Xlsx & Texto
    Render --> WinSp & EscPos
    Print --> WinSp & EscPos & Andr
    Layout --> Viewer & Designer
    Skia --> Viewer & Designer
    Ado & Semi --> Designer
    Pdf & Xlsx & Texto --> Viewer & Designer
    Core & Render & Pdf & Xlsx & Print & Serial --> Host
```

**Princípios**:

- `Reporting.Core` é puro: zero dependência de plataforma, GUI ou framework de UI.
- Bandas + elementos imutáveis (`sealed record`) compõem o `ReportDefinition`.
- A paginação produz `LayoutPrimitive`s posicionados em coordenadas absolutas — qualquer
  renderer (Skia, GDI) consome a mesma sequência.
- Renderers/exporters/printers são plug-and-play via DI.
- Designer e Viewer são Razor Class Libraries: o mesmo binário roda em Blazor Server,
  Blazor Web App e MAUI Blazor Hybrid (Windows + Android).

## Instalação

Pacotes publicados no **NuGet.org** sob o prefixo **`AndersonN.Omni.Report.*`** (e espelhados no
**GitHub Packages**):

```bash
# Conjunto mínimo: autoria code-first + paginação + render PDF (Skia)
dotnet add package AndersonN.Omni.Report.CodeFirst
dotnet add package AndersonN.Omni.Report.Layout
dotnet add package AndersonN.Omni.Report.Rendering.Skia
dotnet add package AndersonN.Omni.Report.Output.Pdf

# Designer e Viewer visuais (Blazor)
dotnet add package AndersonN.Omni.Report.Designer.Blazor
dotnet add package AndersonN.Omni.Report.Viewer.Blazor

# Injeção de dependência para ASP.NET Core / Blazor / MAUI
dotnet add package AndersonN.Omni.Report.Hosting.AspNetCore
```

> O **ID** do pacote leva o prefixo `AndersonN.Omni.Report.`, mas o **namespace** continua
> `Reporting.*` (ex.: `using Reporting.CodeFirst;`) e os assets das RCLs seguem em
> `_content/Reporting.Designer.Blazor` / `_content/Reporting.Viewer.Blazor`.

**Pacotes por necessidade** — todos com o prefixo `AndersonN.Omni.Report.` (só o sufixo varia):

| Categoria | Sufixos (após `AndersonN.Omni.Report.`) |
|---|---|
| Exporters | `Output.{Excel,Html,Svg,Csv,Json,Markdown}` |
| Conectores | `DataSources.{Sqlite,PostgreSql,SqlServer,MySql,AdoNet,Json,Xml,WebService,FileSystem}` |
| Impressão | `Printing.{WindowsSpooler,EscPos,Android}` |
| Extras | `Barcode` · `Maps` · `Designer.Blazor.DataConnect` |
| Opt-in (executa C#) | `Expressions.Roslyn` — ⚠ use apenas com fontes de relatório confiáveis |

> **GitHub Packages:** para instalar pelo feed do GitHub em vez do NuGet.org, adicione a fonte
> `https://nuget.pkg.github.com/afernandes/index.json` (requer autenticação com um token do GitHub).

## Quickstarts

> **Requisitos:** SDK do .NET 10. O núcleo, o Skia, os exporters e o ESC/POS são
> cross-platform; o renderer GDI e o `WindowsSpoolerPrinter` exigem `net10.0-windows`
> (Windows) e o `AndroidPrintFrameworkPrinter` exige `net10.0-android`.

### Primeiro relatório code-first em 5 minutos

```csharp
using Reporting.CodeFirst;
using Reporting.Output.Pdf;
using Reporting.Layout;
using Reporting.Rendering.Skia;

var vendas = new[]
{
    new { Cliente = "Ana",  Produto = "Caneta",  Total = 25.00m },
    new { Cliente = "Ana",  Produto = "Caderno", Total = 27.40m },
    new { Cliente = "Beto", Produto = "Lápis",   Total = 10.80m },
};

var report = ReportBuilder.Create("Vendas")
    .Page(p => p.A4().Portrait().Margins(20))
    .DataSource("Vendas", vendas)
    .ReportHeader(h => h.Height(15)
        .Text("Relatório de Vendas").At(0, 0).Size(170, 12).Center().Bold().Font("Arial", 16))
    .Group("PorCliente", "Fields.Cliente", g => g
        .Header(h => h.Height(8)
            .Text("Cliente: {Fields.Cliente}").At(0, 1).Size(170, 6).Bold())
        .Detail(d => d.Height(6)
            .Text("{Fields.Produto}").At(0, 0).Size(100, 6)
            .Text("{Fields.Total:C}").At(140, 0).Size(30, 6).AlignRight()))
    .ReportFooter(f => f.Height(10)
        .Text("Total: {Sum(Fields.Total):C}").At(0, 2).Size(170, 6).AlignRight().Bold())
    .Build();

var rendered = await report.PaginateAsync();
new SkiaPdfExporter().ExportToFile(rendered, "vendas.pdf");
```

Resultado: PDF vetorial com texto selecionável, fórmulas pt-BR (R$ 53,40), agrupado por cliente.

### Primeiro relatório no designer em 5 minutos

1. Adicione ao seu app Blazor o pacote `AndersonN.Omni.Report.Designer.Blazor`:
   ```xml
   <PackageReference Include="AndersonN.Omni.Report.Designer.Blazor" />
   ```
2. No `App.razor` (ou `_Host.cshtml`) carregue as folhas de estilo do designer no `<head>`
   — as cinco são necessárias, na ordem abaixo (tokens → base → layout → componentes → overlays):
   ```html
   <link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/tokens.css" />
   <link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/base.css" />
   <link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/layout.css" />
   <link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/components.css" />
   <link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/overlays.css" />
   ```
   E, antes de `</body>`, o módulo JS (drag/resize, marquee, smart-guides, réguas e zoom):
   ```html
   <script src="_content/Reporting.Designer.Blazor/js/designer.js"></script>
   ```
3. Em uma página:
   ```razor
   @page "/designer"
   @rendermode InteractiveServer
   @using Reporting.Designer.Blazor

   <ReportDesigner OnSaved="@HandleSaved" />

   @code {
       private async Task HandleSaved(byte[] repxBytes)
       {
           // Persist em DB, S3, etc.
           await File.WriteAllBytesAsync("user-report.repx", repxBytes);
       }
   }
   ```
4. Navegue, arraste do toolbox, edite no property grid, salve. O `.repx` é compatível com
   `RepxSerializer.Load` e roda igual no pipeline de paginação code-first.

### Canvas low-level (sem bandas)

Quando o modelo bandado não encaixa (certificados, crachás, etiquetas, layouts computados),
fale **direto** com `IRenderingContext` — sem `ReportDefinition`, sem paginador. Você posiciona
cada primitivo em milímetros:

```csharp
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Styling;

using var ctx = new SkiaRenderingContext();      // é IRenderingContext E ITextMeasurer
ctx.BeginPage(PageSetup.A4Portrait);

ctx.DrawRectangle(                                // faixa laranja preenchida
    new Rectangle(Unit.FromMm(20), Unit.FromMm(20), Unit.FromMm(170), Unit.FromMm(16)),
    pen: null, fill: new BrushStyle(Color.FromRgb(0xC2, 0x41, 0x0C)));

ctx.DrawText("CRACHÁ DE ACESSO",                  // título centralizado
    new Rectangle(Unit.FromMm(20), Unit.FromMm(24), Unit.FromMm(170), Unit.FromMm(10)),
    new TextStyle(new Font("Arial", 20, FontStyle.Bold), Color.White, HorizontalAlignment.Center));

ctx.DrawPath(b => b                               // losango: contorno + preenchimento
        .MoveTo(new Point(Unit.FromMm(105), Unit.FromMm(42)))
        .LineTo(new Point(Unit.FromMm(110), Unit.FromMm(47)))
        .LineTo(new Point(Unit.FromMm(105), Unit.FromMm(52)))
        .LineTo(new Point(Unit.FromMm(100), Unit.FromMm(47)))
        .Close(),
    new PenStyle(Color.Black, Unit.FromPoint(0.7)),
    new BrushStyle(Color.FromRgb(0x96, 0x6C, 0x29)));

ctx.EndPage();
File.WriteAllBytes("cracha.png", ctx.GetPagePng(0));
```

O **mesmo** código de desenho roda contra qualquer backend: troque `SkiaRenderingContext` por
`SkiaPdfRenderingContext` (PDF vetorial) ou `RecordingRenderingContext` (→ XLSX) sem mudar uma
linha. Guia completo em [`docs/low-level-canvas.md`](docs/low-level-canvas.md).

### Hospedando em ASP.NET Core / Blazor / MAUI

```csharp
using Reporting.Hosting;

builder.Services.AddReporting(opts => opts
    .UseSkiaRendering()                                        // padrão; pode chamar UseGdi() em Windows
    .UsePdfOutput(new PdfExportOptions { Author = "Acme" })
    .UseExcelOutput()
    .UsePrinter<WindowsSpoolerPrinter>()                       // ou EscPosPrinter, AndroidPrintFrameworkPrinter
    .AddDataSource("Vendas", await LoadVendasAsync()));
```

A partir daí, qualquer componente Razor (`<ReportViewer />`, `<ReportDesigner />`) ou serviço
custom recebe via DI: `IRenderingContext`, `IReportPaginator`, `SkiaPdfExporter`,
`ExcelExporter`, `IReportPrinter`, `DataSourceRegistry`, `RepxSerializer`.

## Samples

| Sample | Plataforma | O que demonstra |
|---|---|---|
| `samples/Reporting.Samples.CodeFirst` | Console | 15 relatórios reais — Vendas, Produtos, Caixa, Cupom NFC-e, conectores (JSON/XML/REST/FileSystem/SQLite) e componentes visuais (Dashboard, Tablix, Mapa, bloco Code) → PDF/PNG/XLSX/HTML/SVG/CSV/JSON/Markdown/.repx/.repjson/.escpos.bin |
| `samples/Reporting.Samples.DatabaseReport` | Console | Relatório a partir de banco SQLite via AdoNet/SqliteDataSource |
| `samples/Reporting.Samples.RdlImport` | Console | **Lê um `.rdl` do SSRS** com `RdlImporter`, mostra a estrutura importada + avisos, pagina com dados in-memory e exporta PDF/HTML |
| `samples/Reporting.Samples.WindowsPrinting` | Windows console | Lista impressoras + imprime sample 1 em "Microsoft Print to PDF" via WindowsSpoolerPrinter |
| `samples/Reporting.Samples.BlazorServer` | Blazor Web App | Galeria com `<ReportViewer />` + rota `/designer` com `<ReportDesigner />` e faixa **Sandbox** que carrega os samples (Dashboard/Tablix/Mapa renderizam **com dados** no preview) |
| `samples/Reporting.Samples.MauiHybrid` | MAUI (Windows + Android cond.) | Mesmos componentes Razor em desktop nativo e Android nativo |

```powershell
# rodar os samples principais (gera PDFs, XLSX, .repx, .repjson):
dotnet run --project samples/Reporting.Samples.CodeFirst -c Release -- ./out
# ler/importar um .rdl do SSRS e exportar PDF/HTML:
dotnet run --project samples/Reporting.Samples.RdlImport -c Release
# iniciar Blazor Server (designer + viewer):
dotnet run --project samples/Reporting.Samples.BlazorServer
# Windows + designer/viewer in-process via MAUI WebView:
dotnet run --project samples/Reporting.Samples.MauiHybrid -f net10.0-windows10.0.19041.0
```

## Status

A v0.1.0 entregou as 11 etapas do roteiro original (17 bibliotecas, 375 testes). Desde então o
projeto **mais que dobrou**: hoje são **38 bibliotecas** em `src/`, **1000+ testes** verdes (23
projetos de teste) e cobertura ≥ 80% no núcleo. Conformidade com a especificação RDL da Microsoft
em **~85%** (importador `.rdl` em ~80%) — veja [`docs/rdl-spec-compliance.md`](docs/rdl-spec-compliance.md).

**Adicionado após a v0.1.0:**

| Área | Módulos / recursos | Status |
|---|---|---|
| Conectores de dados | AdoNet · SQLite · PostgreSQL · SQL Server · MySQL · JSON · XML · WebService/REST · FileSystem | ✅ |
| Exporters | SVG · HTML · CSV · JSON · Markdown · **Word/.docx** (tabela + imagens + charts rasterizados) · PNG/Image | ✅ |
| Código de barras | `Reporting.Barcode` — 1D (Code128/39/Codabar/ITF/EAN/UPC/ISBN/ISSN) + QR Code 2D | ✅ |
| Master-detail | sub-bandas + relações pai→filho (paginador e designer) | ✅ |
| Designer | DataConnect (conexão/schema/query/preview/relações), impressão (browser + nativo), formatação condicional, **import `.rdl`**, editores visuais dos elementos avançados, **edição aninhada de Rectangle**, réguas dual-axis com guias arrastáveis/snap | ✅ |
| Gráficos | `ChartElement` barras/linhas/pizza **+ área/dispersão/bolha/stock/radar** renderizando + fluente `.Chart()` | ✅ |
| KPIs | Gauge · DataBar · Sparkline · Indicator **renderizando** + API fluente | ✅ |
| Tablix | tabela bandada **+ matrix/pivô** (grupos de linha/coluna aninhados, subtotais, ColSpan) renderizando + fluente `.Tablix()` | ✅ |
| Rectangle container | hierarquia de filhos + clip + cantos arredondados, nos 3 modos (designer com edição aninhada) | ✅ |
| Paginação | duas passadas, multi-coluna (snake), `PrintOnFirstPage`/`PrintOnLastPage`, `CanGrow`/`CanShrink` (encolhe a banda), **split de banda por elemento** | ✅ |
| Map | **mapa vetorial**: Web Mercator + graticule + shapes GeoJSON (offline) + **basemap por tiles** (OpenStreetMap) + marcadores · `Reporting.Maps` · fluente `.Map()` | ✅ |
| Import RDL | DataSets, Tablix (flat→bandas + matrix), Chart/Gauge/Subreport, CustomReportItem→DataBar/Sparkline/Indicator, estilo, cores nomeadas, cultura, formatação condicional | ✅ ~80% |
| Code (C#/Roslyn) | avaliação `Code.X(...)` via pacote **opt-in** `Reporting.Expressions.Roslyn` (executa C# — só fontes confiáveis) | ✅ |

Veja [CHANGELOG.md](CHANGELOG.md) para o histórico, [docs/](docs/) para guias por área e a
matriz completa de conformidade RDL em [`docs/rdl-spec-compliance.md`](docs/rdl-spec-compliance.md).

### Roadmap (diferido — decisão de produto)

A saída é sempre um **relatório estático** (sem interatividade/drill-down em runtime). Itens grandes
registrados como follow-up, com a razão honesta de cada um, em
[`docs/rdl-spec-compliance.md`](docs/rdl-spec-compliance.md):

- **N-DetailBands / List** — múltiplas regiões de dados no nível raiz (toca o núcleo da paginação).
- **ReportSections** (RDL 2016) — múltiplas seções com `PageSetup` por seção.
- **`.rds`** — shared data sources / datasets externos.
- **Canvas WYSIWYG real no Designer** — render real (hoje placeholder) de Chart/Gauge/Tablix no canvas.
- **`Reporting.Maps`** — shapes embutidos cobrem um contorno simplificado do Brasil; conjuntos
  detalhados registráveis pelo host via `MapShapeRegistry.Register(nome, geoJson)`.

## Documentação

**Comece por aqui:**

- 📘 [`docs/user-guide.md`](docs/user-guide.md) — **guia do usuário**: todos os recursos e como criar relatórios nos 3 modos
- 🛠️ [`docs/developer-guide.md`](docs/developer-guide.md) — **guia do desenvolvedor**: arquitetura, pontos de extensão, como adicionar componentes
- 📊 [`docs/comparison.md`](docs/comparison.md) — **comparação** com o RDL oficial da Microsoft e outras engines (.NET e Java)
- 🎞️ [`docs/presentation.html`](docs/presentation.html) — apresentação visual (HTML/CSS/JS) — abra no navegador
- 📐 [`docs/rdl-spec-compliance.md`](docs/rdl-spec-compliance.md) — matriz de conformidade RDL por área × dimensão + follow-ups

**Guias por área:**

- [`docs/rdl-coverage.md`](docs/rdl-coverage.md) — matriz de cobertura: o que renderiza × o que só faz round-trip
- [`docs/expressions.md`](docs/expressions.md) — NCalc estendido, templates `{expr:fmt}`, agregados, scopes
- [`docs/data-sources.md`](docs/data-sources.md) — IEnumerable&lt;T&gt;, DataTable, scaffold para SQL/JSON
- [`docs/low-level-canvas.md`](docs/low-level-canvas.md) — desenho direto via `IRenderingContext` (sem bandas) e retarget de backend
- [`docs/designer.md`](docs/designer.md) — designer Blazor: shell, canvas, property grid, undo/redo
- [`docs/printing.md`](docs/printing.md) — Windows spooler, ESC/POS térmico, Android Print Framework
- [`docs/master-detail.md`](docs/master-detail.md) — relatórios master-detail e sub-bandas
- [`docs/printing-from-designer.md`](docs/printing-from-designer.md) — impressão no designer (browser + nativo)

## Contribuindo

Conventional Commits, branch model `main` + feature branches. Veja [CONTRIBUTING.md](CONTRIBUTING.md).

Build e testes localmente (a solução usa o formato `.slnx`):

```powershell
dotnet build OmniReport.slnx -c Release
dotnet test  OmniReport.slnx
```

Projetos de produção compilam com `TreatWarningsAsErrors` — mantenha o build sem warnings.

## License

MIT — see [LICENSE](LICENSE).
