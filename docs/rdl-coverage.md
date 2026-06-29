# Cobertura RDL — o que renderiza × o que só faz round-trip

Esta matriz é a fonte de verdade sobre **o que o engine do OmniReport realmente desenha**
versus o que o modelo apenas **serializa/desserializa** (round-trip lossless em `.repx`/`.repjson`)
sem render visual ainda. Use-a para calibrar expectativas em relação a Crystal Reports / SSRS /
FastReport.

Legenda:

- ✅ **Render** — o engine produz saída visual (Skia/GDI/SVG/PDF/HTML/…).
- 🔁 **Round-trip** — modelo, builder e serialização funcionam; ainda **não** desenha.
- 🟡 **Parcial** — uma parte renderiza; a outra está em 🔁.

## Elementos básicos

| Elemento | Render | Round-trip | Observações |
|---|:--:|:--:|---|
| TextBox / Label | ✅ | ✅ | `TextRuns`, formatação condicional, autosize (`CanGrow`/`CanShrink`) |
| Line | ✅ | ✅ | |
| Rectangle | ✅ | ✅ | |
| Ellipse | ✅ | ✅ | |
| Image | ✅ | ✅ | embarcada ou por caminho/URL |
| Barcode | ✅ | ✅ | 1D (Code128/39/Codabar/ITF/EAN-13/EAN-8/UPC-A/ISBN/ISSN) e QR 2D |

## Elementos avançados (RDL)

