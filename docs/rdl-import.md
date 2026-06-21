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

- **Página**: `PageWidth`/`PageHeight` + margens. Tamanhos RDL (`21cm`, `2.5in`, `20pt`, `10mm`,
  `96px`) são convertidos; um valor **sem unidade** é tratado como não-especificado (a engine usa o
  default) — o schema RDL exige unidade.
- **Parâmetros**: nome, `DataType`→tipo CLR, `Prompt`, `Nullable`→`Required` (um parâmetro com
  `DefaultValue` não é obrigatório), `MultiValue`, `DefaultValue`, e **Available Values**:
  - estático: `<ValidValues><ParameterValues><ParameterValue><Value>/<Label>`;
  - query: `<ValidValues><DataSetReference><DataSetName>/<ValueField>/<LabelField>`.
- **Report items** livres em `Body` (→ banda ReportHeader), `PageHeader` e `PageFooter`:
  - `Textbox` → `TextBox` (se o valor for expressão `=…`) ou `Label` (texto literal);
  - `Line`, `Rectangle` (a forma + itens aninhados, deslocados para coordenadas absolutas),
  - `Image` externa (`Source=External`).
- **Tablix matrix/crosstab** (`<Tablix>` com hierarquias dinâmicas de linha **e** coluna) → `TablixElement`:
  `TablixRowHierarchy`/`TablixColumnHierarchy` (membros com `<Group><GroupExpression>`, recursivo p/ níveis
  aninhados, + sort do membro) → `RowGroups`/`ColumnGroups`; `<TablixCorner>` → célula `(0,0)`;
  valor do `<TablixBody>` → célula de corpo `(1,1)`; `DataSetName`. Tabelas planas / colunas estáticas e
  span por-célula são follow-up — um aviso vai em `Metadata["ImportWarnings"]` (nunca silencioso).
- **DataSets** (`<DataSets><DataSet>`) → `DataSourceDefinition` (metadados de binding; a execução da query
  fica delegada ao host `IReportDataSource`): `<Fields>` → `DataField` (campos com `<Value>` viram
  `CalculatedField`); `<Filters>` estruturado → `FilterExpression` booleano; `<SortExpressions>`;
  `<Query>` `CommandText`/`CommandType`/`QueryParameters` preservados em `Parameters` (record `Query`
  dedicado é follow-up).
- **Report-level**: `<EmbeddedImages>` → bytes inline (um `<Image Source="Embedded">` resolve a
  `ImageElement` com `InlineData`); `<CustomProperties>` → `Metadata`; `<Code>` (módulo VB report-level)
  preservado em `Metadata["RdlCode"]` (execução é follow-up).
- **Estilo e atributos** dos itens: nó `<Style>` (fonte/cores/borda/padding/alinhamento/format),
  `Visibility/Hidden`, `Bookmark`, `DocumentMapLabel`, `Action`, `CanGrow`/`CanShrink`.
- **Expressões** VB → OmniReport (`RdlExpression`): `Fields!X.Value`→`Fields.X`,
  `Parameters!P.Value`→`Parameters.P`, `Globals!PageNumber`→`PageNumber`, `Globals!ExecutionTime`→`Now`,
  `User!UserID`→`UserName`. Texto sem `=` é literal. Apenas o membro `.Value` é reescrito —
  `.Count`/`.Label` são preservados (erro visível em vez de dado errado).

## Ainda não importado (follow-ups)

Pulados silenciosamente (o import **estrutural sempre tem sucesso**, não lança):

- **Tablix/Matrix** e **Chart** (data regions) — o maior gap restante; precisa do mapeamento de
  hierarquias/células e do binding de dados (depende dos DataSets, já importados).
- **Subreports**; **TextRuns** múltiplos (só o 1º run é lido); **defaults multi-valor** (só o 1º valor);
  record `Query` dedicado (CommandText/QueryParameters hoje vivem em `DataSourceDefinition.Parameters`);
  operador infixo VB `Like` (use a função `Like()`).
- Botão "Importar .rdl" no Designer (a API pública já existe; falta o wiring de UI).
