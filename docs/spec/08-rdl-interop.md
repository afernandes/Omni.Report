# 8. Interop com RDL (projeção)

Esta seção especifica a estratégia de interoperabilidade entre o modelo nativo OmniReport e o formato
**RDL** (*Report Definition Language*, o XML que o SSRS / Report Builder consomem). Toda a lógica vive em
`src/Reporting.Serialization`:

| Tipo | Arquivo | Papel |
|---|---|---|
| `RdlImporter` | `RdlImporter.cs` | `.rdl` → `ReportDefinition` (leitura) |
| `RdlExporter` | `RdlExporter.cs` | `ReportDefinition` → `.rdl` (escrita), implementa `IReportSerializer` |
| `RdlWriter` (internal static) | `Internal/RdlWriter.cs` | constrói a árvore `<Report>` (motor do exporter) |
| `RdlExpression` (internal static) | `Internal/RdlExpression.cs` | dialeto VB/RDL → expressão OmniReport |
| `RdlExpressionReverse` (internal static) | `Internal/RdlExpressionReverse.cs` | expressão OmniReport → dialeto VB/RDL |

---

## 8.1 Estratégia: modelo nativo é a fonte da verdade; RDL é projeção

A decisão de arquitetura é explícita e tem consequências normativas:

1. **O `ReportDefinition` (modelo imutável) é o contrato canônico.** Serialização, render, import, code-first
   e Designer consomem o *mesmo* modelo. O formato de persistência nativo (`.repx` / `.repjson`) é o
   round-trip **lossless** (~98–100%, ver seção de serialização).

2. **RDL é uma *projeção* de interop, não um formato canônico.** O par importer/exporter existe para
   *migração* (ler um `.rdl` SSRS real) e *interop* (salvar um `.rdl` que abre no SSRS / Report Builder),
   **não** para ser o store primário do OmniReport. Um `.rdl` importado deve ser indistinguível, *no
   modelo*, de um report criado no Designer ou em código (`RdlImporter.cs:18-19`).

3. **O contrato de round-trip RDL é "por valor", não "por XML".** Como `RdlExporter` implementa
   `IReportSerializer`, `Load(Save(def))` é o contrato (`RdlExporter.cs:13-14`). A igualdade verificada é
   **semântica/de valor** do `ReportDefinition`, não a igualdade textual do XML — o exporter re-emite
   exatamente o subconjunto que o importer relê (`RdlWriter.cs:491-497`).

4. **Perda nunca é silenciosa.** Tanto o importer quanto o exporter acumulam avisos:
   - Import: `Metadata["ImportWarnings"]` (junção por `" | "`), preenchido a partir de `_warnings`
     (`RdlImporter.cs:42-44, 327-335`).
   - Export: `RdlExporter.Warnings` (`IReadOnlyList<string>`), repopulado a cada `Save`
     (`RdlExporter.cs:23-34`).

   Esta é uma **invariante**: todo caminho lossy emite um aviso textual (em português) em vez de descartar
   um valor sem rastro.

### Cobertura atual (autoavaliação do repositório)

Os números vêm de `docs/rdl-spec-compliance.md` (tabela §1):

| Direção | Cobertura | Notas |
|---|:--:|---|
| **Import** (`.rdl` → modelo) | **~82%** | DataSets+query, Tablix (flat→bandas / matrix / ColSpan / NoRowsMessage / PageBreak), Chart/Gauge/Subreport, CustomReportItem, estilo/visibilidade/ação/bookmark, cores nomeadas CSS3, variáveis, multi-coluna, cultura |
| **Export** (modelo → `.rdl`) | **~90%** | TODO `ReportElement` exporta; página/itens/Style/DataSets/Params/Variables/Tablix matrix+flat |
| **Round-trip interno** (`.repx`/`.repjson`) | **~98%** | lossless; auto-wiring por convenção |

### Robustez de parsing

- O parser é **namespace-agnóstico**: navega por `LocalName` (`El`/`Val` em `RdlImporter.cs:1534-1537`),
  aceitando RDL 2005 / 2008 / 2010 / 2016 sem detectar versão. O writer emite sempre o namespace
  `…/sqlserver/reporting/2016/01/reportdefinition` (`RdlWriter.cs:26-27`).