| Elemento | Render | Round-trip | Observações |
|---|:--:|:--:|---|
| Chart | ✅ | ✅ | **8 tipos** — Bar, Line, Pie, Area, Scatter, Radar, Bubble, Stock — renderizam; **construíveis e editáveis no Designer** (dropdown de tipo + campos de série, incl. Size/High/Low de Bubble/Stock) + code-first `.Chart()/.Series()/.BubbleSeries()/.StockSeries()` |
| Gauge | ✅ | ✅ | radial (anéis/ponteiro) + linear bullet |
| DataBar | ✅ | ✅ | barra proporcional |
| Sparkline | ✅ | ✅ | line / column / area |
| Indicator | ✅ | ✅ | seta direcional / forma / rating por faixa |
| Tablix | ✅ | ✅ | tabela plana e **matrix/pivô com grupos aninhados** (N níveis de linha × N de coluna; soma por interseção-folha; níveis externos com visual "outline") — **construível e editável no Designer** (toggle Crosstab + editores de grupo multi-linha) + code-first `.Tablix(t => t.RowGroup().RowGroup().ColumnGroup().Cell())`. **Paginação 2D**: matrix/tabela-plana mais alta que a página quebra **por linha** reimprimindo o cabeçalho de coluna (`RepeatColumnHeaders`/`KeepTogether`, #197 matrix / #207 flat; Sample18), e mais larga que a página quebra **por coluna** em tiles ("Across then Down", opt-in `MinColumnWidth`, #209; Sample19) — compõem numa grade de tiles. Os 3 flags são editáveis no Designer (grupo "Paginação", #211). Células de valor honram o estilo do template (ForeColor/Font/alinhamento/**Fill** #204) e **conditional format por-célula** via `Value` (#206) |
| Map | ✅ | ✅ | **vetorial** 100% (Web Mercator + graticule + shapes GeoJSON + marcadores) **e tiles raster** — o engine calcula a grade de tiles Web Mercator e emite as imagens via `PaginationRequest.MapTileResolver` (plugável); `Basemap`/URL-template editável nas 3 superfícies (`.Basemap()` no code-first). O fetch HTTP em si fica num provider opt-in (rede), igual a imagem-por-URL |
| Subreport | ✅ | ✅ | renderiza o filho (inline ou via resolver de id) na largura do subreport, com bindings de parâmetro avaliados no contexto pai; code-first `.Subreport()`/`.SubreportInline()` + editor no Designer |
| Code (bloco C#) | — | ✅ | não é "render": **avalia** via pacote opt-in `Reporting.Expressions.Roslyn` (`Code.Metodo(...)`). ⚠ executa C#: use só com fontes confiáveis |

## Recursos de interação

| Recurso | Render | Round-trip | Observações |
|---|:--:|:--:|---|
| `Action` (link/bookmark/drill-through) | ✅ | ✅ | hyperlink / bookmark-link / drill-through viram **link clicável no HTML** (overlay `<a>` posicionado em mm sobre o elemento); editável no Designer. PDF link annotations: futuro |
| Drill-down / toggle (`ToggleItemId` + `InitiallyHidden`) | ❌ | ✅ | modelo round-trippa; colapso interativo é um **limite arquitetural** — todo render é Skia (SVG/PDF/HTML/preview achatados, sem nó por elemento), exigiria um pipeline DOM-render (ver [Limites conhecidos](#limites-conhecidos-decisão-de-escopo)) |
| `DocumentMapLabel` / `Bookmark` | ✅ | ✅ | **Bookmark** vira âncora (`id="bm-…"`) e **DocumentMap vira um TOC navegável** (`<nav class="docmap">` no topo do HTML, linkando às âncoras `dm-…`) |

## Saídas (exporters)

| Formato | Status | Observações |
|---|:--:|---|
| PDF (`Output.Pdf`) | ✅ | vetorial via SkiaSharp, texto selecionável, metadados |
| Excel (`Output.Excel`) | ✅ | ClosedXML, classificação de linhas, fórmulas de subtotal |
| SVG / HTML | ✅ | SVG embutido + CSS de impressão |
| CSV / JSON / Markdown | ✅ | RFC 4180 / schema estável de primitivos / GFM |
| ESC/POS (térmica) | ✅ | raster 203 dpi, corte automático |
| Word/DOCX | ❌ | **limite conhecido** — Word é layout de fluxo; o engine é canvas posicionado. Exige um motor de fluxo dedicado (ver [Limites conhecidos](#limites-conhecidos-decisão-de-escopo)) |

## Limites conhecidos (decisão de escopo)

Dois itens **não** são entregues como render real — por restrições arquiteturais reais, não por
estarem "pela metade". Ficam aqui documentados com honestidade para não virarem surpresa:

1. **Drill-down / toggle de visibilidade** — todo o render do engine passa pelo Skia: SVG e PDF saem
   de `SKSvgCanvas` (achatado, sem `<g>` por elemento) e o preview do Designer é PNG rasterizado.
   Não há nó por elemento para mostrar/ocultar, então o colapso interativo (com reflow) exigiria um
   **pipeline DOM-render** novo (cada elemento como nó HTML posicionado e ocultável) — uma adição
   grande e ortogonal ao pipeline atual. O modelo (`ToggleItemId`/`InitiallyHidden`) round-trippa.

2. **Word/DOCX** — o engine posiciona tudo em canvas absoluto (mils); o Word é **fluxo**
   (parágrafos/tabelas que refluem). Um DOCX fiel precisa de um **motor de layout de fluxo** à parte,
   não de um simples serializador — esforço comparável a um novo backend.

> **Map tiles** saiu desta lista: o engine agora calcula a grade Web Mercator e emite os tiles via
> `MapTileResolver` plugável. Só o **provider HTTP** (fetch de rede dos tiles) fica como pacote
> opt-in — análogo a uma imagem-por-URL, não um limite de render.

Os demais 17 itens da matriz renderizam de verdade e são autoráveis nas três superfícies.

## As três superfícies de autoria

Todo elemento que renderiza deve ser **autorável das três formas** — é um invariante do projeto:

1. **Code-first** — API fluente `ReportBuilder.Create(...)` (`.Chart()`, `.Tablix()`, `.Map()`, …).
2. **Low-level** — canvas sem bandas (primitivos + elementos posicionados manualmente).
3. **Designer** — toolbox + canvas + PropertyGrid no `Reporting.Designer.Blazor`.

### Autorabilidade no Designer (construir e editar do zero)

Invariante mais forte: **todo parâmetro que um elemento renderiza deve ser editável ao construir do
zero** — não basta round-trippar quando se carrega um `.repx` já configurado. Uma auditoria
adversarial encontrou 16 lacunas "round-trip sem editor"; **12 foram fechadas** (com testes que
constroem o valor do zero → `ToElement` emite → `FromElement` recupera):

- Line: orientação (horizontal/vertical/diagonal) · Image: caminho/URL, expressão (data-bound) e
  sizing · Barcode: simbologia, texto e ECC (QR) · Sparkline: categoria · TextBox: autosize
  (`CanGrow`/`CanShrink`) e visibilidade condicional (`VisibleExpression`) · Chart: cor por série ·
  Indicator: ícone por estado · Rectangle: cantos arredondados · **Parâmetros do relatório**
  (prompt/obrigatório/multi — antes nem eram salvos) · Visibilidade de banda (estática + expressão).

As **4 restantes são features**, não simples editores faltando — ficam como follow-ups:

1. **Tablix — sort por grupo**: o editor de grupos é multi-linha (uma expressão por linha); capturar
   `SortExpression`/direção por nível pede um editor estruturado por grupo.
2. **Subreport — `InlineDefinition`**: editar um sub-relatório **embutido** exige um editor de
   relatório aninhado (hoje o Designer referencia por `ReportId`; o inline round-trippa no `.repx`).
3. **TextBox — `TextRuns` (rich text)**: edição de texto rico com runs/estilos por trecho é um
   editor à parte (hoje o Designer edita a `Expression` única).
4. **GroupBand no band strip**: edição completa de grupos direto na faixa de bandas.

> Estado atual: **17 dos 19 itens 100% reais nas 3 superfícies**; os 2 restantes (drill-down
> interativo e Word/DOCX) estão na seção [Limites conhecidos](#limites-conhecidos-decisão-de-escopo)
> acima, com a razão arquitetural de cada um. Mantenha esta matriz sincronizada ao mexer em qualquer
> um deles. Veja o `CHANGELOG.md` para o histórico por versão.
