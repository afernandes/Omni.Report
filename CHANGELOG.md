# Changelog

All notable changes to OmniReport are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Nada ainda.

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