- A **importação estrutural nunca falha** em item desconhecido: tipos não suportados em `AddItem` são
  *pulados*, não erram (`RdlImporter.cs:474-475`). As únicas exceções (`FormatException`) são para
  documento vazio ou raiz `≠ <Report>` (`RdlImporter.cs:61-65`).

---

## 8.2 Conversão de dialeto de expressão

RDL usa expressões VB que começam com `=` e acesso de membro estilo VB (`Fields!Name.Value`); OmniReport usa
caminhos pontilhados (`Fields.Name`) e formas de função. A conversão é **bidirecional**.

### 8.2.1 Sentido import: `RdlExpression.Convert` (RDL → OmniReport)

`RdlExpression.IsExpression(raw)` é `true` sse `raw` começa com `=` (`RdlExpression.cs:34`). Um valor sem
`=` é texto literal e retorna **inalterado** (`RdlExpression.cs:49-52`). Para uma expressão, o corpo após o
`=` sofre as substituições abaixo (via `[GeneratedRegex]`), nesta ordem:

| Padrão RDL | Resultado OmniReport | Regex / regra |
|---|---|---|
| `Fields!X.Value` | `Fields.X` | `FieldRef` (`:16-17`) — **só** o membro `.Value` |
| `Parameters!P.Value` | `Parameters.P` | `ParameterRef` (`:19-20`) |
| `ReportItems!N.Value` | `ReportItems.N` | `ReportItemRef` (`:27-28`) |
| `Globals!PageNumber` / `OverallPageNumber` | `PageNumber` | `GlobalRef` (`:22-23, 57-63`) |
| `Globals!TotalPages` / `OverallTotalPages` | `TotalPages` | idem |
| `Globals!ExecutionTime` | `Now` | idem |
| `Globals!ReportName` (e outros) | identificador nu (`ReportName`) | idem |
| `User!UserID` | `UserName` | `UserRef` (`:30, 64`) |
| `a & b` (concat VB) | `Concat(a, b)` | `ConvertConcat` (`:93-102`) |
| `a Like "X*"` (infixo VB) | `Like(a, "X*")` | `ConvertLike` (`:74-87`) |

**Invariante de fidelidade do `.Value`:** somente o membro `.Value` é reescrito. `Parameters!P.Count` ou
`Fields!X.Label` são **deixados intactos** de propósito — emergem como erro de expressão visível em vez de
dado silenciosamente errado (`RdlExpression.cs:13-15`).

**Precedência VB respeitada:** `&` liga *mais forte* que `Like`. Logo `a & b Like "X*"` →
`Like(Concat(a, b), "X*")` (`RdlExpression.cs:39-42`). O parsing é *quote/paren-aware* e recursivo nos
argumentos de função (`ConvertParenGroups`, `:106-146`), honrando o escape VB de aspas dobradas (`""`).
Limitação documentada: o `Like` runtime suporta os curingas VB `* ? #`; classes de caractere `[...]`
**não** são suportadas (`RdlExpression.cs:42`).

### 8.2.2 Sentido export: `RdlExpressionReverse.ToRdl` (OmniReport → RDL)

Inverso exato. Para uma entrada não vazia retorna sempre a forma `=…` (`RdlExpressionReverse.cs:35-50`):

| OmniReport | RDL |
|---|---|
| `Fields.X` | `Fields!X.Value` (`FieldDot`, `:16-17`) |
| `Parameters.P` | `Parameters!P.Value` (`ParameterDot`) |
| `ReportItems.N` | `ReportItems!N.Value` (`ReportItemDot`) |
| `PageNumber` / `TotalPages` / `ReportName` | `Globals!…` (`BareGlobal`, `:27-28, 47`) |
| `Now` | `Globals!ExecutionTime` |
| `UserName` | `User!UserID` (`BareUser`, `:30-31, 48`) |
| `Concat(a, b, …)` | `a & b & …` (`UnwrapFunctions`, `:86-93`) |
| `Like(v, p)` | `v Like p` (`:94-97`) |

`UnwrapFunctions` re-parenteza um operando `Like` quando ele vira operando de `&` (pois `&` liga mais forte),
preservando o agrupamento: `a & (b Like c)` (`RdlExpressionReverse.cs:88-93, 116-125`). Funções não
mapeadas (`IIf`, `Switch`, …) são preservadas com seus argumentos recursados.

