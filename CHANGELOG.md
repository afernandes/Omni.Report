# Changelog

All notable changes to OmniReport are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

225 PRs desde a 0.1.1. A linha condutora foi **interoperabilidade com SSRS/RDL** — ler, editar e
gravar `.rdl` — e a regra de que toda feature chega igual nos **três modos de autoria** (code-first,
API de baixo nível e Designer), com serialização e testes junto. As entradas abaixo são agrupadas por
área em vez de uma por commit; o número entre parênteses é o PR.

### Added

**Interoperabilidade RDL/SSRS**

- **Importador `.rdl`** (SSRS XML → `ReportDefinition`) e programa de conformidade em cima dele:
  estilo e atributos dos itens, `EmbeddedImages`, `CustomProperties`, `Code`, metadados de parâmetro
  (`Hidden`/`Nullable`/`AllowBlank`), variáveis de relatório, `DataSets`, matrix/crosstab, data-viz
  (Chart/Gauge/Subreport), `TextRuns` multi-run, tabela plana (Table/List), `WrapMode`, `Sizing` de
  imagem, `PageBreak`, `Columns`, subtotais de matrix, `Image Source=Database`, `Rectangle` como
  container real com hierarquia, `Gauge ScaleRanges`, propriedades de `<Style>` valoradas por
  expressão (formatação condicional), `CustomReportItem` (DataBar/Sparkline/Indicator/Gauge) e
  metadados report-level. (#88–#126, #135, #136, #145)
- **Exportador `.rdl`** — fecha o ciclo ler → editar → gravar: expressão reversa OmniReport→VB/SSRS,
  itens no `Body` com estilo, `DataSets`/`ReportParameters`/`Variables`, `<Tablix>` matrix, e
  reconstrução de tabela plana com `ColSpan` e lacunas via grade de fronteiras. (#151–#157)
- **Round-trip sem perda**: campos sem equivalente em RDL viajam em `<CustomProperties>`, e o export
  é **validado contra o XSD oficial 2016/01** em teste. (#162, #164, #166)
- `<Report><Language>` dirige a cultura do render; `Globals!ReportName`, `Globals!Language` e
  `ReportItems!Name.Value` resolvem em expressão. (#101, #105, #111)

**Tablix e matrix**

- **Crosstab/matrix** com grupos aninhados N×N nas três superfícies. (#15, #16, #21)
- Subtotais por grupo, total geral, `ColumnSubtotals` e rótulos de total configuráveis. (#86, #98)
- `ColSpan`/`RowSpan` — mescla de células. (#121, #122)
- `SortExpression` de grupo passa a renderizar; `NoRowsMessage` para dataset vazio. (#84, #110)
- **Formatação condicional por célula**: na matrix, `Value` expõe o agregado da interseção. (#73, #206)
- **Paginação de Tablix grande**: row-level para matrix e para tabela plana, e **paginação
  horizontal de colunas** (tiling 2D, "Across then Down") para crosstab largo. (#197, #207, #209)

**Expressões**

- Vocabulário SSRS: condicionais, texto, data (`DateAdd`/`DateDiff`/`DatePart`/`MonthName`),
  conversões, formatação (`FormatCurrency`/`Number`/`Percent`/`DateTime`), e as funções de texto
  restantes (`Space`, `StrDup`, `StrReverse`, `Asc`, `Chr`, `Val`, `InStrRev`, `StrComp`,
  `StrConv`). (#80, #81, #107, #113)
- Funções posicionais e de escopo: `RowNumber`, `Previous`, `First`, `Last`, `CountRows`,
  `CountDistinct`, e `RunningValue` (agregado acumulado). (#93, #159)
- Busca entre datasets: `Lookup`, `LookupSet`, `MultiLookup`. (#85, #146)
- Agregados estatísticos `Var`/`VarP`/`StDev`/`StDevP`. (#130)
- **Qualquer propriedade alterável por expressão** (modelo `fx` do SSRS), com coerção de cor
  nomeada. (#35, #136, #137)

**Designer**

- **PropertyGrid orientado a metadados**: um atributo `[PropertyGrid]` no modelo passa a gerar o
  editor, com flattening de tipos aninhados, agrupamento por categoria, editores de lista e de
  dicionário genéricos, e botão `fx` por propriedade abrindo o editor Monaco. Toda propriedade
  escalar é bindável por expressão por padrão. (#33–#52)
- **Toolbox auto-descoberto por reflexão** — `[ToolboxElement]` vira a fonte única do elemento e
  elimina quatro switches por tipo. (#78, #79)
- **Canvas WYSIWYG**: imagem embutida renderiza de verdade, data-viz mostra amostra representativa,
  Tablix aparece como grade estrutural, e Barcode/QR/Map ganham preview. (#171–#175)
- Edição aninhada de `Rectangle` — filhos visíveis, selecionáveis e editáveis. (#141)
- Align/distribute de multi-seleção, copy/paste sobre a seleção inteira. (#176, #202)
- Menu Importar/Exportar RDL e Exportar DOCX. (#169)
- Autoria completa de parâmetros — incluindo validação de `Required` no prompt, cascading e re-query
  interativo no Preview. (#24–#27, #177, #192, #193)
- Elementos antes só editáveis por código passam a ser construíveis no Designer: **Map** completo
  (graticule, shapes, cores, basemap), **Code** (Source/Language), **Subreport**
  (ReportId/Data/Parâmetros), cor de preenchimento de Rectangle/Ellipse, e os props de paginação do
  Tablix (`MinColumnWidth`/`RepeatColumnHeaders`/`KeepTogether`). (#10, #11, #12, #32, #211)

**Estilo**

- **Gradiente de fundo** (duas cores + direção, alinhado ao RDL), inclusive em formatação
  condicional. (#179, #180, #190)
- **Named styles** (`Style.BasedOn` + `NamedStyles`): modelo, API code-first, picker no Designer,
  criação a partir da seleção, renomear/excluir com atualização de referências. (#181–#183, #188,
  #189, #194)
- Paleta CSS3/RDL de cores nomeadas completa. (#144)
- `Style.BackColor` pinta fundo de qualquer elemento; `Style.BackgroundImage` (External). (#103, #134)
- Células de valor da matrix honram estilo e preenchimento do template. (#184, #204)

**Saída**

- Novos exportadores: **PNG** (#83), **Word `.docx`** tabular via OpenXML com imagens inline e
  visuais rasterizados (#127, #129, #138), **XML estruturado** (#185) e **TIFF multi-página**
  (encoder baseline manual, sem dependência nova) (#195).
- `ExportAsync` com `CancellationToken` em `IReportExporter`. (#227)
- **Avisos de degradação na exportação** — a perda de conteúdo deixa de ser silenciosa. (#228)

**Layout e paginação**

- Motor de **multi-coluna** (snake/jornal). (#118, #119)
- Split de banda por elemento quando excede a página; `CanShrink` encolhe a banda;
  `PrintOnLastPage`; `RepeatHeaderOnNewPage`. (#142, #143, #167)
- `ImageSizing` (Fit/Fill/Native/Stretch) honrado em todos os backends. (#114, #115)
- `TextDecoration` (underline/strikeout) no renderer Skia. (#100)
- Clip de overflow — ciente de `CornerRadius` — nos filhos do container `Rectangle`. (#128, #133)
- Gradiente no backend GDI+. (#221)

**Parâmetros**

- **Available Values** (domínio estático ou por query), **cascading/dependent parameters** e
  `DefaultValue` por expressão (`=Today()`, `=DateAdd(...)`, `=Parameters!X`). (#87, #160, #191–#193)

**Gráficos**

- Tipos Area, Scatter, Radar, Bubble e Stock, com render, serialização e code-first. (#14, #18, #19)

**Outros**

- Subreport com render real no engine (paginação recursiva). (#13)
- Interatividade no HTML: `Action` vira link, `Bookmark` vira âncora, `DocumentMap` vira TOC
  navegável. (#17, #20)
- Basemap por tiles raster (Web Mercator) com resolver plugável no Map. (#31)
- `DetailBand.DataSetName` — vínculo explícito banda→dataset. (#112)

### Changed

- **Serialização por convenção**: elementos novos passam a ser serializados sem editar os quatro
  switches, primeiro para escalares e depois recursivamente (records posicionais, coleções e
  aninhados). Somado ao toolbox auto-descoberto, adicionar um elemento deixou de exigir alterações
  espalhadas. (#74–#77)
- `Style.Format` passou a ser honrado de forma consistente — célula plana de Tablix, rótulos de KPI
  (Gauge/DataBar), eixo de valores do gráfico e valor único; a célula plana passou a honrar também o
  `Style` do conteúdo (fonte, cor, alinhamento). (#66, #69, #70, #71, #72)
- `RdlImporter`/`RdlWriter` divididos em partials por bloco. (#230)
- `ElementViewModel` teve o mapeamento de domínio extraído para facetas. (#226)
- `HttpClient` compartilhado nos três data sources HTTP, evitando esgotamento de sockets. (#201)
- Cada fonte de dados passou a ser lida **uma vez** por paginação, em vez de duas. (#225)
- Medição da matrix cacheada em `EffectiveElementBottom`. (#203)
- Cinco `catch` nus em `Layout` passaram a filtrar por tipo, em vez de mascarar bugs reais. (#220)

### Fixed

- **`ReportPaginator` singleton vazava estado entre requisições concorrentes**, corrompendo headers
  e código entre relatórios servidos ao mesmo tempo. (#216)
- **Drill-down virava relatório estático**: o importador descartava `ToggleItemId`/`InitiallyHidden`.
  (#217)
- **Export RDL perdia `MinColumnWidth`/`RepeatColumnHeaders`/`KeepTogether`** do Tablix — perda de
  dados no round-trip. (#212)
- `DefaultValue` de parâmetro deixava de ser importado em silêncio; `DefaultValueExpression` se
  perdia no round-trip do Designer. (#158, #163)
- `.repx` não persistia `CanGrow`/`CanShrink`; `.repjson` não tinha paridade com `.repx`
  (master-detail, sort, filter, variáveis); `BarcodeElement.QrEcc` faltava nos quatro caminhos.
  (#29, #30, #55)
- O Designer materializava `Font`/`ForeColor` herdados como literais no round-trip, quebrando a
  herança de named styles. (#222)
- "Abrir .repx…" e "Importar RDL…" não abriam o arquivo — o menu fechava antes do diálogo. (#170)
- Chart e KPI passaram a honrar a cultura do relatório na formatação numérica. (#132)
- Largura/altura do Tablix passaram a ser derivadas das colunas/linhas no import (RDL não traz
  `<Width>`). (#150)
- Uma leva de correções de binder e de editores do Designer vinda de auditorias sucessivas —
  caminhos via struct, `Unit`, enums fora de faixa, estado local dos editores de lista e dicionário,
  alpha no color picker, preservação de `Style` herdado. (#53–#63)
- **Integridade de entrega**: o stub do Android era publicado como pacote de verdade, o workflow de
  release empacotava sem rodar a suíte, e o README exibia imagens por caminho relativo — quebradas
  na página do NuGet. (#231)

### Security

- NCalc 6 e SQLitePCLRaw 3.x, esta última fechando o advisory GHSA-2m69-gcr7-jv3q. (#9)

### Infrastructure

- **Projeto de benchmarks** (BenchmarkDotNet) cobrindo paginação, expressões e export, com linha de
  base medida em `docs/benchmarks.md`. (#229)
- Redes de paridade por reflexão para os serializadores e para o caminho RDL, round-trip por
  propriedade com gerador seeded, e caracterização das limitações conhecidas de paginação. (#64,
  #140, #165, #219)
- **`ROADMAP.md`** — 43 itens priorizados de P0 a P4, com evidência por item. (#218)
- Documentação: guia do usuário e do desenvolvedor, comparação com RDL e concorrentes,
  **especificação formal do formato** (v1.0) e matriz de conformidade RDL mantida em dia. (#148,
  #161, e as reconciliações #123, #147, #178, #187, #200, #213)

## [0.1.1] — 2026-06-18

Correções e polimento do Designer após a primeira release pública, mais ajustes de
empacotamento/CI. Sem mudanças de API pública.

### Fixed

- **Designer · texto vazando dos limites do elemento**: o conteúdo de um TextBox/Label podia
  transbordar a caixa no canvas. Agora cada elemento clipa o próprio conteúdo (`overflow: hidden`
  por elemento). (#5)
- **Designer · scroll no zoom**: ao ampliar a página o scroll deixava de aparecer — o
  `transform: scale` é só pintura e não cresce a caixa de layout, então o container de rolagem não
  via o conteúdo ampliado. O canvas passa a reservar o espaço extra via margem, preservando o
  `transform: scale` (réguas e `getCanvasZoom` intactos). (#6)
- **Designer · fundo roxo da banda Sub-Detail**: a faixa havia perdido o tom roxo de fundo —
  estilo restaurado. (#6)
- **Designer · botão Snap agora funciona de verdade**: o toggle era apenas visual; o motor JS
  usava `snap = true` fixo, então arraste/redimensionamento sempre grudavam na grade. Agora há um
  estado global (`snapEnabled`/`setSnap`) capturado no início de cada arraste, e as smart-guides
  também o respeitam. (#6)
- **Designer · botão Régua agora funciona**: o botão não tinha ação. Passa a mostrar/ocultar as
  réguas (horizontal/vertical + canto), com o canvas ocupando o espaço quando ocultas. (#6)
- **Empacotamento · bundle de CSS scoped (404)**: o `PackageId` aplicado a todos os projetos
  vazava o prefixo `AndersonN.Omni.Report.` para o app de exemplo, renomeando seu bundle scoped
  (`{PackageId}.styles.css`) e gerando 404. O `PackageId` ficou restrito às bibliotecas de
  produção e o sample passou a usar `MapStaticAssets()`/`@Assets[...]`. (#6)

### Changed

- **CI/CD · release automatizado**: `release.yml` publica nos dois feeds (NuGet.org via Trusted
  Publishing/OIDC, sem API key de longa duração + GitHub Packages), envia símbolos (`.snupkg`) e
  cria o GitHub Release automaticamente. O push itera arquivo a arquivo (o glob não expande no
  runner Windows). (#2, #3)
- **README**: seção **Galeria** com renders reais dos samples, badge do NuGet e seção de
  instalação dos pacotes. (#3, #4)

## [0.1.0] — 2026-06-18

Primeira release pública. Engine de relatórios bandado completo: **36 bibliotecas** publicáveis em
`src/`, **5 samples** e **726 testes** (22 projetos de teste), build limpo (0 warnings). Cobre as
11 etapas do roteiro de construção (núcleo → hosting, concluídas em 2026-05-24) e os recursos
avançados que vieram por cima: gráficos e KPIs com renderização nativa, Tablix, Map vetorial,
conectores de dados, exporters extras (SVG/HTML/CSV/JSON/Markdown), código de barras e o bloco
`Code` C# opt-in.

### Added

#### Núcleo (Etapa 1)
- `Reporting.Core`: modelo de domínio imutável (`ReportDefinition`, `PageSetup`,
  `ReportBand`, `GroupBand`, `DetailBand`, todos os tipos de `ReportElement`,
  `Style`, `Border`, `Font`, `Unit` em mils, `EquatableArray<T>`,
  `EquatableDictionary<K,V>`).
- `Reporting.Expressions`: engine NCalc estendida com `Fields.*`/`Parameters.*`/
  `Variables.*`/`Page.*`, agregados (`Sum`/`Avg`/`Count`/`Min`/`Max`/`RunningTotal`)
  com scope (`Report`/`Group`/`Page`/`Running`), `TemplateRenderer` para
  interpolação `{expr:fmt}`, member-path resolver para acessos aninhados
  (`Fields.Cliente.Nome`).
- `Reporting.DataSources`: `IReportDataSource`, `EnumerableDataSource<T>` com
  accessors compilados via `Expression<Func<T, object>>` cacheados,
  `DataTableDataSource`, `DataSourceRegistry`.

#### Renderização (Etapa 2)
- `Reporting.Rendering`: abstrações `IRenderingContext`, `IPathBuilder`,
  `TextStyle`/`PenStyle`/`BrushStyle`, `AverageWidthTextMeasurer` headless.
- `Reporting.Layout`: paginador two-pass (`ReportPaginator`) com suporte a
  `GroupBand`, `KeepTogether`, `NewPageBefore/After`, `RepeatHeaderOnNewPage`,
  `CanGrow`/`CanShrink`, `Page.Total` em duas passadas. Primitivos
  `DrawText/Line/Rectangle/Ellipse/Image`.
- `Reporting.Rendering.Skia`: `SkiaRenderingContext` (PNG por página, PDF
  rasterizado via `SKDocument`), `SkiaPrimitiveRenderer` shareable stateless.

#### Code-first API (Etapa 3)
- `Reporting.CodeFirst`: `ReportBuilder.Create(...)` com `Page`, `Parameters`,
  `DataSource<T>`, `ReportHeader`, `PageHeader`, `Group`, `Detail`,
  `PageFooter`, `ReportFooter`, `Metadata`. `BandContent` single-fluent-surface
  com `Text`/`Label`/`Line`/`Rectangle`/`Ellipse`/`Image`/`Barcode` e configuração
  granular (`At/Size/Font/Bold/Center/AlignRight/Color/Format/...`).
- `FieldPathBuilder.From<T>(Expression<Func<T, object>>)` converte typed lambdas
  em strings de path.
- 3 samples brasileiros: `Sample01_VendasPorCliente`, `Sample02_EspelhoProdutos`,
  `Sample03_RelatorioCaixa`.

#### Serialização (Etapa 4)
- `Reporting.Serialization`: `RepxSerializer` (XML, schema versionado,
  migrations via `IRepxMigration`), `RepJsonSerializer` (JSON via
  `System.Text.Json.Nodes`). Round-trip lossless verificado em fixture
  kitchen-sink + 3 samples de produção.

#### Saídas (Etapa 5)
- `Reporting.Output.Pdf`: `SkiaPdfExporter` vetorial nativo via `SKDocument` —
  texto selecionável, metadados embarcados (Title/Author/Subject/Keywords).
- `Reporting.Output.Excel`: `ExcelExporter` via ClosedXML com grid
  quantization, classificação de linhas (Header/Detail/GroupHeader/Subtotal/Total)
  e fórmulas `=SUM(...)` automáticas para colunas numéricas em linhas de subtotal.

#### Impressão Windows (Etapa 6)
- `Reporting.Printing`: abstrações cross-platform (`IReportPrinter`,
  `PrinterInfo`, `PrinterCapabilities`, `PrintOptions`, `PrintResult`,
  `DuplexMode`).
- `Reporting.Rendering.Gdi`: `GdiRenderingContext` (TFM `net10.0-windows`)
  sobre `System.Drawing.Graphics`. Construtores duplos: bound (para
  `PrintDocument`) ou standalone (Bitmap por página, p/ testes).
- `Reporting.Printing.WindowsSpooler`: `WindowsSpoolerPrinter` com `PrintDocument`
  + `GdiRenderingContext` — vetorial no spooler, suporta `PrintToFile`
  (Microsoft Print to PDF / XPS), Duplex, Copies, PaperBin.

#### Cross-platform impressão (Etapa 7)
- `Reporting.Printing.EscPos`: `EscPosPrinter` para impressoras térmicas
  brasileiras (Bematech/Daruma/Elgin/Epson TM-T*). Rasterização SkiaSharp a
  203 dpi, `GS v 0` raster commands, corte automático. Transports plug-and-play:
  `StreamEscPosTransport`, `TcpEscPosTransport` (porta 9100), `SerialEscPosTransport`.
- `Reporting.Printing.Android` (compilação condicional): stub `net10.0` lança
  `PlatformNotSupportedException`; real `net10.0-android` com `PrintManager` +
  `PrintDocumentAdapter` (PDF in-memory) gated em `OMNIREPORT_BUILD_ANDROID=true`.
- `Sample04_CupomNfce` — papel térmico 80mm com identificação fiscal real
  (CNPJ, chave de acesso 44 dígitos, protocolo SEFAZ, Lei 12.741/12).

#### Viewer Blazor (Etapa 8)
- `Reporting.Viewer.Blazor`: `<ReportViewer />` Razor Class Library com
  toolbar (navegação/zoom/export PDF&XLSX/print), zoom puro CSS transform
  (sem re-render server-side), download via JS interop (`omniViewer.download`
  → Blob + createObjectURL).
- Sample `Reporting.Samples.BlazorServer` com galeria dos 4 reports.

#### Designer Blazor (Etapa 9, MVP)
- `Reporting.Designer.Blazor`: `<ReportDesigner />` com shell completo
  (TopBar/Toolbar/StatusBar/3 painéis), ViewModels mutáveis observáveis
  (`Notifying` base leve, sem MVVM Toolkit), `ElementToolbox` com 7 tipos,
  `BandCanvas` SVG inline com selection ring, `PropertyGrid` reflexivo,
  Save/Load `.repx` via `RepxSerializer`, Preview modal in-process via Skia.
- Command pattern + `CommandHistory` com 5 comandos concretos (Add/Remove/Move/
  Resize/ChangeProperty), undo/redo + limite configurável.
- Atalhos: Ctrl+Z/Y/S/N/O, Delete, Setas (1mm) / Shift+Setas (10mm), Esc.
- Design package "Print Studio" importado (`wwwroot/css/tokens.css` +
  `Docs/COMPONENTS.md`/`DESIGN-TOKENS.md`/`KEYBOARD-SHORTCUTS.md` como contrato
  visual vinculante).

#### MAUI Blazor Hybrid (Etapa 10)
- `Reporting.Samples.MauiHybrid`: TFM `net10.0-windows10.0.19041.0` (sempre) +
  `net10.0-android` (condicional via `OMNIREPORT_BUILD_ANDROID=true`).
  `BlazorWebView` hospedando os mesmos componentes do sample Blazor Server.
  DI por plataforma via `#if WINDOWS / #if ANDROID` registrando
  `WindowsSpoolerPrinter` ou `AndroidPrintFrameworkPrinter`.

#### Hosting + docs + CI (Etapa 11)
- `Reporting.Hosting.AspNetCore`: `services.AddReporting(opts => opts
  .UseSkiaRendering().UsePdfOutput().UseExcelOutput()
  .UsePrinter<...>().AddDataSource(...))`.
- README com diagrama Mermaid de arquitetura + quickstarts.
- `docs/` com guias por área (expressions, data-sources, designer, printing).
- `.github/workflows/ci.yml`: jobs `build-windows`, `build-linux`, `pack`.

#### Gráficos e KPIs (renderização nativa)
- `Reporting.Core.ChartElement` agora **renderiza** (antes era só round-trip): barras agrupadas,
  linhas e pizza, com eixos, gridlines, rótulos, título e legenda. `ChartRenderer` produz
  primitivos vetoriais (padrão `BarcodeRenderer`), consumidos por todos os backends.
- Medidores KPI renderizando via `KpiRenderer`: **Gauge** (radial com anéis/ponteiro + linear
  bullet-style), **DataBar** (barra proporcional), **Sparkline** (line/column/area) e
  **Indicator** (seta direcional/forma/rating por faixa de estado).
- Novo primitivo `DrawPolygonPrimitive` (polilinha/polígono preenchível) com `BuildPath`
  compartilhado; mapeado em `RenderedReportPlayer` (Skia/GDI/Viewer), `SkiaPdfExporter`,
  `SvgExporter` e `JsonExporter`.
- API fluente code-first: `.Chart()/.Series()/.Legend()`, `.Gauge()/.DataBar()/.Range()/.GaugeBand()`,
  `.Sparkline()`, `.Indicator()/.State()`.
- `TablixElement` **renderiza** como tabela bandada (cabeçalho + linha de detalhe por registro +
  gridlines, auto-crescimento) via `TablixRenderer`, com fluente `.Tablix(t => t.Column(...))`.
  Matrix e grupos de linha/coluna aninhados seguem como evolução.
- `MapElement` **renderiza um mapa de verdade**: projeção **Web Mercator** (proporção preservada),
  **graticule** (grade lat/long com rótulos em graus), **camada de shapes GeoJSON** (polígonos
  preenchidos / linhas) como basemap vetorial offline, e marcadores projetados por cima. Shapes vêm
  de GeoJSON inline (`.Shapes(...)`) ou de um conjunto nomeado (`.ShapeSet("brazil")`) resolvido pelo
  `MapShapeRegistry`. Fluente: `.Map(lat, lon).ShapeSet(...).Shapes(...).Graticule().ShapeColors(...)`.
  Novo pacote opcional **`Reporting.Maps`** com shapes embutidos simplificados (`MapShapes.RegisterBuiltIns()`);
  basemap de **tiles online (OSM/Bing)** segue como camada opt-in futura.

#### Conectores de dados
- `Reporting.DataSources.AdoNet` (agnóstico) + wrappers `SQLite`, `PostgreSQL`, `SQL Server`,
  `MySQL`; `JSON`, `XML`, `WebService`/REST e `FileSystem`. Streaming async, parametrização
  segura, inferência de schema.

#### Exporters
- `Output.Svg`, `Output.Html` (SVG embutido + CSS de impressão), `Output.Csv` (RFC 4180),
  `Output.Json` (schema estável de primitivos) e `Output.Markdown` (GFM). Cobertura de testes
  criada para os cinco (antes sem testes).

#### Designer e master-detail
- Master-detail/sub-bandas (relações pai→filho) no paginador e no designer.
- `Reporting.Designer.Blazor.DataConnect` (conexão/schema/query/preview/relações/campos calculados),
  impressão no designer (browser universal + adapter nativo opt-in), formatação condicional,
  validação de expressão inline e elementos RDL Phase 1.
- Editores visuais no Designer para os 7 elementos avançados (Chart, Tablix, Gauge, DataBar,
  Sparkline, Indicator, Map): adicionáveis pelo toolbox, placeholder no canvas, property grid
  por tipo (séries, faixas, estados, colunas) e round-trip lossless no `.repx`.

#### Código de barras
- `Reporting.Barcode`: encoders gerenciados 1D (Code128/39/Codabar/ITF/EAN-13/EAN-8/UPC-A/ISBN/ISSN)
  e QR Code 2D — geometria vetorial escalável.

#### Código customizado (opt-in)
- `Reporting.Expressions.Roslyn`: pacote **opt-in** que compila o bloco `Code` (C#) via
  `Microsoft.CodeAnalysis` e resolve `Code.MethodName(...)` nas expressões. O núcleo
  (`Reporting.Expressions`) ganha só um ponto de extensão (`CodeFunctionResolver`, `null` por
  padrão) — sem a dependência pesada e sem executar C# a menos que o host habilite via
  `PaginationRequest.CodeFunctionResolver` / `RoslynCode.CreateResolver(...)`. ⚠ Executa C#
  embutido no relatório: use apenas com fontes confiáveis.

#### Designer · réguas e UX honesta
- **Réguas reais (horizontal + vertical)** no canvas: motor em `<canvas>` que mede a página viva a
  cada scroll/zoom/resize e redesenha, então "0" fica na borda do papel em qualquer zoom/scroll.
  Rótulos com **troca de unidade** (cm/mm/pol pelo canto), **marcador da posição do mouse**,
  **sombreamento da extensão da seleção** e **guias arrastáveis** das réguas (criar arrastando ou
  clicando, mover, apagar arrastando para fora/duplo-clique) — com **snap dos elementos às guias**.
- **Subreport/Code preservados** no round-trip do Designer (antes viravam TextBox e perdiam config).
- **DataBar e Indicator** agora são adicionáveis pela toolbox (seção "Avançados").
- **Largura de coluna do Tablix** editável no PropertyGrid (mapeia `TablixElement.ColumnWidths`).
- Remoção de dados fixos/enganosos das telas: footer (papel·orientação, contagens e seleção reais;
  sem "main"/"conectado"/"UTF-8·CRLF"), TopBar (sem botões stub) e diálogo **Sobre** com versão real
  lida do assembly.

#### Exemplos dos recursos novos
- 4 novos samples code-first demonstrando os mecanismos acima end-to-end (PDF/PNG/SVG/HTML/…):
  `Sample12_Dashboard` (bar/pie/line + Gauge com faixas + Sparkline + Indicator + DataBar por
  linha), `Sample13_TabelaProdutos` (Tablix), `Sample14_MapaFiliais` (Map por lat/long) e
  `Sample15_CodigoCustomizado` (bloco `Code`/Roslyn opt-in chamando `Code.Imposto/Liquido/Faixa`).

### Fixed
- **Agregado de escopo `Report` em bandas iniciais**: `Sum/Avg/Count/Min/Max` sem scope explícito
  agora resolvem o total do dataset em **qualquer** banda — inclusive `ReportHeader`/`PageHeader`,
  que renderizam antes do loop de detalhe. Antes avaliavam contra um acumulador vazio (→ 0), o que
  fazia um Gauge/Indicator de total no cabeçalho aparecer zerado. O paginador agora prima o escopo
  `Report` com o conjunto completo de linhas (semântica SSRS); rodapés permanecem idênticos.
- **Serialização `.repjson` dos elementos avançados**: `RepJsonSerializer` lançava
  `Unsupported element type` ao salvar Tablix/Code/Map/Gauge/DataBar/Sparkline/Indicator. Escrita
  e leitura JSON agora cobrem os 7 elementos, além de `TextRuns` e das extensões RDL de base
  (`Action`/`Bookmark`/`DocumentMapLabel`/`ToggleItemId`/`InitiallyHidden`) — paridade lossless
  total com o `.repx`.

### Notes
- Os 7 elementos avançados RDL **renderizam** (Chart, Tablix, Gauge, DataBar, Sparkline,
  Indicator, Map) e o bloco `Code` C# **avalia** via pacote opt-in. Evoluções restantes:
  Tablix matrix/grupos de linha-coluna aninhados, tiles de basemap no Map, e editores visuais
  desses elementos no Designer.

### Metrics
- **36 bibliotecas publicáveis** em `src/`.
- **5 samples**: CodeFirst (console), WindowsPrinting (console), BlazorServer (web),
  DatabaseReport (console) e MauiHybrid (desktop + mobile).
- **726 testes** (22 projetos) — xUnit + FluentAssertions + bUnit + PdfPig +
  ClosedXML readback.
- **Cobertura ≥ 80%** nos projetos com lógica testável (Core, Expressions, DataSources, Layout,
  Rendering, Rendering.Skia, Rendering.Gdi, CodeFirst, Serialization, Output.Pdf, Output.Excel,
  Printing.EscPos).
- **Build limpo** (0 warnings, TreatWarningsAsErrors em produção).

[Unreleased]: https://github.com/afernandes/Omni.Report/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/afernandes/Omni.Report/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/afernandes/Omni.Report/releases/tag/v0.1.0
