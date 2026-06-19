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
| Chart | 🟡 | ✅ | Bar (agrupado), Line, Pie, **Area, Scatter, Radar** renderizam. Bubble/Stock (exigem dimensão extra na série) **pendentes** |
| Gauge | ✅ | ✅ | radial (anéis/ponteiro) + linear bullet |
| DataBar | ✅ | ✅ | barra proporcional |
| Sparkline | ✅ | ✅ | line / column / area |
| Indicator | ✅ | ✅ | seta direcional / forma / rating por faixa |
| Tablix | 🟡 | ✅ | tabela plana **e matrix/pivô** (1 grupo linha × 1 grupo coluna; célula = soma da interseção) renderizam; code-first `.Tablix(t => t.RowGroup().ColumnGroup().Cell())`. Grupos **aninhados** (multi-nível) e editor de matrix no Designer pendentes |
| Map | 🟡 | ✅ | **vetorial** renderiza (Web Mercator + graticule + shapes GeoJSON + marcadores). **Basemap por tiles online (OSM/Bing) pendente** |
| Subreport | ✅ | ✅ | renderiza o filho (inline ou via resolver de id) na largura do subreport, com bindings de parâmetro avaliados no contexto pai; code-first `.Subreport()`/`.SubreportInline()` + editor no Designer |
| Code (bloco C#) | — | ✅ | não é "render": **avalia** via pacote opt-in `Reporting.Expressions.Roslyn` (`Code.Metodo(...)`). ⚠ executa C#: use só com fontes confiáveis |

## Recursos de interação

| Recurso | Render | Round-trip | Observações |
|---|:--:|:--:|---|
| `Action` (link/bookmark/drill-through) | 🟡 | ✅ | metadados round-trippam; emissão por renderer varia |
| Drill-down / toggle (`ToggleItemId` + `InitiallyHidden`) | ❌ | ✅ | modelo round-trippa; **toggle interativo (chevron) pendente** no Viewer/HTML |
| `DocumentMapLabel` / `Bookmark` | 🟡 | ✅ | persistidos; navegação depende do consumidor |

## Saídas (exporters)

| Formato | Status | Observações |
|---|:--:|---|
| PDF (`Output.Pdf`) | ✅ | vetorial via SkiaSharp, texto selecionável, metadados |
| Excel (`Output.Excel`) | ✅ | ClosedXML, classificação de linhas, fórmulas de subtotal |
| SVG / HTML | ✅ | SVG embutido + CSS de impressão |
| CSV / JSON / Markdown | ✅ | RFC 4180 / schema estável de primitivos / GFM |
| ESC/POS (térmica) | ✅ | raster 203 dpi, corte automático |
| Word/DOCX | ❌ | **pendente** (maior pedido corporativo faltante) |

## As três superfícies de autoria

Todo elemento que renderiza deve ser **autorável das três formas** — é um invariante do projeto:

1. **Code-first** — API fluente `ReportBuilder.Create(...)` (`.Chart()`, `.Tablix()`, `.Map()`, …).
2. **Low-level** — canvas sem bandas (primitivos + elementos posicionados manualmente).
3. **Designer** — toolbox + canvas + PropertyGrid no `Reporting.Designer.Blazor`.

> Mantenha esta matriz sincronizada ao concluir cada item pendente (Tablix matrix, Subreports,
> tiles de Map, toggle de visibilidade, tipos de gráfico, DOCX). Veja o `CHANGELOG.md` para o
> histórico por versão.
