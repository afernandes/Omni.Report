# Guia do Desenvolvedor — OmniReport

Para **estender, integrar ou contribuir** com o OmniReport. Para usar a engine, veja o
[Guia do Usuário](user-guide.md).

## Sumário

1. [Princípios de arquitetura](#1-princípios-de-arquitetura)
2. [Mapa de projetos](#2-mapa-de-projetos)
3. [Fluxo render → export](#3-fluxo-render--export)
4. [Modelo imutável e serialização auto-wired](#4-modelo-imutável-e-serialização-auto-wired)
5. [Pontos de extensão](#5-pontos-de-extensão)
   - [Adicionar um `ReportElement`](#51-adicionar-um-reportelement)
   - [Fonte de dados custom](#52-fonte-de-dados-custom)
   - [Exportador custom](#53-exportador-custom)
   - [Driver de impressão custom](#54-driver-de-impressão-custom)
   - [Backend de renderização](#55-backend-de-renderização)
6. [Expressões e funções](#6-expressões-e-funções)
7. [Paginação por dentro](#7-paginação-por-dentro)
8. [Hospedagem / DI](#8-hospedagem--di)
9. [Build, testes e convenções](#9-build-testes-e-convenções)

---

## 1. Princípios de arquitetura

- **`Reporting.Core` é puro**: zero dependência de plataforma, GUI ou framework de UI.
- **Modelo imutável**: bandas e elementos são `sealed record`; `EquatableArray<T>`/`EquatableDictionary<K,V>`
  dão igualdade por valor. Unidades em `Unit` (mils); `Rectangle`/`Point`/`Color` são structs/records.
- **Paginação → primitivas planas**: a paginação produz uma lista de `LayoutPrimitive` em **coordenadas
  absolutas**. Qualquer renderer (Skia, GDI) ou exportador consome a **mesma** sequência → saída consistente.
- **Plug-and-play via DI**: renderers, exportadores, impressoras e fontes de dados são intercambiáveis.
- **UI como RCL**: Designer e Viewer são Razor Class Libraries — o mesmo binário roda em Blazor Server, Web App
  e MAUI Hybrid.

## 2. Mapa de projetos

`src/` (38 bibliotecas). Núcleo sem dependências de plataforma:

| Projeto | Responsabilidade |
|---|---|
| `Reporting.Core` | Modelo de domínio: bandas, elementos, `Style`, charts/KPIs, geometria, paleta de cores |
| `Reporting.Expressions` | Avaliador (NCalc), agregados, `Lookup`, posicionais, contexto de expressão |
| `Reporting.Expressions.Roslyn` | **Opt-in** — compila `CodeElement` C#/VB (RCE por design; desabilitado por padrão) |
| `Reporting.Data` / `Reporting.DataSources` | `IReportDataSource`, datasets, campos, relações, parâmetros |
| `Reporting.Layout` | **Paginador** de duas passadas; `BandRenderer`, `TablixRenderer`, `ChartRenderer`, `KpiRenderer`, `MapRenderer`; `LayoutPrimitive` |
| `Reporting.Rendering` | Abstrações: `IRenderingContext`, `IReportExporter`, `IPathBuilder` |
| `Reporting.Rendering.Skia` / `.Gdi` | Backends (SkiaSharp cross-plat; GDI Windows) |
| `Reporting.CodeFirst` | API fluente `ReportBuilder`/`BandContent`/`TablixBuilder`/`ChartBuilder` |
| `Reporting.Serialization` | `.repx` (XML) + `.repjson` (JSON) writers/readers; **`RdlImporter`** |
| `Reporting.Output.{Pdf,Excel,Svg,Html,Csv,Json,Markdown,Docx,Image}` | Exportadores |
| `Reporting.Printing.{WindowsSpooler,EscPos,Android}` | Drivers de impressão |
| `Reporting.Barcode` / `Reporting.Maps` | Encoders 1D/QR; shapes GeoJSON embutidos |
| `Reporting.Designer.Blazor` / `Reporting.Viewer.Blazor` | UI (RCL) |
| `Reporting.Hosting.AspNetCore` | `AddReporting` fluent DI |

## 3. Fluxo render → export

```
ReportDefinition
   │  ReportPaginator.PaginateAsync(request)
   ▼
RenderedReport  (páginas × List<LayoutPrimitive> absolutas)
   │
   ├── IReportExporter.Export(rendered, stream)   → PDF/XLSX/DOCX/CSV/…
   └── RenderedReportPlayer.Dispatch(primitive)   → IRenderingContext (Skia/GDI) → PNG/SVG/tela/impressora
```

`LayoutPrimitive` base carrega `Bounds`, `SourceElementId`, `LinkTarget`, `BookmarkId`, `DocMapLabel`,
`ClipBounds`, `ClipCornerRadius`, `IsVisual`. Subtipos: `DrawTextPrimitive`, `DrawLinePrimitive`,
`DrawRectanglePrimitive`, `DrawEllipsePrimitive`, `DrawImagePrimitive`, `DrawPolygonPrimitive`.

> ⚠️ **Ao adicionar um campo a `LayoutPrimitive`**, varra todos os sítios de despacho: o `RenderedReportPlayer`,
> os exportadores Skia próprios (PDF/PNG/SVG têm laços de replay), `RecordingRenderingContext`, e
> `ReportPaginator.Subreport.TranslatePrimitive` (translada primitivas de subreports). Campo escalar viaja de
> graça no `with`; campos posicionais precisam de tratamento explícito.

## 4. Modelo imutável e serialização auto-wired

`.repx`/`.repjson` são ~98% lossless. A serialização de elementos é **auto-wired por convenção** pelo
`ElementSerializationRegistry`: um `ReportElement` novo — `sealed record`, ctor sem parâmetros, membros
escalares/array/record — faz **round-trip sem editar manualmente os writers/readers**.

Restrições do auto-wiring: **não** suporta `EquatableDictionary` nem `ReportElement` aninhado por convenção —
para esses, use `EquatableArray<RecordKV>` ou registre o elemento como hand-wired. Bandas
(`ReportBand`/`DetailBand`/`GroupBand`) e `TablixElement`/`ReportDefinition` **são hand-wired** (posicionais):
um campo novo neles exige os 4 switches manuais (RepxWriter/Reader + RepJsonWriter/Reader).

## 5. Pontos de extensão

### 5.1 Adicionar um `ReportElement`

Estado reduzido (graças ao auto-wiring + PropertyGrid por metadados):

1. **Modelo** (`Reporting.Core`): `public sealed record FooElement : ReportElement { [PropertyGrid(...)] public string Bar { get; init; } = ""; }` — serialização repx/repjson **automática**.
2. **Render** (`Reporting.Layout`): adicione um braço no `BandRenderer.RenderElement` que emite `LayoutPrimitive`s (use um `FooRenderer` se for complexo). **Este é o único passo manual obrigatório.**
3. **Designer**: anote o tipo com `[ToolboxElement]` → entra no toolbox + outline + PropertyGrid automaticamente; adicione um braço no `ElementViewModel` (mapeamento kind↔record).
4. **Code-first** (opcional): método fluente em `BandContent`.

Veja `serialization-auto-wiring-design.md` e `property-grid-metadata-design.md`.

### 5.2 Fonte de dados custom

Implemente `IReportDataSource` (acesso assíncrono a linhas) e registre:

```csharp
public sealed class MyDataSource : IReportDataSource { /* MaterializeAsync → linhas */ }
builder.Services.AddReporting(o => o.AddDataSource("Vendas", new MyDataSource()));
```

### 5.3 Exportador custom

Implemente `IReportExporter` (`Format`/`FileExtension`/`ContentType` + `Export(RenderedReport, Stream)`).
Exportadores tabulares reusam `LayoutPrimitiveGrid.Build(report)` (quantiza primitivas de texto em linhas/colunas
com `RowKind`). Exportadores que precisam rasterizar uma região (ex.: chart no Word) usam
`RegionRasterizer.RenderRegionPng(prims, region, dpi)` de `Reporting.Output.Image`.

> Exportadores OOXML (XLSX/DOCX) **devem** incluir um teste `OpenXmlValidator` — a ordem dos filhos é fixa no
> schema e o SDK não reordena.

Registre no `ExporterRegistry` via `AddReporting(o => o.UseXxxOutput())`.

### 5.4 Driver de impressão custom

Implemente `IReportPrinter`. O ESC/POS rasteriza páginas a 203 dpi e envia comandos; o Windows-spooler desenha
via GDI. Veja `Reporting.Printing.*`.

### 5.5 Backend de renderização

Implemente `IRenderingContext` (DrawText/Line/Rectangle/Ellipse/Image/Path + `PushClip`/`PopClip` +
`BeginPage`/`EndPage`). Skia é a referência cross-plat; GDI cobre Windows. O `RenderedReportPlayer` dirige
qualquer contexto.

## 6. Expressões e funções

`ExpressionEvaluator` (NCalc) dispara `EvaluateFunction` para qualquer nome; o dispatch tenta, em ordem:
**agregado → lookup → posicional → escalar (VB/SSRS) → NCalc nativo**. Para adicionar uma função, ligue-a no
ponto certo dessa cadeia (ex.: agregados em `AggregateCalculator` + `AggregateNames`; escalares em
`TryEvaluateScalarFunction`). `RdlExpression.Convert` traduz a sintaxe VB-SSRS na importação.

## 7. Paginação por dentro

`ReportPaginator.ExecutePass` percorre linhas, abre/fecha grupos, emite bandas via `BandRenderer`. `BandRenderer`
mede (`Measure`) e renderiza (`Render`) com a **mesma** lógica de altura efetiva (`EffectiveElementBottom`/
`EffectiveBandHeight`) — invariante crítica `Measure≡Render`. `PageAccumulator` gerencia colunas (snake),
`ContentBottom`, `Origin` e `Flush`. `EmitBandSplit` fatia uma banda maior que a coluna por elemento (com guard
de terminação). Detalhes em [`pagination`](rdl-spec-compliance.md) e nos testes
`tests/Reporting.Layout.Tests/Pagination*`.

## 8. Hospedagem / DI

```csharp
builder.Services.AddReporting(o => o
    .UseSkiaRendering()            // ou .UseGdi() em Windows
    .UsePdfOutput().UseExcelOutput().UseDocxOutput()
    .UsePrinter<WindowsSpoolerPrinter>()
    .AddDataSource("Vendas", data));
```

Resolva por DI: `IReportPaginator`, `SkiaPdfExporter`, `ExcelExporter`, `DocxExporter`, `IReportPrinter`,
`DataSourceRegistry`, `RepxSerializer`. Os componentes `<ReportViewer />`/`<ReportDesigner />` consomem o mesmo
container.

## 9. Build, testes e convenções

```powershell
dotnet build OmniReport.slnx -c Release      # projetos de produção: TreatWarningsAsErrors (sem warnings!)
dotnet test  OmniReport.slnx                 # 23 projetos de teste, 1000+ casos
```

- **Conventional Commits**; `main` + feature branches (veja `CONTRIBUTING.md`).
- A auditoria trata advisories (ex.: `CS8019` using não usado) como **erro de build**.
- Cada exportador OOXML precisa de teste `OpenXmlValidator`; cada `LayoutPrimitive` novo, varredura dos
  despachos; cada feature, paridade nos 3 modos quando há campo de modelo.
- Testes de formatação **culture-específica** passam no Windows (ICU) e podem falhar no Linux/CI (NLS) —
  compare com o render da própria cultura, não com literais.
