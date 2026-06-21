# Import de `.rdl` (SSRS)

`RdlImporter` lê um arquivo **RDL** (Report Definition Language, o XML do SQL Server Reporting
Services) e produz um `ReportDefinition` normal do OmniReport — que renderiza na engine, abre no
Designer e é equivalente ao que o code-first / low-level produziriam. É a "compatibilidade RDL"
literal: o caminho de migração SSRS → OmniReport.

## Uso

```csharp
using Reporting.Serialization;

var def = new RdlImporter().Import(File.OpenRead("Vendas.rdl"), reportName: "Vendas");
// ou: new RdlImporter().ImportXml(xmlString);

// É um ReportDefinition comum — renderiza, serializa em .repx/.repjson, abre no Designer.
var pdf = await new ReportEngine().RenderAsync(def, dataSources);
```

## O que é importado (1º corte)

- **Página**: `PageWidth`/`PageHeight` + margens + `<Columns>`/`<ColumnSpacing>` (layout em colunas
  jornal/snake; o paginador flui o detalhe coluna-a-coluna). Tamanhos RDL (`21cm`, `2.5in`, `20pt`, `10mm`,
  `96px`) são convertidos; um valor **sem unidade** é tratado como não-especificado (a engine usa o
  default) — o schema RDL exige unidade.
- **Parâmetros**: nome, `DataType`→tipo CLR, `Prompt`, `Nullable`→`Required` (um parâmetro com
  `DefaultValue` não é obrigatório), `MultiValue`, `DefaultValue`, e **Available Values**:
  - estático: `<ValidValues><ParameterValues><ParameterValue><Value>/<Label>`;
  - query: `<ValidValues><DataSetReference><DataSetName>/<ValueField>/<LabelField>`.
- **Report items** livres em `Body` (→ banda ReportHeader), `PageHeader` e `PageFooter`:
  - `Textbox` → `TextBox` (se o valor for expressão `=…`) ou `Label` (texto literal); um Textbox com
    **vários `<TextRun>`** (formatação mista numa caixa) → `TextBox.TextRuns` (valor + `<Style>` + `<ActionInfo>`
    por-run; parágrafos separados por quebra de linha; `Expression` de fallback concatena os runs). O render
    desenha os runs concatenados com o estilo do TextBox — estilo visual por-run e hotspot de ação por-run são
    follow-up; `MarkupType=HTML` é achatado com aviso;
  - `Line` (direção Horizontal/Vertical/diagonal inferida dos bounds — altura ~0 = régua horizontal,
    largura ~0 = vertical; antes toda linha virava diagonal), `Rectangle` (a forma + itens aninhados,
    deslocados para coordenadas absolutas),
  - `Image` externa (`Source=External`); `<Sizing>` → `ImageSizing` (`Fit`→Stretch, `FitProportional`→Fit,
    `Clip`→Native; `AutoSize`/ausente → Fit, o default do model — sem equivalente de "crescer o item").
- **Data viz**: `<Chart>` → `ChartElement` (tipo da 1ª série, categoria da hierarquia, uma `ChartSeries`
  por série com valor do 1º DataValue); `<GaugePanel>` → `GaugeElement` (Radial/Linear + valor do 1º
  ponteiro); `<Subreport>` → `SubreportElement` (ReportName→ReportId + Parameters→ParameterBindings).
  `<Map>` e `<CustomReportItem>` (DataBar/Sparkline/Indicator) → aviso em `Metadata["ImportWarnings"]`.
- **Tablix tabela plana** (`<Tablix>` Table/List sem grupo dinâmico em nenhum eixo — colunas estáticas +
  linha Details):
  - quando é o **único** item do Body (e a página não tem `<PageHeader>`) → **decompõe em bandas**: a linha
    de cabeçalho vira uma banda **PageHeader** (Labels, repete por página) e a linha de detalhe vira a
    **DetailBand** (1 `TextBox` por coluna, `DataSetName` do Tablix), de modo que a tabela **pagina
    nativamente** e repete o cabeçalho — em vez de um bloco único. Colunas posicionadas por largura absoluta,
    reescaladas para caber na largura do Tablix; `<Style>`/Format por célula preservados.
  - caso contrário (Body com outros itens) → `TablixElement` no modo tabela: célula `(0,c)` = header label,
    `(1,c)` = detalhe TextBox, `<TablixColumns>`/`Width` → `ColumnWidths` (pesos relativos).
    O `<PageBreak><BreakLocation>` (Start/End/StartAndEnd; e o legado RDL 2005 `PageBreakAtStart`/`AtEnd`) do
    Tablix vira `DetailBand.PageBreak` (quebra de página antes/depois — ex.: "cada grupo em sua página").
  - Header vs detalhe classificados pela hierarquia de linha (o membro com `<Group>` é o detalhe). Híbrido
    tabela+matrix, múltiplas linhas de detalhe, row-group headers/footers e ColSpan são follow-up (com aviso);
    múltiplas DetailBands (várias data regions no Body) também (cai no `TablixElement`).
