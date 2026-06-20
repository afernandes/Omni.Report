# Compatibilidade com SSRS/RDL — análise de gaps

**Status:** análise (jun/2026) · **Método:** 11 analisadores paralelos comparando cada área do RDL ao OmniReport, com a lente de paridade **code-first / low-level / Designer**.
**Relacionado:** [rdl-coverage.md](rdl-coverage.md) (cobertura render × round-trip).

## Panorama

Das features RDL avaliadas: **96 completas · 18 parciais · 32 ausentes** (~66% completo). O engine já cobre os 17 report items com render nativo (8 tipos de chart, matrix/pivô, KPIs), expressões com funções SSRS (condicional/texto/data/agregação), e os 3 modos de autoria existem para o que está implementado. Os gaps concentram-se em **interop de formato (.rdl)**, **parâmetros**, **funções de lookup**, **profundidade do Tablix**, e **interatividade em runtime**.

> **Princípio de paridade:** todo item abaixo deve, ao ser implementado, funcionar nos 3 modos — code-first (`ReportBuilder`), low-level (records imutáveis) e Designer — não só num.

## Tier 1 — núcleo de compatibilidade RDL (maior valor)

| # | Gap | Status | Impacto | Esforço | Paridade a garantir |
|---|---|---|---|---|---|
| 1 | **Import `.rdl` (SSRS XML → ReportDefinition)** | ausente | Desbloqueia migração SSRS→OmniReport. É a "compatibilidade RDL" literal (hoje só `.repx`/`.repjson`). | M | Importador produz o mesmo `ReportDefinition` que code-first/low-level criariam; abre no Designer |
| 2 | **Parameters: Available Values** (lista estática + query-driven) | ausente | Feature de UX mais crítica do prompt — dropdowns com domínio validado. Hoje todo parâmetro é texto livre. | M | Modelar no record + API code-first + editor no Designer |
| 3 | **`Lookup`/`LookupSet`**/`MultiLookup` | ✅ Lookup+LookupSet (#85) · MultiLookup pendente | Buscar valor em outro dataset (tax por código, nome por id) sem join prévio. ~30% dos casos avançados. | M | Função no avaliador (vale p/ os 3 modos automaticamente) |
| 4 | **Parameters: Cascading / dependentes** + default-como-expressão + validação | ausente | Parâmetros dependentes (Estado→Cidade) e defaults dinâmicos. Cluster que falta inteiro. | M | record + code-first + Designer |

## Tier 2 — relatórios ricos (alto valor)

| # | Gap | Status | Notas |
|---|---|---|---|
| 5 | **Tablix: SortExpression não renderiza** | parcial | `TablixGroup.SortExpression` existe no model e round-trippa (AdvancedElements.cs:62), mas `TablixRenderer` ignora; code-first/Designer não expõem. Um `.repx` SSRS com matrix ordenado renderiza fora de ordem. **Quick win** (model já existe). |
| 6 | **Tablix: subtotais / group footer rows** | ✅ linha (#86) | `RowSubtotals`: footer por grupo de linha externo + total geral, 3 modos. Follow-up: ColumnSubtotals, labels configuráveis. |
| 7 | **Tablix: células com merge/span** (colSpan/rowSpan) | parcial (#86) | ✅ span verdadeiro nos headers de coluna (largura = nº folhas). Falta span por-célula arbitrário (rowSpan / merge no corpo). |
| 8 | **Tablix: membros estático vs. dinâmico** | ausente | RDL StaticMember/DynamicMember — crosstabs assimétricos (coluna fixa "Total" + colunas dinâmicas). |
| 9 | **Drill-down / ToggleItem runtime** | parcial | Model + round-trip + autoria completos (`ToggleItemId`/`InitiallyHidden`); falta o chevron expandir/colapsar no viewer/HTML/PDF. Exige pipeline interativo (DOM) além do raster Skia. Esforço **grande**. |
| 10 | **Multi-coluna (newspaper)** | ausente | `Columns`/`ColumnSpacing` round-trippam mas o paginador ignora — renderiza coluna única. |
| 11 | **Repeat headers on new page** (tablix/group) | ausente | Cabeçalhos não repetem ao quebrar página. |

## Tier 3 — expressões, estilo, dados, export

| # | Gap | Status | Notas |
|---|---|---|---|
| 12 | **`Previous()`** (valor anterior na sequência/grupo) | ausente | Comum em variação período-a-período. |
| 13 | **`ReportItems.X`** (ler valor de outro elemento) | ausente | Referência cruzada entre textboxes. |
| 14 | **Globals / Variables** (Globals.RenderFormat; Report/Group Variables) | parcial | Alguns globais existem; faltam outros + variáveis de grupo/relatório. |
| 15 | **Named/reusable styles** (Style[@Name]) | ausente | Hoje todo Style é inline; perde manutenibilidade em relatórios grandes. |
| 16 | **Gradients** (linear/radial) + background image | ausente | Preenchimentos avançados. |
| 17 | **Shared data sources / datasets** | parcial | Hoje tudo embedded (escolha arquitetural — arquivo único); RDL separa. |
| 18 | **Export Word (.docx)** | ausente | Migração SSRS; limite canvas-vs-flow (esforço alto). |
| 19 | **Export imagem (PNG/TIFF público) + XML** | parcial | PNG interno existe (`SkiaRenderingContext.GetPagePng`) sem `IReportExporter` público; TIFF/XML ausentes. **Quick win** (PNG = wrapper). |

## Recomendação de sequência

1. **Quick wins primeiro** (model já existe, só falta wirar): **#5 Tablix SortExpression** e **#19 exporter PNG público** — baixo risco, fecham gaps reais rápido.
2. **Núcleo RDL**: **#2 Available Values** → **#3 Lookup** → **#1 Import .rdl**. Esta ordem porque Available Values e Lookup também beneficiam relatórios nativos (não só import), e o importador se apoia no modelo já enriquecido.
3. **Riqueza visual**: **#7 cell span** + **#6 subtotais** (Tablix de verdade) e **#9 drill-down runtime** (o maior salto de interatividade, mas exige o pipeline DOM).

Cada item entra com o ciclo padrão: implementar nos 3 modos de autoria → testes → revisão adversarial → PR.
