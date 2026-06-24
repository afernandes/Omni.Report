# Compatibilidade com SSRS/RDL — análise de gaps

**Status:** reconciliado (jun/2026) · **Método original:** 11 analisadores paralelos comparando cada área do RDL ao OmniReport, com a lente de paridade **code-first / low-level / Designer**.
**Reconciliação (jun/2026):** os 19 itens rastreados abaixo foram **re-verificados contra o código** (arquivo:linha + teste). Vários que a análise inicial marcava como "ausente/parcial" já estavam implementados — as datas entre a análise e hoje cobriram boa parte do roadmap.
**Relacionado:** [rdl-coverage.md](rdl-coverage.md) (cobertura render × round-trip).

## Panorama

> ⚠️ **O headline original "96 completas · 18 parciais · 32 ausentes (~66%)" está defasado.** Dos **19 gaps rastreados** nas tabelas abaixo, **~13 estão DONE, ~4 PARTIAL e ~3 MISSING** (1 deles **vedado por decisão de produto**, não por falta de implementação). A taxa de fechamento dos gaps rastreados é ~85–90%, não 66%. Um re-audit completo dos 146 itens daria um número global novo; aqui reconciliamos só os rastreados (verificados um a um).

O engine cobre os 17 report items com render nativo (8 tipos de chart, matrix/pivô com sort+subtotais, KPIs), expressões com vocabulário SSRS (condicional/texto/data/agregação + Lookup/LookupSet/MultiLookup + Previous/RunningValue/ReportItems), **import e export `.rdl`** (XML SSRS ↔ ReportDefinition, lossless via CustomProperties), e 9 exporters (PDF/XLSX/DOCX/HTML/SVG/CSV/Markdown/PNG/JSON). Os 3 modos de autoria existem para o que está implementado.

> **Princípio de paridade:** todo item ao ser implementado deve funcionar nos 3 modos — code-first (`ReportBuilder`), low-level (records imutáveis) e Designer.

> **Constraint de produto:** o **output é SEMPRE estático** (raster Skia / layout determinístico). Itens que exigem interatividade em runtime (drill-down clicável, toggle expandir/colapsar) ficam **fora de escopo por decisão de produto** — a visibilidade *estática* (`Visible`/`VisibleExpression`) é suportada; o *toggle interativo* não.

## Tier 1 — núcleo de compatibilidade RDL

| # | Gap | Status verificado | Evidência | Teste |
|---|---|---|---|---|
| 1 | **Import `.rdl`** (SSRS XML → ReportDefinition) | ✅ **DONE** | `RdlImporter.cs:424-479` importa Textbox/Line/Rectangle(aninhado)/Image/Tablix/Chart/Gauge/Subreport + CustomReportItem (DataBar/Sparkline/Indicator); Map é skip-com-warning. CustomProperties lossless. | Sim (`RdlImporterTests`, 62) |
| 2 | **Parameters: Available Values** (estático + query) | ✅ **DONE** | `ReportParameter.cs:29-54`, import `RdlImporter.cs:1361-1381`, resolver `ParameterValueResolver.cs`, dropdown no prompt | Sim |
| 3 | **`Lookup`/`LookupSet`/`MultiLookup`** | ✅ **DONE (as 3)** | `ExpressionEvaluator.cs:230-267` | Sim (`LookupTests`, 12) |
| 4 | **Parameters: Cascading/dependentes** | ⚠️ **PARTIAL** | `DefaultValueExpression` ✅ (`ReportParameter.cs:22`) + validação Required ✅ (prompt). **Falta** cascata real (Estado→Cidade): nenhum `DependsOn`/AvailableValues ligado a outro parâmetro. | DefaultValueExpr/validação: sim · cascata: não |

## Tier 2 — relatórios ricos (Tablix + paginação)