### 8.2.3 A classe de bug `ValueOf`-corrompe-literal e o helper `ValueRoundTrip`

Há uma armadilha de round-trip nas **value regions** (Chart/Gauge/Subreport/Tablix matrix). O importer
guarda muitos valores RDL como **literais** (via `Convert`, que deixa um valor sem `=` verbatim). Se o
exporter os re-emitisse com `ValueOf` (que prefixa `=`, marcando como expressão), o *próximo* import os
trataria como expressão e dobraria qualquer `&`/`Like` em `Concat`/`Like` — **corrompendo o literal**.

A defesa é o helper `ValueRoundTrip` (`RdlWriter.cs:618-626`):

```csharp
private static string ValueRoundTrip(string? expr)
{
    if (string.IsNullOrEmpty(expr)) return string.Empty;
    var asExpression = ValueOf(expr);                 // tenta a forma "=…"
    return RdlExpression.Convert(asExpression) == expr // sobrevive ao Convert do importer?
        ? asExpression                                 // sim → emite como expressão
        : expr;                                        // não → emite literal cru
}
```

Regra normativa: `ValueRoundTrip` só usa `ValueOf` quando a forma `=…` **sobrevive** ao `Convert` do
importer (volta idêntica); caso contrário emite o valor cru (que `Convert` deixa verbatim por não ter `=`).
O mesmo cuidado aparece *inline* em `NoRowsMessage` (legenda literal, emitida crua — `RdlWriter.cs:668-673,
869-871`). Usar `ValueOf` direto (sem o guard) é correto apenas onde o valor *é* sabidamente uma expressão
(ex.: `Tablix` `GroupExpression`/`SortExpression`, `RdlWriter.cs:728, 732`).

---

## 8.3 Tabela de mapeamento elemento → RDL

Despacho do importer em `RdlImporter.AddItem` (`:420-476`); do exporter em `RdlWriter.WriteItem`
(`:461-482`). N/A = não há arm dedicado.

| `ReportElement` | RDL (`<…>`) | Import | Export | Observações |
|---|---|:--:|:--:|---|
| `LabelElement` | `Textbox` (sem `=`) | ✅ | ✅ | literal; `Textbox` cujo valor não é expressão vira `Label` (`:1108-1109`) |
| `TextBoxElement` | `Textbox` (com `=`) | ✅ | ✅ | `CanGrow`/`CanShrink`; multi-`TextRun` → `TextRuns` + fallback de template (`:1090-1119`) |
| `LineElement` | `Line` | ✅ | ✅ | direção **inferida dos bounds** (RDL não tem `Direction`); Pen via `<Style><Border>` (`:1123-1135`, `WriteLine :990-1000`) |
| `RectangleElement` | `Rectangle` | ✅ | ✅ | filhos aninhados com bounds relativos (`Children`); recursão (`:434-446`, `WriteRectangle :1002-1012`) |
| `ImageElement` | `Image` | ✅ | 🟡 | `External`/`Embedded`/`Database` → `Path`/`Inline`/`Expression`; **export de bytes embutidos é fase posterior** (avisa, `:1014-1030`) |
| `TablixElement` | `Tablix` | ✅ | ✅ | matrix (groups+corner+body) e flat-table; ColSpan; ver §8.5 |
| `ChartElement` | `Chart` | 🟡 | 🟡 | subset achatado (tipo+categoria+séries); `Title`/`Legend`/cor/`Size`/`High`/`Low` não round-trippam (avisam) |
| `GaugeElement` | `GaugePanel` | 🟡 | 🟡 | tipo+valor do 1º ponteiro+Min/Max+`ScaleRanges` |
| `SubreportElement` | `Subreport` | ✅ | 🟡 | `ReportName`/`ParameterBindings`; `InlineDefinition`/`DataExpression` órfãos (§8.4) |
| `DataBarElement` | `CustomReportItem Type="DataBar"` | ✅ | — | importa via `CustomReportItemItem` (`:482-493`) |
| `SparklineElement` | `CustomReportItem Type="Sparkline"` | ✅ | — | idem |
| `IndicatorElement` | `CustomReportItem Type="Indicator"` | ✅ | — | idem |
| `GaugeElement` (CRI) | `CustomReportItem Type="Gauge"/"RadialGauge"/"LinearGauge"` | ✅ | — | idem |
| — | `Map` | 🔴 | — | **não importado** (impedância espacial); aviso explícito (`:459-461`) |

