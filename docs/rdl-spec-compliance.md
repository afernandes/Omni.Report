# Conformidade com a Especificação RDL (Microsoft)

> Documento de arquitetura de conformidade. Cruza a **especificação oficial Microsoft RDL**
> (MS-RDL 2008/01, 2010/01, 2016/01) com o suporte atual do OmniReport, avaliado em
> **5 dimensões de autoria/execução**: `model` (modelo imutável), `serial` (round-trip `.repx`/`.repjson`),
> `import` (RdlImporter de `.rdl`), `render` (engine de desenho), `code-first` (API fluente) e
> `designer` (Blazor: toolbox + canvas + PropertyGrid).
>
> Fontes: [`rdl-coverage.md`](rdl-coverage.md), [`rdl-import.md`](rdl-import.md),
> [`rdl-compatibility-gaps.md`](rdl-compatibility-gaps.md), [`expressions.md`](expressions.md),
> [`data-sources.md`](data-sources.md) + inspeção de código (`Reporting.Core`,
> `Reporting.Serialization`, `Reporting.Layout`, `Reporting.Expressions`,
> `Reporting.CodeFirst`, `Reporting.Designer.Blazor`).

---

## 1. Resumo executivo

### Meta

Tornar o RDL um **cidadão de 1ª classe** do OmniReport: **100% de conformidade** com a especificação
Microsoft RDL, com **paridade integral entre os 3 modos de autoria** — **code-first** (API fluente),
**low-level** (modelo imutável + serialização `.repx`/`.repjson`) e **Designer** (Blazor visual).
Toda feature nasce nos 3 modos; nenhuma é "só import" ou "só render".

### Diagnóstico em uma frase

O OmniReport tem um **núcleo (model + serialização + render + code-first + Designer) forte e maduro**
— o gargalo de conformidade é **o `RdlImporter`**, que hoje importa apenas a casca estrutural
(`Body`/`PageHeader`/`PageFooter`, `ReportParameters`, e os 4 itens simples: TextBox/Line/Rectangle/Image).
**Tudo o que é data region (Tablix/Chart/Gauge/Map/etc.), DataSets, estilo, ação, visibilidade e
quebras é silenciosamente descartado na importação.** O round-trip interno `.repx`/`.repjson` é
~100% lossless; o "buraco" está quase todo na ponte **RDL XML → ReportDefinition**.

### Conformidade global estimada

| Indicador | Estimativa | Comentário |
|---|:--:|---|
| **Round-trip interno (`.repx`/`.repjson`)** | **~98%** | Praticamente lossless; auto-wiring por convenção |
| **Render** | **~90%** | Quase todos os elementos desenham de verdade (sem stubs); faltam ticks de gauge, multi-run real, toggle interativo |
| **Code-first** | **~88%** | API cobre quase tudo; Tablix builder limitado; Variables/Code parciais |
| **Designer** | **~82%** | Toolbox completo; faltam editores ricos (TextRuns, Tablix inline, Variables, metadados de parâmetro) |
| **Model** | **~80%** | Maioria modelada; faltam Hidden/Nullable/AllowBlank, TablixHeader/Body nativos, BackgroundImage/Gradient, CustomReportItem |
| **Import (RdlImporter)** | **~20%** | **Gargalo crítico**. Sem DataSets, sem data regions, sem estilo/ação/visibilidade |
| **CONFORMIDADE GLOBAL (ponderada por uso real)** | **~62%** | Núcleo de autoria nova ≈ 88%; migração de SSRS (`.rdl` → editar) ≈ 25% |

### Conformidade por área

| # | Área | Global | model | serial | import | render | code-first | designer |
|---|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| A | Report root / Page / Body / EmbeddedImages / Code / CustomProperties / DocumentMap | **~70%** | 🟡 | ✅ | 🔴 | ✅ | ✅ | ✅ |
| B | DataSources / DataSets (Query, Fields, CalculatedFields, Filters, Sort, Relations) | **~55%** | 🟡 | 🟡 | 🔴 | ✅ | ✅ | 🟡 |
| C | ReportParameters / ParameterLayout / Variables | **~70%** | 🟡 | ✅ | 🟡 | ✅ | 🟡 | 🟡 |
| D | Simple ReportItems: Textbox / Rectangle / Image / Line / Subreport | **~70%** | 🟡 | ✅ | 🟡 | 🟡 | ✅ | 🟡 |
| E | Tablix (hierarquias, members, body, corner, totais, headers repetidos) | **~50%** | 🟡 | 🟡 | 🔴 | ✅ | 🟡 | 🟡 |
| F | Data viz: Chart / Gauge / Map / DataBar / Sparkline / Indicator / CustomReportItem | **~75%** | 🟡 | ✅ | 🔴 | ✅ | ✅ | ✅ |
| G | Style / Visibility / Action / Bookmark / Sorting / PageBreaks | **~80%** | 🟡 | ✅ | 🟡 | 🟡 | ✅ | 🟡 |
| H | Expression Language (coleções, agregados, funções posicionais, lookup) | **~65%** | 🟡 | ✅ | 🟡 | 🟡 | ✅ | 🟡 |

