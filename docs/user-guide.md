# Guia do Usuário — OmniReport

Guia completo de **todos os recursos** do OmniReport e como usá-los nos **três modos de autoria**. Para a
arquitetura interna e pontos de extensão, veja o [Guia do Desenvolvedor](developer-guide.md). Para a comparação
com RDL/concorrentes, veja [comparison.md](comparison.md).

## Sumário

1. [Conceitos](#1-conceitos)
2. [Início rápido](#2-início-rápido)
3. [Os três modos de autoria](#3-os-três-modos-de-autoria)
4. [Bandas](#4-bandas)
5. [Catálogo de elementos](#5-catálogo-de-elementos)
6. [Estilo, binding e formatação condicional](#6-estilo-binding-e-formatação-condicional)
7. [Dados: fontes, datasets, parâmetros e variáveis](#7-dados)
8. [Expressões](#8-expressões)
9. [Paginação](#9-paginação)
10. [Saída: exportadores e impressão](#10-saída)
11. [Visualizador (Blazor)](#11-visualizador-blazor)
12. [Importar do SSRS (.rdl)](#12-importar-do-ssrs-rdl)

---

## 1. Conceitos

Um relatório é um **`ReportDefinition`** imutável: `PageSetup` + **bandas** (header/detail/footer…) + **fontes
de dados** + **parâmetros**. A **paginação** (`PaginateAsync`) percorre os dados, posiciona cada elemento e
produz um `RenderedReport` (lista plana de `LayoutPrimitive` em coordenadas absolutas). Um **exportador**
(PDF, XLSX, DOCX…) ou um **renderer** (Skia/GDI) consome essa mesma sequência — o que garante saída idêntica
entre formatos.

```
ReportDefinition → PaginateAsync() → RenderedReport → Exporter/Renderer → PDF/PNG/XLSX/DOCX/…
```

## 2. Início rápido

```csharp
using Reporting.CodeFirst;
using Reporting.Output.Pdf;

var vendas = new[]
{
    new { Cliente = "Ana", Produto = "Caneta", Total = 25.00m },
    new { Cliente = "Ana", Produto = "Caderno", Total = 27.40m },
};

var report = ReportBuilder.Create("Vendas")
    .Page(p => p.A4().Portrait().Margins(20))
    .DataSource("Vendas", vendas)
    .ReportHeader(h => h.Height(15).Text("Relatório de Vendas").At(0, 0).Size(170, 12).Bold().Center())
    .Detail(d => d.Height(6)
        .Text("{Fields.Produto}").At(0, 0).Size(100, 6)
        .Text("{Fields.Total:C}").At(140, 0).Size(30, 6).AlignRight())
    .ReportFooter(f => f.Height(10).Text("Total: {Sum(Fields.Total):C}").At(0, 2).Size(170, 6).Bold().AlignRight())
    .Build();

var rendered = await report.PaginateAsync();
new SkiaPdfExporter().ExportToFile(rendered, "vendas.pdf");
```

## 3. Os três modos de autoria

Os três produzem o **mesmo** `ReportDefinition` e a **mesma** saída — escolha por conveniência, não por
limitação.

### 3.1 Code-first (fluente) — `Reporting.CodeFirst`

`ReportBuilder.Create(name)` abre a API fluente. Principais blocos:

| Método | Para quê |
|---|---|
| `.Page(p => p.A4()/.Letter()/.Custom(w,h).Portrait()/.Landscape().Margins(...).Columns(n))` | Papel, orientação, margens, multi-coluna |
| `.DataSource(name, IEnumerable)` / `.Parameter(...)` / `.Variable(...)` | Dados, parâmetros, variáveis |
| `.ReportHeader/.PageHeader/.Detail/.PageFooter/.ReportFooter(b => …)` | Bandas |
| `.Group(name, "Fields.X", g => g.Header(…).Detail(…).Footer(…))` | Agrupamento |
| dentro de uma banda: `.Text/.Label/.Line/.Rectangle/.Ellipse/.Image/.Chart/.Tablix/.Barcode/.Gauge/.DataBar/.Sparkline/.Indicator/.Map/.Subreport(...)` | Elementos |
| posicionamento: `.At(x,y).Size(w,h)` (mm), estilo: `.Font(...).Bold().Center().AlignRight().Fill(...).Format(...)` | Layout e estilo |

### 3.2 Low-level (records imutáveis) — `Reporting.Core`

Construa `ReportDefinition` e os elementos diretamente — ideal para geração programática ou layouts computados:

```csharp
var def = new ReportDefinition("Etiqueta", PageSetup.A4Portrait,
    detail: new DetailBand(Unit.FromMm(20), new EquatableArray<ReportElement>(
        new LabelElement { Text = "Produto", Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 6.Mm()) },
        new BarcodeElement { Symbology = BarcodeSymbology.QrCode, Expression = "Fields.Sku",
                             Bounds = new Rectangle(0.Mm(), 8.Mm(), 30.Mm(), 30.Mm()) })));
```

Para layouts **sem bandas** (certificados, crachás, etiquetas), fale direto com `IRenderingContext` — veja
[low-level-canvas.md](low-level-canvas.md).

### 3.3 Designer visual (Blazor) — `Reporting.Designer.Blazor`

Componente `<ReportDesigner />` WYSIWYG: toolbox arrastável, canvas com réguas/guias/zoom, **PropertyGrid
orientado a metadados** (gerado das anotações `[PropertyGrid]` do modelo — um elemento novo ganha editor
automaticamente), outline tree, browser de fontes de dados, undo/redo, edição **aninhada de Rectangle**, e
import/export `.repx`/`.repjson`. Veja [designer.md](designer.md).

> O `.repx`/`.repjson` salvo abre igual no pipeline code-first — os três modos compartilham o serializador.

## 4. Bandas

| Banda | Quando renderiza |
|---|---|
| `ReportHeader` | uma vez, no início |
| `PageHeader` | topo de cada página (`PrintOnFirstPage`/`PrintOnLastPage` controlam 1ª/última) |
| `GroupHeader` / `GroupFooter` | a cada grupo (`GroupBand` com `KeepTogether`, `RepeatHeaderOnNewPage`, `PageBreak`) |
| `Detail` | uma vez por linha de dados (`CanGrow`/`CanShrink`, `FilterExpression`, `SortExpressions`) |
| `SubDetail` | banda aninhada que itera uma relação master-detail por linha do pai |
| `PageFooter` | base de cada página (ancorada ao fundo) |
| `ReportFooter` | uma vez, no fim |

## 5. Catálogo de elementos

Todos os elementos abaixo **renderizam** na saída (PDF/PNG/SVG/HTML/DOCX). *No canvas do Designer*, os
avançados (Chart/Gauge/Tablix/Map/KPIs) aparecem hoje como **placeholder** — o render real no canvas é um
follow-up; a saída final é completa.

| Elemento | Descrição | Destaques |
|---|---|---|
| **Label** | Texto estático | sem binding |
| **TextBox** | Texto/expressão com `{expr:format}` | `TextRuns` (multi-estilo + ação por-run), `CanGrow`/`CanShrink` |
| **Line** | Linha (horizontal/vertical/diagonal) | direção + pen |
| **Rectangle** | Retângulo, **container** de filhos | cantos arredondados, clip dos filhos, hierarquia |
| **Ellipse** | Círculo/elipse | preenchimento |
| **Image** | Imagem (bytes/caminho/expressão) | `Sizing` Stretch/Fit/Fill/Native |
| **Chart** | Gráfico | **8 tipos**: Bar/Line/Pie/Area/Scatter/Radar/Bubble/Stock; multi-série; legenda |
| **Tablix** | Tabela **+ matrix/pivô** | grupos de linha/coluna aninhados, subtotais, ColSpan, `NoRowsMessage` |
| **Table** | Tabela simples bandada | header/detail/footer por coluna |
| **Barcode** | 1D + QR | Code128/39/Codabar/ITF/EAN/UPC/ISBN/ISSN + QR (ECC) |
| **Gauge** | Medidor radial/linear | Min/Max + faixas coloridas (`Ranges`) |
| **DataBar** | Barra proporcional | normaliza valor em [0..1] |
| **Sparkline** | Mini-gráfico em célula | Line/Column/Area |
| **Indicator** | Ícone KPI por estado | seta/forma/barra de rating/símbolo |
| **Map** | Mapa vetorial | Web Mercator + graticule + shapes GeoJSON + **basemap por tiles (OSM)** + marcadores |
| **Subreport** | Relatório embutido | por ID/registry ou inline; binding de parâmetros + dados |
| **Code** | Bloco C#/VB | `Code.X(...)` via pacote **opt-in** `Reporting.Expressions.Roslyn` (executa C# — só fontes confiáveis) |

## 6. Estilo, binding e formatação condicional

Capacidades **transversais** a (quase) todos os elementos:

- **`Style`** — `Font` (família/tamanho/negrito/itálico/sublinhado/tachado), `ForeColor`/`BackColor`,
  `Border`, `Padding`, `HorizontalAlignment`/`VerticalAlignment`, `Format` (`C`/`N`/`P`/`d`/custom),
  `BackgroundImage`. Cores aceitam `#hex` **ou nome CSS/RDL** (Maroon/Teal/SteelBlue/… — paleta completa).
- **`PropertyExpressions`** — bind **qualquer propriedade** a uma expressão (ex.: `Style.ForeColor` =
  `=IIf(Fields.Total<0,"Red","Black")`), estilo-SSRS. Funciona nos 5 caminhos (model/serial/3 modos).
- **`ConditionalFormats`** — merge aditivo de estilo por condição (negativo-em-vermelho, zebra, semáforo).
- **`Action`** — hyperlink, bookmark-link ou drillthrough (no PDF/HTML viram links navegáveis).
- **`Visible`/`VisibleExpression`** — visibilidade **estática** (resolvida no render; sem toggle interativo).
- **`Bookmark`/`DocumentMapLabel`** — âncoras e mapa do documento (TOC).
- **`CanGrow`/`CanShrink`** — o elemento (e a banda, com opt-in) cresce/encolhe conforme o conteúdo.

## 7. Dados

### Fontes — `Reporting.DataSources` + provedores

- **In-memory**: `IEnumerable<T>` (acesso a membros aninhados pré-compilado), `DataTable`.
- **SQL** via `AdoNet` (provider-agnóstico): SQLite, PostgreSQL, SQL Server, MySQL, Oracle.
- **Semi-estruturado**: JSON, XML, WebService (REST/SOAP), FileSystem.
- Registre via `.DataSource(name, data)` (code-first) ou `AddReporting(...).AddDataSource(...)` (DI).

### DataSets, campos e relações

- `Fields` (inferidos ou declarados), `CalculatedFields` (expressão por linha), `Filters`, `SortExpressions`.
- **Master-detail**: `Relations` (pai→filho) consumidas pela `SubDetailBand`.

### Parâmetros e variáveis

- **`ReportParameter`** tipado: `DataType`, `DefaultValue`, `AvailableValues` (estáticos ou por query),
  multi-valor, `Nullable`, `Hidden`.
- **`ReportVariable`** com escopo Row/Group/Report.

## 8. Expressões

Sintaxe: `Fields.X`, `Parameters.X`, `Globals.PageNumber/TotalPages/ReportName/Language`, `ReportItems.X`.
Em templates de texto: `{Fields.Total:C}`.

- **Agregados** (com escopo Report/Page/Group/Running): `Sum`, `Avg`, `Count`, `CountDistinct`, `Min`, `Max`,
  `First`, `Last`, `RunningTotal`/`RunningValue`, **`Var`/`VarP`/`StDev`/`StDevP`**.
- **Lookup cross-dataset**: `Lookup`, `LookupSet`, **`MultiLookup`** (vetorizado).
- **Posicionais**: `RowNumber`, `CountRows`, `Previous`.
- **Funções VB/SSRS**: texto (`Left`/`Right`/`Mid`/`InStr`/`Replace`/`Trim`/`Format`/…), data
  (`Today`/`Now`/`DateAdd`/`DatePart`/`Year`/`Month`/…), formatação (`FormatCurrency`/`FormatNumber`/
  `FormatPercent`/`FormatDateTime`), matemática e lógica (`IIf`, `Switch`, `Choose`).
- **Conversão automática** de expressões VB-SSRS no import (`Fields!X.Value` → `Fields.X`, `&` → `Concat`,
  `Like` infixo → `Like(...)`).

Detalhes em [expressions.md](expressions.md).

## 9. Paginação

`PaginateAsync()` faz **duas passadas** quando necessário (para `Page.Total` e para gating de última página).
Recursos:

- **Multi-coluna (snake)** — `Columns(n)`; contínuo (rolo térmico) força coluna única.
- **`PrintOnFirstPage`/`PrintOnLastPage`** — suprime header/footer na 1ª/última página.
- **`CanGrow`** — a banda cresce com o conteúdo; **`CanShrink`** (opt-in) — a banda **encolhe** e puxa a
  próxima para cima.
- **Split de banda por elemento** — uma banda mais alta que a página é fatiada elemento-a-elemento entre
  páginas (cada elemento permanece inteiro); um único elemento maior que a página é emitido inteiro (sem loop).
- **`PageBreak`** (Start/End/StartAndEnd) em Detail/Group; **`KeepTogether`**; **`RepeatHeaderOnNewPage`**.

## 10. Saída

### Exportadores — `IReportExporter` (registry plugável)

| Formato | Classe | Notas |
|---|---|---|
| **PDF** | `SkiaPdfExporter` | vetorial, texto selecionável, bookmarks, metadados, papel térmico contínuo |
| **PNG/Imagem** | `PngImageExporter` | raster por página ou composto |
| **SVG** | `SvgExporter` | vetorial |
| **HTML** | `SvgHtmlExporter` | SVG embutido + overlay de links/document-map (a11y) |
| **XLSX** | `ExcelExporter` | fórmulas `=SUM`, zebra, freeze (via ClosedXML) |
| **DOCX (Word)** | `DocxExporter` | tabela editável + imagens inline + **charts rasterizados** |
| **CSV** | `CsvExporter` | RFC 4180 |
| **Markdown** | `MarkdownExporter` | GFM + front-matter |
| **JSON** | `JsonExporter` | schema determinístico (snapshot/RAG) |

```csharp
new DocxExporter().ExportToFile(rendered, "vendas.docx");
new ExcelExporter().ExportToFile(rendered, "vendas.xlsx");
```

### Impressão — `IReportPrinter`

- **`WindowsSpoolerPrinter`** (net10.0-windows): vetorial, duplex, bandejas, page-ranges, "Print to PDF".
- **`EscPosPrinter`**: térmica 58/80 mm, 203 dpi, TCP/serial/USB (Bematech/Daruma/Elgin/Epson). Veja
  [printing.md](printing.md).

## 11. Visualizador (Blazor)

`<ReportViewer />` (`Reporting.Viewer.Blazor`): navegação de páginas, zoom, toolbar de export/print. Roda em
Blazor Server e WASM. Veja o quickstart no [README](../README.md#hospedando-em-aspnet-core--blazor--maui).

## 12. Importar do SSRS (.rdl)

```csharp
var def = new Reporting.Serialization.RdlImporter().ImportXml(rdlXmlString, reportName: "Vendas");
// Perdas conhecidas ficam em def.Metadata["ImportWarnings"] (nunca descarte silencioso).
```

O importador cobre ~80% de um `.rdl` real (DataSets, Tablix flat+matrix, Chart/Gauge/Subreport,
CustomReportItem, estilo, cores nomeadas, cultura, formatação condicional). O que é lossy gera **aviso
explícito**. Detalhes e limites em [rdl-spec-compliance.md](rdl-spec-compliance.md) e
[comparison.md](comparison.md).