Outros elementos (`KpiElement`, etc.) caem em `Unsupported` no export (aviso "fase posterior",
`RdlWriter.cs:484-488`) e são pulados no import.

### Atributos comuns (todos os elementos)

`ApplyCommon` (import, `:973-1008`) e `WriteCommon` (export, `:1053-1095`) cobrem o que é comum a *todo*
report item:

| Aspecto | RDL | Modelo |
|---|---|---|
| Nome | atributo `Name` | `ReportElement.Name` (alimenta `ReportItems!N.Value`) |
| Estilo | `<Style>` (Font/Color/Border/Padding/Align/Format/WrapMode/BackgroundImage) | `Style` |
| Visibilidade | `<Visibility><Hidden>` (const ou expr) | `Visible` / `VisibleExpression` (**inverso** de `Hidden`, `:1071-1085`) |
| Bookmark | `<Bookmark>` | `Bookmark` |
| Doc map | `<DocumentMapLabel>` | `DocumentMapLabel` |
| Ação | `<Action>` (Hyperlink/BookmarkLink/Drillthrough) | `ElementAction` |
| Formatação condicional (expr) | sub-prop de `<Style>` que é `=expr` | `PropertyExpressions` (§8.6) |
| Bounds | `<Top>/<Left>/<Width>/<Height>` | `Bounds` (`Rectangle`) |

> **Invariante (`ApplyCommon`):** todo tipo de elemento que `AddItem` pode produzir **deve** ter um arm no
> `switch` de `ApplyCommon`; caso contrário Style/Visibility/Bookmark/Action seriam descartados em silêncio
> (`RdlImporter.cs:995-997`).

### Conversão de unidades

`ParseSize` (import) aceita `in/cm/mm/pt/pc/px`; um valor sem unidade ou com sufixo desconhecido retorna
`null` (o RDL exige unidade — o caller decide o fallback, `RdlImporter.cs:1594-1622`). `Size` (export) emite
sempre **milímetros** invariantes (`"0.####" + "mm"`, `RdlWriter.cs:1260-1263`). Tamanhos de fonte são
tratados em pontos como `double` para evitar o arredondamento de mils (`ParsePoints`, `:1472-1495`).

---

## 8.4 O que JÁ mapeia para o modelo nativo (não é órfão)

Antes de listar os órfãos, é normativo registrar o que **tem** representação nativa e round-trippa hoje —
para não confundir gaps de *shape* com perda de valor:

- **Página:** `PageWidth`/`Height`, margens, `Columns`/`ColumnSpacing` (snake columns), via `PageSetup`.
- **Parâmetros:** `DataType`, `Prompt`, `Nullable`, `AllowBlank`, `Hidden`, `MultiValue`, `DefaultValue`
  *literal*, `ValidValues` estático **e** por query (`DataSetReference`). `Required` é **derivado** de
  `!Nullable && sem DefaultValue` (RDL não tem `<Required>`, `:1265-1268`; o writer avisa se o modelo
  diverge dessa derivação, `RdlWriter.cs:186-192`).
- **DataSets:** `Fields` (+ `rd:TypeName`), `CalculatedFields` (`<Field><Value>`), `Filters` (dobrados em
  uma expressão booleana, `:203-241`), `SortExpressions`, `Query` (`_sql`/`_storedProc`/`param:*` —
  convenção viva do designer, `:168-189`).
- **Variáveis:** `<Variables>` de escopo Report → `ReportVariable` (`:316-324`).
- **Estilo/comportamento por item:** Font, ForeColor, BackColor, Border (4 lados), Padding, Align H/V,
  Format, WrapMode, BackgroundImage (`External`), Visibility (const+expr), Bookmark, DocumentMapLabel,
  Action (Hyperlink/BookmarkLink/Drillthrough), PageBreak (forma 2008+ `BreakLocation` e booleanos 2005),
  NoRowsMessage, ColSpan.
- **Metadados report-level:** `<Language>`, `<Description>`, `<Author>`, `<AutoRefresh>`,
  `<CustomProperties>`, `<Code>` (preservado em `Metadata["RdlCode"]`, não executado) — via `Metadata`
  (`:358-396`).