- **Tablix matrix/crosstab** (`<Tablix>` com hierarquias dinâmicas de linha **e** coluna) → `TablixElement`:
  `TablixRowHierarchy`/`TablixColumnHierarchy` (membros com `<Group><GroupExpression>`, recursivo p/ níveis
  aninhados, + sort do membro) → `RowGroups`/`ColumnGroups`; `<TablixCorner>` → célula `(0,0)`;
  valor do `<TablixBody>` → célula de corpo `(1,1)`; `DataSetName`. Híbrido tabela+matrix e span por-célula
  são follow-up — um aviso vai em `Metadata["ImportWarnings"]` (nunca silencioso).
- **Tablix `<NoRowsMessage>`** (literal ou `=expressão`) → `TablixElement.NoRowsMessage`, em ambos os modos
  (tabela plana e matrix). No render, um dataset vazio mostra a mensagem centralizada no lugar da grade
  (expressões são avaliadas).
- **DataSets** (`<DataSets><DataSet>`) → `DataSourceDefinition` (metadados de binding; a execução da query
  fica delegada ao host `IReportDataSource`): `<Fields>` → `DataField` (campos com `<Value>` viram
  `CalculatedField`); `<Filters>` estruturado → `FilterExpression` booleano; `<SortExpressions>`;
  `<Query>` → a **convenção viva do Designer** (`_sql`/`_storedProc`/`param:@x` em `Parameters`), a mesma
  que `DesignerDataSource`/`DataSourceFactory` consomem — então a query importada **abre no editor de fonte
  de dados e executa** (`CommandType=StoredProcedure`→`_storedProc`; `<QueryParameter>` `=Parameters!P.Value`
  → bind ao parâmetro P, valor literal → literal; um valor de **expressão** não-paramétrica, ex. `=Today()`,
  é congelado como literal **com aviso**). `CommandType=TableDirect` é tratado como texto.
- **Report-level**: `<EmbeddedImages>` → bytes inline (um `<Image Source="Embedded">` resolve a
  `ImageElement` com `InlineData`); `<CustomProperties>` → `Metadata`; `<Code>` (módulo VB report-level)
  preservado em `Metadata["RdlCode"]` (execução é follow-up).
- **Estilo e atributos** dos itens: nó `<Style>` (fonte/cores/borda/padding/alinhamento/format/`WrapMode`
  →WordWrap, onde `NoWrap` desliga a quebra),
  `Visibility/Hidden`, `Bookmark`, `DocumentMapLabel`, `Action`, `CanGrow`/`CanShrink`.
- **Expressões** VB → OmniReport (`RdlExpression`): `Fields!X.Value`→`Fields.X`,
  `Parameters!P.Value`→`Parameters.P`, `Globals!PageNumber`/`OverallPageNumber`→`PageNumber`,
  `Globals!TotalPages`/`OverallTotalPages`→`TotalPages`, `Globals!ReportName`→`ReportName` (resolve ao
  `ReportDefinition.Name` no render), `Globals!ExecutionTime`→`Now`, `User!UserID`→`UserName`. Texto sem `=` é literal. Apenas o membro `.Value` é reescrito —
  `.Count`/`.Label` são preservados (erro visível em vez de dado errado).

## Ainda não importado (follow-ups)

Avisados via `Metadata["ImportWarnings"]` ou parcialmente suportados (o import **estrutural sempre tem
sucesso**, nunca lança nem descarta em silêncio):

- **Tablix** híbrido tabela+matrix / span por-célula / múltiplas linhas de detalhe; **Map** e
  **CustomReportItem** (DataBar/Sparkline/Indicator) — avisados.
- **TextRun** com `MarkupType=HTML` (achatado p/ texto, avisado); estilo visual por-run e hotspot de ação
  por-run no render (preservados no modelo, desenho é follow-up). Células de **Tablix** (corner/body) ainda
  achatam para o 1º run (multi-run só nos Textboxes livres por ora).
- **defaults multi-valor** (só o 1º valor); record `Query` 1ª-classe dedicado (a query importada já é
  funcional via as chaves `_sql`/`param:` em `DataSourceDefinition.Parameters`; promover a um record é
  refino futuro). O operador infixo VB `Like` **é convertido** (`a Like "X*"` →
  `Like(a, "X*")`, honrando a precedência `&` > `Like`); classes de caractere `[...]` no padrão não são
  suportadas pela função `Like()` subjacente (só os curingas `* ? #`).
- **`ReportItems!X.Value`** é importado e resolve em bandas renderizadas **depois** da referenciada (ex.:
  rodapé ecoando o corpo); `ReportItems!X.Value` num **cabeçalho de página** referenciando o corpo retorna
  vazio (a header renderiza antes — 2º passe é follow-up).
- Botão "Importar .rdl" no Designer (a API pública já existe; falta o wiring de UI).