Legenda: ✅ pleno · 🟡 parcial · 🔴 ausente/crítico.

---

## 2. Matriz de conformidade por área

> `Gap` resume a lacuna dominante. `Esforço`: S (≤1 dia) · M (~1 sprint) · L (1–2 sprints) · XL (arquitetural).
> `Prioridade`: 1 (alta) · 2 (média) · 3 (baixa/edge).

### Área A — Report root, Page, Body, EmbeddedImages, Code, CustomProperties, DocumentMap

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| Report.Name | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| PageWidth/Height/Margins | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — (via `PageSetup`) | S | 1 |
| Report.Page (estrutura 2008+) | 🟡 | 🟡 | ✅ | 🟡 | ✅ | ✅ | ✅ | Import lê margens/tamanho mas não valida namespace/versão | M | 2 |
| Body / ReportItems | 🟡 | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Body livre mapeado p/ banda `ReportHeader` (muda contexto de render) | L | 2 |
| ReportSections (2016+) | 🔴 | 🔴 | n/a | 🔴 | n/a | n/a | n/a | Sem multi-seção; `PageSetup` único por report | XL | 3 |
| EmbeddedImages | 🔴 | 🟡 | 🟡 | 🔴 | ✅ | 🔴 | 🔴 | Modelo (`ImageElement.InlineData`) existe; importador não popula bytes; falta UI de embed | M | 1 |
| Code (VB/C#) | 🟡 | ✅ | ✅ | 🔴 | 🟡 | ✅ | ✅ | Round-trip ok; **não executa** sem Roslyn; importador não lê `<Code>`; não está no switch do renderer | L | 1 |
| CustomProperties | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ | ✅ | Modelado como `Metadata`; importa CustomProperties + Description/Author/AutoRefresh/Language (#120) | S | 2 |
| DocumentMap / Label | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | Emite dados de outline; viewer renderiza TOC | S | 1 |
| ReportParameters (coleção) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| RDL Version / Namespace | 🟡 | 🟡 | n/a | 🟡 | n/a | n/a | n/a | Usa `LocalName` (robusto), mas sem detecção/aviso de versão | M | 3 |

### Área B — DataSources / DataSets

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| DataSource.Name | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Fields (schema) | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import não extrai `<Fields>` (inferido em runtime) | M | 1 |
| CalculatedFields | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Modelo/render completos; import ignora `<CalculatedFields>` | M | 1 |
| FilterExpression | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora `<Filters>` | M | 1 |
| SortExpressions | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora `<SortExpressions>` | M | 1 |
| Query.CommandText | 🟡 | 🟡 | 🟡 | 🔴 | n/a | ✅ | ✅ | Texto em `Parameters[_sql]`; sem elemento `Query` dedicado; import ignora | L | 1 |
| Query.CommandType | 🟡 | 🟡 | 🟡 | 🔴 | n/a | ✅ | ✅ | Só Text/StoredProc; sem TableDirect | M | 2 |
| QueryParameters | 🟡 | 🟡 | 🟡 | 🔴 | n/a | ✅ | ✅ | Convenção `param:@x`; import não extrai | M | 2 |
| DataMember | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import não popula (raro em RDL) | S | 2 |
| Relations (master-detail) | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | 🟡 | `DataRelation` ok; UI de relação incompleta; import ignora `<Relationships>` | L | 2 |
| **RdlImporter: DataSet** | 🔴 | 🔴 | 🔴 | 🔴 | n/a | 🔴 | 🔴 | **Importador não lê `<DataSources>`/`<DataSets>` — fundação da migração** | XL | 1 |
| Shared DataSource refs (.rds) | 🔴 | 🔴 | 🔴 | 🔴 | n/a | 🔴 | 🔴 | Sem conceito de fonte compartilhada externa | XL | 2 |
| Fields context em expressão | ✅ | ✅ | n/a | n/a | ✅ | ✅ | ✅ | `{Fields.Name}` e `{Fields.Source.Field}` ok | S | 1 |
| DataSourceRegistry (runtime) | ✅ | ✅ | n/a | n/a | ✅ | ✅ | 🟡 | Host provê `IReportDataSource`; ok | S | 1 |

### Área C — ReportParameters, ParameterLayout, Variables

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| DataType | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| DefaultValue | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| ValidValues (estático) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 | Designer sem reorder/editor rico | M | 2 |
| ValidValues (query) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| MultiValue | ✅ | ✅ | ✅ | ✅ | n/a | ✅ | ✅ | `AllowMultiple` | S | 1 |
| Hidden | 🔴 | 🔴 | 🔴 | 🔴 | n/a | 🔴 | 🔴 | Sem propriedade `Hidden` em lugar nenhum | M | 1 |
| Nullable | 🟡 | 🔴 | 🔴 | 🔴 | ✅ | 🔴 | 🔴 | Mapeado como `Required = !Nullable`; sem campo explícito | M | 1 |
| AllowBlank | 🔴 | 🔴 | 🔴 | 🔴 | n/a | 🔴 | 🔴 | Sem suporte | M | 2 |
| Prompt | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| UsedInQuery | 🔴 | 🔴 | 🔴 | 🔴 | n/a | 🔴 | 🔴 | Metadado; baixa prioridade | S | 3 |
| Name (attr) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| ReportParameterLayout | 🔴 | 🔴 | 🔴 | 🔴 | n/a | 🔴 | 🔴 | Prompt auto-gerado; sem ordem/grupos | L | 2 |
| ReportVariable | 🟡 | 🟡 | ✅ | 🔴 | 🟡 | ✅ | 🔴 | Modelo+serial+code-first ok; import não lê `<Variables>`; sem UI | M | 1 |
| VariableScope (Row/Report/Group) | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | 🔴 | Import e Designer faltam | M | 1 |
| Variable.Writable | 🔴 | 🔴 | 🔴 | 🔴 | n/a | 🔴 | 🔴 | Variáveis só leitura | L | 3 |
| Parâmetros em cascata | 🟡 | 🟡 | 🟡 | 🟡 | ✅ | ✅ | 🔴 | Sem metadado de dependência; sem UI de cascata | L | 2 |

### Área D — Simple ReportItems

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| Textbox.Expression / Value | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Textbox.Paragraphs/TextRuns | 🟡 | ✅ | ✅ | 🟡 | 🟡 | 🟡 | 🔴 | Import lê só 1º run; render concatena sem estilo por-run; sem editor rich-text | M | 1 |
| Textbox.CanGrow/CanShrink | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import não lê os 2 booleanos | S | 1 |
| Textbox.TextDecoration | 🔴 | 🔴 | n/a | 🔴 | 🔴 | 🔴 | 🔴 | Underline/Strikeout via FontStyle; falta Overline + decoração própria | M | 2 |
| Textbox.WritingMode/Direction | 🔴 | 🔴 | n/a | 🔴 | 🔴 | 🔴 | 🔴 | Sem orientação vertical/RTL | L | 3 |
| Textbox.DataType | 🔴 | 🔴 | n/a | 🔴 | 🔴 | 🔴 | 🔴 | Sem DataType para formatação contextual | M | 2 |
| Rectangle.CornerRadius | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora | S | 2 |
| Rectangle.Fill/Border | 🟡 | 🟡 | ✅ | 🔴 | ✅ | ✅ | ✅ | Import não lê estilo do RDL | M | 2 |
| Rectangle.NestedReportItems | 🟡 | 🔴 | n/a | 🟡 | 🔴 | 🔴 | 🔴 | Retângulo é folha; filhos achatados na banda (perde hierarquia) | L | 3 |
| Image.Source (Ext/Embed/DB) | 🟡 | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Só External; Embedded/Database viram path-placeholder | M | 2 |
| Image.MimeType/Sizing | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Sizing honrado no render em todos os backends (#114, ImageSizingMath); import RDL <Sizing>→model é PR2; MimeType não modelado | S | 2 |
| Line.Direction | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import não infere direção dos bounds | S | 1 |
| Line.Pen (style/width/color) | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora estilo da linha | S | 2 |
| Subreport.ReportId/Inline | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | 🟡 | Import não lê Subreport; editor de inline-def é gap | M | 1 |
| Subreport.ParameterBindings | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Cai junto com import de Subreport | S | 1 |
| Subreport.DataSetName | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Idem | S | 1 |
| (global) Visibility/Hidden | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import não lê `<Hidden>`/`<ToggleItem>` | S | 1 |
| (global) Bookmark/DocMapLabel/Action | 🟡 | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import não extrai | S | 2 |
| (global) Style (Font/Color/Border/…) | 🟡 | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import não lê nós `<Style>` dos itens | M | 2 |
| Sizing: Width/Height/Left/Top | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | Todas as unidades RDL | S | 1 |

### Área E — Tablix

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| Tablix (root) | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Modelo completo (exceto import) | S | 1 |
| ColumnHierarchy/RowHierarchy | 🟡 | 🟡 | 🟡 | 🔴 | ✅ | 🟡 | 🟡 | Hierarquia achatada em arrays; sem `TablixMember.TablixMembers` aninhado | L | 1 |
| TablixMember | 🟡 | 🟡 | 🟡 | 🔴 | ✅ | 🟡 | 🟡 | `TablixGroup` sem Visibility/FixedData/HideIfNoRows/Keep* | M | 2 |
| Group (Filters/GroupExpressions) | 🟡 | 🟡 | 🔴 | 🔴 | ✅ | 🟡 | 🔴 | Sem Group.Filters nem agregados nomeados por grupo | M | 2 |
| TablixHeader | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Não modelado; cabeçalho/rodapé de grupo via índice de célula | L | 1 |
| TablixBody (Rows/Columns) | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Estrutura achatada em `Cells[]`; não reconstrói body/rows | L | 1 |
| TablixColumn (Size) | 🟡 | 🟡 | 🟡 | 🔴 | ✅ | 🟡 | 🔴 | Larguras como pesos, não `Size` RDL | S | 2 |
| TablixRow (Height) | 🔴 | 🔴 | 🔴 | 🔴 | ✅ | 🔴 | 🔴 | Altura inferida; sem metadado por linha | M | 2 |
| TablixCell / CellContents | 🟡 | 🟡 | 🟡 | 🔴 | 🟡 | 🟡 | 🟡 | Sem ColSpan/RowSpan; conteúdo só primitivo em matrix | M | 2 |
| TablixCorner | 🔴 | 🔴 | 🔴 | 🔴 | 🟡 | ✅ | 🟡 | Canto em `Cells[(0,0)]`; sem geometria/spanning real | M | 2 |
| Repeat Column/Row Headers | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Paginador não repete cabeçalho entre páginas | M | 2 |
| Fixed Column/Row Headers | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | N/A em saída estática/PDF | M | 3 |
| GroupsBeforeRowHeaders | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Layout outline fixo | L | 3 |
| LayoutDirection (LTR/RTL) | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Sempre LTR | S | 3 |
| TablixMember.SortExpressions | 🟡 | 🟡 | 🟡 | 🔴 | ✅ | 🟡 | 🟡 | Só 1 chave de sort por grupo | S | 2 |
| Tablix.SortExpressions (top) | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Sort só por grupo | S | 2 |
| Subtotais/Total geral | 🟡 | 🟡 | 🟡 | 🔴 | 🟡 | 🟡 | 🟡 | Só subtotal de linha; rótulo pt-BR hardcoded; sem subtotal de coluna | M | 2 |
| Member: HideIfNoRows/Repeat/Keep* | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Não modelado | M | 2 |
| Member.Visibility (toggle) | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Drill-down por grupo ausente | M | 2 |
| DataSetName | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Tablix.Filters | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Filtro pré-grupo ausente | M | 2 |
| Tablix.Style/OmitBorderOnPageBreak | 🟡 | ✅ | ✅ | ✅ | 🟡 | 🔴 | 🔴 | Bordas via grid hardcoded; sem OmitBorderOnPageBreak | S | 2 |
| PageBreak/PageName/NoRowsMessage | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ | ✅ | NoRowsMessage ✅ Tablix (model/render/4-serial/import/code-first/Designer, #110); PageBreak/PageName ainda; PageName não gera bookmark | S | 2 |
| ColSpan/RowSpan | 🟡 | 🟡 | 🟡 | 🟡 | ✅ | ✅ | 🟡 | TablixCell.ColumnSpan/RowSpan no model + 4-serial; render flat-table honra ColSpan (#121); import RDL <ColSpan> (#122, banda + TablixElement); RowSpan render + matrix + RowSpan implícito do RDL são follow-up | M | 2 |
| **RdlImporter: Tablix** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 | flat-table (#106) + matrix (#96) + NoRowsMessage (#110) importam; híbrido/span/multi-detail são follow-up (com aviso) | L | 1 |

### Área F — Data viz

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| Chart (8 tipos) | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Render/serial/code-first/designer ok; import ignora; sem 3D/drill | S | 1 |
| Chart.ChartData (binding) | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Sem `DataSetName` explícito; multi-source futuro | S | 1 |
| Gauge | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora; sem ticks/thresholds semânticos | M | 2 |
| Gauge.Scale (Min/Max/Interval) | 🟡 | 🟡 | 🟡 | 🔴 | 🟡 | 🟡 | 🟡 | Sem Interval/ticks/labels de escala | M | 3 |
| Gauge.Ranges | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora; resto completo | S | 1 |
| Map | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Vetorial ok; tiles scaffolded; import ignora | M | 2 |
| Map.GeoJSON / Graticule | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Completos (exceto import) | S | 1 |
| DataBar | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Só horizontal; sem Direction/gradiente/negativos | M | 2 |
| Sparkline | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Ranges só no code-first; sem Win-Loss/marker | M | 2 |
| Indicator | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | `IconName` em vez de IndicatorImage; arrow real ausente | M | 2 |
| CustomReportItem | 🔴 | 🔴 | n/a | 🔴 | n/a | n/a | n/a | Ponto de extensibilidade RDL inteiro ausente (ObjectType + AltReportItem) | XL | 3 |
| **RdlImporter: data viz** | 🔴 | n/a | n/a | 🔴 | n/a | n/a | n/a | **Import pula Chart/Gauge/Map/DataBar/Sparkline/Indicator/Tablix** | L | 1 |

### Área G — Style, Visibility, Action, Bookmark, Sorting, PageBreaks

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| Font / ForeColor / BackColor | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Border (4 lados) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Padding | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Format | 🟡 | 🟡 | ✅ | n/a | 🟡 | ✅ | 🟡 | Suporte a custom format SSRS limitado; import não lê Style/Format | M | 2 |
| Alignment H/V | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| WordWrap | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| BackgroundImage | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausência total | L | 2 |
| BackgroundGradient | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausência total | L | 3 |
| TextDecoration (Overline) | 🟡 | 🟡 | ✅ | n/a | 🟡 | ✅ | 🟡 | Underline/Strikeout ok; falta Overline | S | 3 |
| Visibility.Hidden (expr) | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import não lê (fallback visível, seguro) | S | 1 |
| Visibility.ToggleItem (drill) | 🟡 | ✅ | ✅ | 🔴 | 🔴 | ✅ | ✅ | Modelo ok; render não tem UI interativa (limite arquitetural Skia) | L | 2 |
| Action.Hyperlink | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import não lê (fallback) | S | 1 |
| Action.BookmarkLink | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Idem | S | 1 |
| Action.Drillthrough | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Viewer emite evento; import ignora | S | 1 |
| Bookmark | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import ignora | S | 1 |
| Label / DocumentMapLabel | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import ignora Label RDL | S | 1 |
| SortExpressions (multi-key) | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import não lê DataSet/SortBy | S | 1 |
| FilterExpression | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | Import não lê Filters | S | 1 |
| PageBreak (5 valores) | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora | S | 1 |
| KeepTogether | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora | S | 2 |
| KeepWithNext/Previous | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Orphan/widow ausente | M | 3 |
| RepeatHeaderOnNewPage | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import ignora | S | 2 |
| ConditionalFormat | ✅ | ✅ | ✅ | 🔴 | ✅ | ✅ | ✅ | Import não processa | S | 1 |
| PropertyExpressions | ✅ | ✅ | ✅ | n/a | ✅ | ✅ | ✅ | Extensão OmniReport (binding por propriedade) | S | 2 |
| TextRun.Action | 🟡 | ✅ | ✅ | 🔴 | 🟡 | ✅ | 🔴 | Render não distingue trecho clicável; sem editor | M | 3 |

### Área H — Expression Language

| Elemento | Status | model | serial | import | render | code-first | designer | Gap | Esf. | Pri |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|---|:--:|:--:|
| Fields! | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Parameters! | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| Globals! | 🟡 | 🟡 | 🟡 | 🟡 | 🟡 | ✅ | 🟡 | Só PageNumber/TotalPages/ExecutionTime/ReportName; falta RenderFormat/OverallTotalPages | M | 2 |
| User! | 🟡 | 🟡 | 🟡 | 🟡 | 🟡 | ✅ | 🟡 | UserID→UserName; Language ausente | M | 2 |
| ReportItems! | 🔴 | 🔴 | n/a | 🔴 | 🔴 | 🔴 | 🔴 | Sem acesso a valor de textbox renderizado (total em rodapé) | L | 2 |
| DataSets! | 🔴 | 🔴 | n/a | 🔴 | 🔴 | 🔴 | 🔴 | Sem metadados de dataset em expressão | L | 3 |
| Sum/Avg/Count/Min/Max | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| RunningValue | 🟡 | 🟡 | 🟡 | 🟡 | 🟡 | ✅ | 🟡 | Tratado como agregado de grupo; sem semântica cumulativa real | M | 2 |
| RowNumber | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausente; requer tracking de posição | M | 1 |
| Previous | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausente; requer buffer de histórico | M | 1 |
| First/Last | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausente | M | 2 |
| Lookup | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 1 |
| LookupSet | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | S | 2 |
| Multilookup | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausente (array param) | M | 3 |
| Aggregate (provider) | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Sem extension point de provider | L | 3 |
| CountRows | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausente | S | 2 |
| CountDistinct | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausente | S | 2 |
| StDev/StDevP/Var/VarP | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Ausente | M | 3 |
| InScope | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Requer scope tracking | M | 3 |
| Level (hierarquia recursiva) | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | 🔴 | Sem grupo recursivo | L | 3 |
| Operadores VB (& concat, Like) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | `&`→`Concat` e `Like` infixo→`Like()` mapeados no import (precedência `&`>`Like`); classes `[...]` do Like são follow-up | M | 2 |
| Scope param (Report/Group/Page/Running) | 🟡 | 🟡 | 🟡 | 🟡 | 🟡 | ✅ | 🟡 | Recursive ausente; scope desconhecido cai p/ Report (silencioso) | M | 2 |
| RdlExpression (conversor VB→dotted) | 🟡 | 🟡 | ✅ | ✅ | n/a | n/a | n/a | Converte coleções; não converte operadores VB nem membros ≠ `.Value` | M | 2 |

---

## 3. Programa sequenciado de PRs até 100%

> Ordenação por **(valor × dependência)**. Regra inviolável: **toda feature nasce nos 3 modos
> de autoria** (code-first, low-level/serialização, Designer) — ver Princípios (§4). Para gaps que
> são "puro import", a feature já existe nos outros modos; o PR só fecha a dimensão `import`
> (e, quando faltar, completa serial + Designer).

### Fase 0 — Quick wins de import barato (desbloqueia fidelidade sem arquitetura)

**PR-1 — Import de propriedades dos Simple ReportItems já modelados** ⬅️ **PRÓXIMO PR RECOMENDADO**
Dimensões: `import` (model/serial/render/code-first/designer já completos).
Escopo: no `RdlImporter`, popular o que o modelo **já aceita** mas o import descarta:
`CanGrow`/`CanShrink` (TextBox), `CornerRadius` (Rectangle), `Line.Direction` (inferida dos bounds) +
`Line.Pen`, `Visibility/Hidden`, `Bookmark`/`Action`/`DocumentMapLabel`, e nós `<Style>`
(Font/Color/Border/Padding/Alignment) → `Style` record.
Por que primeiro: **maior ganho de fidelidade por linha de código**, zero risco arquitetural,
nenhuma mudança de modelo, e estabelece o padrão de "extrair sub-elementos no importer" que os
PRs de data region vão reusar. Tudo já renderiza e serializa.
Nota 3-modos: nada novo a criar — apenas alinhar `import` ao que code-first/Designer já fazem.

**PR-2 — Report-level: EmbeddedImages + CustomProperties + Code (import)**
Dimensões: `import` (+ `code-first`/`designer` de embed de imagem).
Escopo: (a) parsear `<EmbeddedImages>` → mapa por Name → `ImageElement.InlineData`;
(b) `<CustomProperties>` → `ReportDefinition.Metadata`; (c) `<Code>` (Source+Language) → `CodeElement`.
Adicionar UI de upload/embed de imagem no Designer + helper code-first.
Dependência: independente. Valor alto p/ migração (logos embarcados são quase universais).

### Fase 1 — Metadados de parâmetro e variáveis (fundação semântica)

**PR-3 — Parameter metadata: Hidden, Nullable explícito, AllowBlank**
Dimensões: `model` → `serial` → `import` → `code-first` → `designer` (nesta ordem).
Escopo: adicionar `Hidden`, `Nullable` (explícito, deprecando a inversão de `Required`) e `AllowBlank`
ao record `ReportParameter`; auto-wire na serialização; ler do RDL; toggles no Designer; validar no
prompt do viewer. **Exemplo canônico de feature 3-modos nascendo junta.**

**PR-4 — Variables: import + Designer (model/serial/code-first já existem)**
Dimensões: `import` + `designer` (+ completar `render` de escopo).
Escopo: parsear `<Variables>` (Report e Group) → `ReportVariable` com `VariableScope`; editor de
variáveis no Designer; garantir avaliação de escopo Row/Report/Group no paginador.

### Fase 2 — Expression Language: funções posicionais (alto uso SSRS)

**PR-5 — RowNumber + Previous + First/Last (com isolamento de escopo)**
Dimensões: 5 dimensões (model de função no evaluator → render → code-first → designer validator).
Escopo: tracking de posição/buffer de histórico no `ReportExpressionContext`; implementar as 4
funções com reset por scope. Cobre os padrões SSRS mais comuns (numeração de linha, running totals
manuais, YoY). Inclui `CountRows`/`CountDistinct` (S) de carona.

**PR-6 — Operadores VB no conversor + Globals/User completos**
Dimensões: `import` (RdlExpression) + `render` (mapear `&`→concat, `Like`).
Escopo: mapear `&` e `Like` no `RdlExpression`; expor `Globals!RenderFormat`/`OverallTotalPages`,
`User!Language`. Aumenta fidelidade de round-trip de expressões SSRS.

### Fase 3 — DataSets: a fundação da migração (XL, dependência alta)

**PR-7 — RdlImporter: DataSources + DataSets (Query/Fields/CalculatedFields/Filters/Sort/Relations)**
Dimensões: `import` (model/serial/render/code-first/designer já existem para quase tudo).
Escopo: parsear `<DataSources>`/`<DataSets>` → `DataSourceDefinition` (CommandText em `Query`
dedicado — pequena evolução de model + serial), `Fields`, `CalculatedFields`, `Filters`,
`SortExpressions`, `QueryParameters`, `Relationships`. **Desbloqueia migração real de SSRS.**
Dependência: precede o import de Tablix/Chart (que referenciam `DataSetName`).
Nota: a execução de query continua delegada ao host (registro de `IReportDataSource`).

**PR-8 — Query model dedicado (CommandText/CommandType/QueryParameters de 1ª classe)**
Dimensões: `model` → `serial` → `code-first` → `designer` (refina o que hoje vive em `Parameters[]`).
Escopo: promover SQL/StoredProc/QueryParameters de convenção-de-dicionário para um sub-record
`Query` explícito, mantendo retrocompat de leitura. Encerra a parcialidade de B.

### Fase 4 — Tablix: o maior bloco de conformidade

**PR-9 — RdlImporter: Tablix (flatten mapeado + warnings)**
Dimensões: `import`.
Escopo: parsear `TablixColumnHierarchy`/`TablixRowHierarchy`/`TablixBody`/`TablixCorner` e mapear
para o modelo plano atual (`RowGroups`/`ColumnGroups`/`Cells`), emitindo **warnings** para recursos
sem destino (ColSpan, nesting irregular). Mesmo lossy no shape, recupera o conteúdo — fim da perda
silenciosa. Depende de PR-7.

**PR-10 — Tablix model enrichment: TablixHeader + TablixBody/Row nativos + member props**
Dimensões: `model` → `serial` → `render` → `code-first` → `designer`.
Escopo: refatorar `Cells[]` para estrutura mais rica (headers map + rows com Height); adicionar
`TablixHeader` (rodapé/cabeçalho de grupo, estilo, visibilidade); `TablixMember` ganha
`Visibility`/`HideIfNoRows`/`RepeatOnNewPage`/`KeepWithGroup`/`KeepTogether`; multi-key sort.
**Maior PR estrutural** — habilita conformidade fina e melhora o import do PR-9.

**PR-11 — Tablix pagination + cells avançadas**
Dimensões: `render` + `model`/`serial`/`designer`.
Escopo: `RepeatColumnHeaders`/`RepeatRowHeaders` (repetir cabeçalho entre páginas), `ColSpan`/`RowSpan`,
`Tablix.Filters`/`Group.Filters`, subtotais de coluna + rótulos configuráveis (remover pt-BR hardcoded),
`OmitBorderOnPageBreak`, `NoRowsMessage`. Builder code-first de Tablix mais completo.

### Fase 5 — Import de data viz e Subreport (fecha a casca visual)

**PR-12 — RdlImporter: Chart/Gauge/Map/DataBar/Sparkline/Indicator + Subreport**
Dimensões: `import` (model/serial/render/code-first/designer já completos).
Escopo: mapear cada data region RDL → element record correspondente; idem Subreport
(ReportId/Inline + ParameterBindings + DataSetName). Depende de PR-7 (DataSetName) e PR-9 (Tablix
como container de cells). Encerra a parcialidade de F.

### Fase 6 — Riqueza de estilo e interação

**PR-13 — Style.BackgroundImage + TextDecoration completo + Image Embedded/Sizing/MimeType**
Dimensões: `model` → `serial` → `render` → `import` → `code-first` → `designer`.
Escopo: `BackgroundImage` (ImageUrl/Repeat/Position/Transparency); `TextDecoration` com Overline;
`Image.Source=Embedded/Database` real + `MimeType` + mapeamento de `Sizing`.

**PR-14 — Multi-run TextRuns: render por-run + editor rich-text + import completo**
Dimensões: `render` + `designer` + `import`.
Escopo: render com estilo/ação por run (mixed-font path), import de todos Paragraphs/TextRuns
(não só o 1º), editor rich-text no Designer, builder code-first de runs, `TextRun.Action` granular.

**PR-15 — ReportItems! collection + RunningValue cumulativo real**
Dimensões: `model`/`render` (evaluator + pós-processamento de saída).
Escopo: rastrear valores de textbox renderizados por escopo de página para `ReportItems!Name.Value`
(padrão "total da página no rodapé"); corrigir semântica cumulativa de `RunningValue`.

### Fase 7 — Itens arquiteturais e edge cases (XL / baixa prioridade)

**PR-16 — ReportSections (multi-seção, page setup por seção)** — XL. `model`/`serial`/`import`/`render`.
**PR-17 — Rectangle como container (composite) + nested ReportItems verdadeiros** — L.
**PR-18 — ToggleItem interativo / drill-down** — L. Requer pipeline DOM-render (limite arquitetural Skia atual).
**PR-19 — ReportParameterLayout + parâmetros em cascata (UI)** — L.
**PR-20 — Shared DataSource references (.rds)** — XL.
**PR-21 — CustomReportItem (extensibilidade + AltReportItem)** — XL.
**PR-22 — Estatísticos (StDev/Var…) + InScope/Level/Multilookup/Aggregate provider** — M–L. Level/InScope dependem de grupo recursivo.
**PR-23 — WritingMode/Direction (vertical/RTL) + BackgroundGradient + KeepWithNext/Previous + RDL version detection** — M–L.

### Resumo de sequência

```
PR-1 ◀ próximo  →  PR-2  →  PR-3 ─ PR-4  →  PR-5 ─ PR-6
                                                  │
                          PR-7 (DataSets, fundação) ◀─┘
                                │
                  ┌─────────────┼──────────────┐
                PR-8          PR-9           PR-12 (depende de 7,9)
                            (Tablix import)
                                │
                          PR-10 → PR-11
                                          → Fase 6 (PR-13..15) → Fase 7 (PR-16..23)
```

Tamanho honesto: chegar a **~90% de conformidade efetiva** (incluindo migração de SSRS utilizável)
exige **PR-1 a PR-12** — estimados ~10–14 sprints. Os ~10% finais (Fase 7) são predominantemente
**XL arquiteturais** (ReportSections, container Rectangle, toggle interativo, .rds, CustomReportItem)
e edge cases estatísticos: muito esforço, baixa frequência de uso — fechar por último.

---

## 4. Princípios de paridade e não-regressão

1. **Model imutável primeiro.** Toda feature começa por um record imutável em `Reporting.Core`.
   O modelo é o contrato; serialização, render, import, code-first e Designer **consomem o mesmo
   model**. Nunca se cria uma feature "só no Designer" ou "só no importer" — isso gera divergência
   silenciosa entre os modos.

2. **Serialização auto-wired sempre que possível.** Propriedades escalares e recursivas do modelo
   devem round-trippar pelo caminho genérico por convenção (`ElementSerializationRegistry`),
   minimizando os 4 switches manuais de serializer (repx/repjson × writer/reader). Hand-wiring só
   quando a convenção não cobre (ver memória *serialization-auto-wiring*). Adicionar uma propriedade
   ao record deve, idealmente, "só funcionar" no `.repx`/`.repjson`.

3. **Importador e Designer consomem o mesmo model — nunca um atalho paralelo.** O `RdlImporter`
   produz `ReportDefinition`; o Designer edita `ReportDefinition`; o code-first constrói
   `ReportDefinition`. Um `.rdl` importado tem de ser **indistinguível** (no modelo) de um report
   criado no Designer ou em código. Import lossy é aceitável **no shape** (ex.: Tablix achatado),
   mas deve emitir **warnings explícitos**, nunca perda silenciosa.

4. **Os 3 modos nascem juntos.** Definition of Done de qualquer feature de conformidade:
   (a) record no model; (b) round-trip `.repx`/`.repjson` testado; (c) render real (ou round-trip
   documentado se não houver render); (d) método fluente code-first; (e) edição no Designer
   (toolbox/PropertyGrid). Um PR que entrega só 1–2 dessas dimensões deixa a feature em estado
   "parcial" — exatamente a dívida que esta matriz cataloga.

5. **Não quebrar code-first/low-level ao evoluir o model.** Mudanças estruturais (ex.: promover SQL
   de `Parameters[]` para sub-record `Query`; refatorar `Cells[]` de Tablix) mantêm **leitura
   retrocompatível** do formato antigo e adicionam o novo caminho. O round-trip interno é o invariante
   mais forte do produto (~98%) — protegê-lo com testes de regressão por PR.

6. **Render honesto.** Sem placeholders/stubs: ou o elemento desenha de verdade, ou é declarado
   round-trip-only com justificativa arquitetural (ver `rdl-coverage.md`). Features que dependem de
   interatividade (ToggleItem, FixedHeaders) são marcadas N/A para saída estática/PDF até existir
   pipeline DOM-render.

7. **Format/estilo consistente entre renderers.** Qualquer propriedade de estilo nova
   (TextDecoration, BackgroundImage) é honrada por **todos** os renderers (banda/Tablix/KPI/chart)
   via os resolvers compartilhados (`StyleResolver`/`ValueFormatter`), não caso a caso.