- **Tablix subtotais:** `RowSubtotals`/`ColumnSubtotals` *detectados* (membro `<Group/>` vazio) e
  *re-emitidos* (`:679-704`, `WriteTablixHierarchy :745-748`).

---

## 8.5 Tablix: dois shapes round-trip

O importer reconhece dois shapes, espelhados no exporter:

1. **Matrix/crosstab** (`TablixItem` ⇄ `WriteTablix`): grupos dinâmicos de linha+coluna, célula de canto
   `(0,0)` e célula de valor de corpo `(1,1)`, subtotais via membro `<Group/>` vazio
   (`:621-672`, `RdlWriter.cs:634-680`).

2. **Flat-table → bandas** (`TryFlatTablixBands` ⇄ `WriteFlatTablix`): um `Body` que é *exatamente* um
   `Tablix` plano (sem grupo dinâmico) e sem `<PageHeader>` é **decomposto** em uma banda de cabeçalho de
   coluna repetível (`PageHeader`) + uma `DetailBand` paginante (`:794-929`). O inverso reconstrói o
   `Tablix` plano único a partir das bandas, usando a grade de fronteiras-X (start+end) distintas — um
   `ColSpan` vira span e uma coluna descoberta vira célula vazia (`RdlWriter.cs:802-893`).

   O re-fold só dispara se o shape casar **inequivocamente** (`IsReconstructableFlatTable`,
   `RdlWriter.cs:54-60, 769-782`): sem `Groups`, sem `FilterExpression`/`SortExpressions` no Detail,
   header só de `Label`s, células sem overlap nem largura-zero. Um header gráfico (Line/Image) marca *page
   chrome* genuíno e é preservado como `<PageHeader>` — folded incorretamente corromperia o report
   (`RdlWriter.cs:48-60`). Quando não casa mas o Detail é data-bound, os elementos ainda saem como itens do
   Body (o dado nunca se perde) com aviso de que o `DataSetName` não round-trippa (`RdlWriter.cs:82-94`).

---

## 8.6 Formatação condicional como binding de propriedade

RDL expressa formatação condicional (negativo-em-vermelho, zebra, threshold) como sub-propriedades de
`<Style>` cujo valor é uma `=expr`. O importer mapeia essas para `PropertyExpressions` (path pontilhado →
expressão convertida, `ReadStyleExpressions :1013-1030`); o exporter faz o inverso (`WriteStyleExpressions
:1224-1240`). Os paths suportados (`StyleExpressionPaths`):

| Sub-prop RDL | Path OmniReport |
|---|---|
| `Color` | `Style.ForeColor` |
| `BackgroundColor` | `Style.BackColor` |
| `Format` | `Style.Format` |
| `TextAlign` | `Style.HorizontalAlignment` |
| `VerticalAlign` | `Style.VerticalAlignment` |
| `FontFamily` | `Style.Font.Family` |

Limitação: expressões de cor renderizam quando produzem `#hex`; coerção de cor *nomeada* a partir de
expressão é follow-up de render (`:1010-1012`). Note a distinção do tipo `ConditionalFormat` nativo
(`record ConditionalFormat(string Condition, Style Style)`, em `ReportElement.ConditionalFormats`): este é
uma **regra inteira** (condição → Style layerizado), não tem mapeamento RDL hoje e é um candidato a órfão
(§8.7).

---

## 8.7 Plano de extensão: round-trip RDL-canônico LOSSLESS via `<CustomProperties>`

Hoje vários campos do modelo **não têm contraparte RDL** e geram aviso de perda no export. O plano de
evolução (ainda **não implementado** — esta subseção é normativa-de-design, não descreve código existente)
é projetar esses campos órfãos em `<CustomProperties>` — o ponto de extensão **sancionado pelo XSD do RDL**
(par `Name`/`Value`, já lido pelo importer em `ReadMetadata :358-369` e ignorado pelo Report Builder). O
resultado é um `.rdl` que permanece **XSD-válido** e abre no SSRS, **sem perda** no round-trip OmniReport.

### Campos órfãos (perda conhecida hoje, com aviso)

Verificados no modelo (`src/Reporting.Core`):