| # | Gap | Status verificado | Evidência | Teste |
|---|---|---|---|---|
| 5 | **Tablix SortExpression renderiza** | ✅ **DONE** | `TablixRenderer.cs:227-485` (`GroupNode.Sort`, asc/desc, type-aware) | Sim (`TablixMatrixTests:293-310`) |
| 6 | **Tablix subtotais / group footer** | ✅ **DONE** | `RowSubtotals` + `ColumnSubtotals` (`AdvancedElements.cs:55,63`); render `TablixRenderer.cs:244-400`; labels customizáveis | Sim (`TablixMatrixTests:98-273`) |
| 7 | **Tablix merge/span** (colSpan/rowSpan) | ⚠️ **PARTIAL** | colSpan em headers de coluna ✅ (`TablixRenderer.cs:101-108`); modelo tem `RowSpan` (`AdvancedElements.cs:89-90`) mas **rowSpan no corpo não renderiza** | headers: sim · rowSpan: não |
| 8 | **Tablix StaticMember/DynamicMember** | ❌ **MISSING** | crosstabs assimétricos (coluna fixa "Total" + dinâmicas). Grep zero. | Não |
| 9 | **Drill-down / ToggleItem runtime** | 🔒 **VEDADO** (model ✅) | model+round-trip+autoria ✅ (`ToggleItemId`/`InitiallyHidden`, `ReportElement.cs:56-65`); chevron interativo **fora de escopo** (constraint de output estático) | round-trip: sim |
| 10 | **Multi-coluna (newspaper)** | ✅ **DONE** (doc antigo errado) | `PageSetup.Columns/ColumnSpacing` + paginador snake `PageAccumulator.cs:27-79`, `ReportPaginator.cs:925` | Sim (`PaginationEdgeCaseTests:254-290`) |
| 11 | **Repeat headers on new page** | ✅ **DONE** | `_repeatHeaders` no `ReportPaginator.cs:30,782-950` (reimprime na quebra, outer→inner) | Sim (`LargeReportPaginationTests`) |

## Tier 3 — expressões, estilo, dados, export

| # | Gap | Status verificado | Evidência | Teste |
|---|---|---|---|---|
| 12 | **`Previous()`** | ✅ **DONE** | `ExpressionEvaluator.cs:302-312`, `ReportExpressionContext.cs:291-315` | Sim (`PositionalFunctionsTests`) |
| 13 | **`ReportItems.X`** | ✅ **DONE** | `ExpressionEvaluator.cs:104`, `ReportExpressionContext.cs:234-242` (Get/SetReportItem) | Integrado no render |
| 14 | **Globals / Variables** | ⚠️ **PARTIAL** | PageNumber/TotalPages/Now/Today/UserName/ReportName/Language ✅; ReportVariable (Row/Report/Group) ✅. **Falta** `Globals.RenderFormat` (awkward: o layout é format-agnóstico, paginado uma vez p/ todos os formatos) | parcial |
| 15 | **Named/reusable styles** (`Style[@Name]`) | ❌ **MISSING** | todo `Style` é inline (`Style.cs:12`, sem `Name`/registry) | Não |
| 16 | **Gradients** (linear/radial) | ❌ **MISSING** (background image ✅) | `BackgroundImage.cs` ✅; nenhum gradient fill no modelo de Style | bg image: sim |
| 17 | **Shared data sources / datasets** | ⚠️ **PARTIAL (por design)** | tudo embedded (`ReportDefinition.cs:25`) — escolha arquitetural (arquivo único); RDL separa | — |
| 18 | **Export Word `.docx`** | ✅ **DONE** | `DocxExporter.cs` (grid tabular + rasterização de charts/gauges via `RegionRasterizer`) | Sim (`DocxExporterTests`, 13) |
| 19 | **Export imagem público (PNG) + TIFF/XML** | ⚠️ **PARTIAL** | PNG público ✅ (`Reporting.Output.Image/PngImageExporter.cs`); **TIFF/XML ausentes** (Skia não encoda TIFF nativamente; XML não existe — há JSON) | PNG: sim |

## Trabalho genuinamente restante (verificado)

Em ordem de valor/tração para quem for continuar:

1. **#16 Gradients (linear/radial fill)** — modelo de fill no `Style` + shader Skia (`SKShader.CreateLinearGradient`, bem-suportado) + serializer (4 switches ou auto-wiring) + 3 modos + render Skia (verificar visualmente). Feature multi-PR, alto valor visual, caminho claro.
2. **#15 Named/reusable styles** — tabela de estilos + referência por nome + resolução no render; toca serialização. Médio-grande.
3. **#4 Cascading parameters** — `DependsOn` no modelo + AvailableValues ligado a outro parâmetro + resolver + UI no prompt. Médio.
4. **#7 rowSpan no corpo do Tablix** — estender o colSpan existente p/ rowSpan na grade do corpo (modelo já tem `RowSpan`). Geometria de grade fiddly, testável.
5. **#8 StaticMember/DynamicMember** — crosstabs assimétricos. Mudança de modelo maior.
6. **#19 TIFF/XML exporters** — TIFF precisa de lib externa (Skia não encoda); XML é formato novo. Baixa prioridade.
7. **#14 `Globals.RenderFormat`** — exige passar o formato-alvo no `PaginationRequest`, acoplando layout a formato (hoje paginado uma vez p/ todos os formatos). Decisão arquitetural antes de implementar.

**Fora de escopo (decisão de produto):** #9 drill-down/toggle interativo em runtime — o output é sempre estático.

Cada item entra com o ciclo padrão: implementar nos 3 modos de autoria → testes → revisão adversarial → PR.