| Campo órfão | Tipo CLR | Declaração | Aviso atual no export |
|---|---|---|---|
| `SubreportElement.InlineDefinition` | `ReportDefinition?` | `SubreportElement.cs:15` | `RdlWriter.cs:608-611` |
| `SubreportElement.DataExpression` | `string?` | `SubreportElement.cs:24` | idem |
| `ReportVariable.InitialValue` | `object?` (default `null`) | `ReportParameter.cs:60` | `RdlWriter.cs:418-421` |
| `DataField.DisplayName` | `string?` (default `null`) | `DataSourceDefinition.cs:32` | `RdlWriter.cs:334-336` |
| `TablixElement.SubtotalLabel` | `string?` | `AdvancedElements.cs:74` | `RdlWriter.cs:675-678` |
| `TablixElement.GrandTotalLabel` | `string?` | `AdvancedElements.cs:78` | idem |
| `ReportElement.ConditionalFormats` (regra) | `EquatableArray<ConditionalFormat>` | `ReportElement.cs:28` | — (não há arm; perda) |

> Distinção importante: `SubtotalLabel`/`GrandTotalLabel` são *rótulos* dos totais; os *flags* de subtotal
> (`RowSubtotals`/`ColumnSubtotals`) **já** round-trippam (§8.4). E `ConditionalFormats` (a *regra*
> condição→Style) é órfã, diferente das bindings *por-propriedade* de `PropertyExpressions` que já
> round-trippam (§8.6).

### Esquema de projeção proposto (`<CustomProperties>`)

Convenção de nomeação proposta — prefixo reservado `OmniReport.` para evitar colisão com `CustomProperties`
genuínos do report:

```xml
<!-- exportado dentro do report item / dataset / report, conforme o dono do campo -->
<CustomProperties>
  <CustomProperty>
    <Name>OmniReport.Tablix.SubtotalLabel</Name>
    <Value>Subtotal</Value>
  </CustomProperty>
  <CustomProperty>
    <Name>OmniReport.Tablix.GrandTotalLabel</Name>
    <Value>Total geral</Value>
  </CustomProperty>
</CustomProperties>
```

Regras de projeção por campo:

| Campo | `Name` proposto | Codificação do `Value` |
|---|---|---|
| `DataField.DisplayName` | `OmniReport.Field.DisplayName` | string literal |
| `ReportVariable.InitialValue` | `OmniReport.Variable.InitialValue` | escalar invariante (DateTime em `"o"`) |
| `TablixElement.SubtotalLabel` | `OmniReport.Tablix.SubtotalLabel` | string literal |
| `TablixElement.GrandTotalLabel` | `OmniReport.Tablix.GrandTotalLabel` | string literal |
| `SubreportElement.DataExpression` | `OmniReport.Subreport.DataExpression` | expressão (via `ToRdl`) |
| `SubreportElement.InlineDefinition` | `OmniReport.Subreport.InlineDefinition` | `.repjson` aninhado serializado (string) |
| `ConditionalFormats` (cada regra) | `OmniReport.ConditionalFormat.{i}` | par condição+Style serializado (`.repjson`) |

Invariantes do plano:

1. **XSD-válido:** `<CustomProperties>` é parte do esquema RDL oficial; nenhum elemento fora-de-esquema é
   introduzido. O Report Builder ignora propriedades que não reconhece.
2. **Idempotência:** ao reimportar, `ReadMetadata` já popula `Metadata` a partir de `<CustomProperties>`;
   a extensão promove as chaves `OmniReport.*` de volta aos campos tipados (em vez de deixá-las em
   `Metadata`), restaurando o valor **sem perda**.
3. **Sem regressão de aviso:** quando um campo órfão é projetado com sucesso, o aviso de perda
   correspondente deixa de ser emitido; o aviso permanece apenas para o que ainda não tiver projeção.
4. **Encoding-aware:** valores que começam com `=` ou contêm `&`/`Like` devem usar `ValueRoundTrip`/`ToRdl`
   quando forem expressões, e permanecer crus quando forem literais — a mesma disciplina de §8.2.3, para
   que a promoção de volta não os reinterprete.

> **Status:** projeto. O código atual emite avisos (`RdlExporter.Warnings`) para todos os campos da tabela
> de órfãos; a projeção `<CustomProperties>` fecharia o gap rumo ao round-trip RDL-canônico lossless sem
> sacrificar a validade XSD nem a abertura no SSRS.
