# 7. Serialização: .repx e .repjson

Esta seção é normativa quanto ao formato de persistência de um `ReportDefinition`. Existem dois formatos *nativos* — equivalentes em conteúdo, diferindo apenas no envelope sintático:

| Formato | Identificador | Extensão | Codificação física |
|---|---|---|---|
| `.repx` | `"repx"` | `.repx` | XML, UTF-8 sem BOM, indentado com 2 espaços, com declaração `<?xml ...?>` |
| `.repjson` | `"repjson"` | `.repjson` | JSON, UTF-8, indentado (`Indented = true`) |

Ambos são *human-friendly* e *diff-able*: medidas geométricas são gravadas como **mils inteiros** (milésimos de polegada — ver §7.3), eliminando o erro de ponto-flutuante e tornando o round-trip lossless para a geometria. O `.repjson` espelha o schema do `.repx`; a diferença essencial é que o JSON discrimina o tipo de elemento por um campo `"kind"`, enquanto o XML o discrimina pelo **nome da tag**.

Arquivos-fonte: `src/Reporting.Serialization/IReportSerializer.cs`, `RepxSerializer.cs`, `RepJsonSerializer.cs`, `SchemaVersion.cs`, e `Internal/{RepxWriter,RepxReader,RepJsonWriter,RepJsonReader,Formats,ElementSerializationRegistry}.cs`.

## 7.1 Contrato `IReportSerializer`

`IReportSerializer` (`IReportSerializer.cs:8`) é o contrato comum aos dois formatos.

```csharp
public interface IReportSerializer
{
    string Format { get; }         // "repx" | "repjson"
    string FileExtension { get; }  // ".repx" | ".repjson"
    void Save(ReportDefinition definition, Stream stream);
    ReportDefinition Load(Stream stream);
}
```

| Membro | Tipo | Semântica |
|---|---|---|
| `Format` | `string` | Identificador do formato. `"repx"` (`RepxSerializer.cs:22`), `"repjson"` (`RepJsonSerializer.cs:20`). |
| `FileExtension` | `string` | Extensão default *com* o ponto inicial. |
| `Save(def, stream)` | `void` | Serializa `def` para `stream`. Lança `ArgumentNullException` se `definition` ou `stream` forem nulos. |
| `Load(stream)` | `ReportDefinition` | Desserializa de `stream`. Lança `ArgumentNullException` se `stream` for nulo; `FormatException` para conteúdo malformado. |

### 7.1.1 Helpers de conveniência

`ReportSerializerExtensions` (`IReportSerializer.cs:22`) adiciona, por extensão, atalhos de bytes e arquivo. Todos validam os argumentos (`ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`):

| Método | Assinatura | Semântica |
|---|---|---|
| `SaveToBytes` | `byte[] SaveToBytes(this IReportSerializer, ReportDefinition)` | Serializa para um `byte[]` UTF-8 via `MemoryStream`. |
| `LoadFromBytes` | `ReportDefinition LoadFromBytes(this IReportSerializer, ReadOnlySpan<byte>)` | Desserializa de um buffer (cria um `MemoryStream` read-only). |
| `SaveToFile` | `void SaveToFile(this IReportSerializer, ReportDefinition, string path)` | `File.Create(path)` + `Save`. |
| `LoadFromFile` | `ReportDefinition LoadFromFile(this IReportSerializer, string path)` | `File.OpenRead(path)` + `Load`. |

### 7.1.2 `LoadWithVersion` (apenas concretos)

Cada serializer concreto expõe, **além** da interface, um método que devolve também a versão de schema lida do documento:

```csharp
public (ReportDefinition Definition, SchemaVersion Version) LoadWithVersion(Stream stream);
```

Definido em `RepxSerializer.cs:50` e `RepJsonSerializer.cs:45`. Não faz parte de `IReportSerializer`.

## 7.2 Garantia de round-trip (~98% lossless)

> **Invariante (estrutural).** Para qualquer `def` produzido pela API code-first, designer ou importador: `Load(Save(def)).Equals(def)` (`IReportSerializer.cs:5-6`).

A igualdade é **estrutural**, garantida pelo fato de `ReportDefinition` e todos os tipos do modelo serem `record`/`record struct` imutáveis com coleções `EquatableArray<T>`/`EquatableDictionary<K,V>` (ver §3 do modelo). Dois objetos com os mesmos campos são `Equals`, independentemente de identidade de referência.

O round-trip é descrito como **~98% lossless**, e não 100%, por causa das normalizações abaixo. Estas são as *únicas* divergências conhecidas entre `def` original e `Load(Save(def))`:

| Divergência | Causa | Efeito no round-trip |
|---|---|---|
| `ReportDefinition.SchemaVersion` reescrito | O writer sempre emite `SchemaVersion.Current` (`RepxSerializer.cs:29`, `RepJsonSerializer.cs:27`), ignorando o valor do `def` de entrada. O reader copia a versão do *documento* de volta para `def.SchemaVersion` (`RepxReader.cs:60`, `RepJsonReader.cs:51`). | Um `def` com `SchemaVersion` diferente de `"1.0"` não round-trip-a esse campo; é normalizado para a versão corrente. |
| `Style` materializado | Detalhado em §7.10 — `ForeColor`/`Font` "inherit" (`null`) podem ser preenchidos por defaults em camadas acima do serializer (designer), não pelo serializer em si. O serializer **preserva** `null` fielmente. | Pré-existente, fora do escopo do serializer. |
| `ReportElement.Id` ausente | Se a tag/objeto não tem `Id`, o reader **gera** `Guid.NewGuid().ToString("n")` (`RepxReader.cs:282`, `RepJsonReader.cs:273`). | Só afeta documentos escritos à mão sem `Id`; o writer sempre emite `Id`. |

A geometria (`Unit`, `Rectangle`, `Thickness`) é **100% lossless** porque é serializada como inteiros em mils (§7.3). Floats não-geométricos (ex. `Font.Size`, `ChartSeries`) usam round-trip `"R"`/`NumberStyles.Any` invariante.

## 7.3 Tipos-folha e o formato `Formats`

`Internal/Formats.cs` centraliza a (de)serialização dos tipos-folha, **sempre** com `CultureInfo.InvariantCulture` (`Formats.cs:12`) — os arquivos são culture-independent.

| Tipo CLR | Forma persistida | Writer | Reader | Notas |
|---|---|---|---|---|
| `Unit` | mils inteiros, ex. `787` | `FormatUnit` → `unit.Mils.ToString(Inv)` | `ParseUnit` | `ParseUnit` aceita também sufixos `mm`/`in`/`pt` (`394`, `10mm`, `0.5in`, `8pt`) — `Formats.cs:24-39`. String vazia → `Unit.Zero`. |
| `Rectangle` | `"X,Y,W,H"` em mils | `FormatRectangle` | `ParseRectangle` | Exatamente 4 componentes, senão `FormatException`. Vazio → `Rectangle.Empty`. |
| `Thickness` | `"L,T,R,B"` em mils | `FormatThickness` | `ParseThickness` | 4 componentes. Vazio → `Thickness.Zero`. |
| `Color` | `#hex` (ex. `#FF0000`) | `FormatColor` → `Color.ToHex()` | `ParseColor` | O writer sempre emite `#hex`. O reader aceita também nome CSS/RDL (`Red`) via `Color.FromName` (`Formats.cs:93`) p/ paridade com o importador RDL. Vazio → `Color.Transparent`. |
| `Type` (parâmetros/fields) | `AssemblyQualifiedName` (fallback `FullName`/`Name`) | `FormatType` | `ParseType` | `ParseType` tenta `Type.GetType`, depois um switch de tipos BCL comuns por nome simples (`DateTime`, `String`, `Int32`, `Decimal`, …) — `Formats.cs:110-121`. |

> **Nota sobre `Unit`.** 1 polegada = 1000 mils; 1 mm ≈ 39,37 mils (`Unit.cs:13-21`). A escolha de inteiros torna o empilhamento de bandas exato e o snap-to-grid trivial. A4 retrato → `Paper.Width = 8268` mils (210mm), `Paper.Height = 11693` mils (297mm); margem default 20mm → `787` mils.

## 7.4 Estrutura do documento — visão geral

### 7.4.1 `.repx` (XML)

A raiz é `<Report>` com atributos `SchemaVersion` e `Name` (`RepxWriter.cs:21`). Os filhos aparecem em **ordem fixa** (a ordem em que `RepxWriter.Write` os adiciona):

```
<Report SchemaVersion="1.0" Name="...">
  <PageSetup …><Paper …/></PageSetup>      (sempre)
  <Parameters>  …  </Parameters>           (se Count > 0)
  <DataSources> …  </DataSources>          (se Count > 0)
  <Variables>   …  </Variables>            (se Count > 0)
  <ReportHeader …>                         (se != null)
  <PageHeader   …>                         (se != null)
  <Groups>      …  </Groups>               (se Count > 0)
  <Detail …>                               (sempre)
  <PageFooter   …>                         (se != null)
  <ReportFooter …>                         (se != null)
  <Metadata>    …  </Metadata>             (se Count > 0)
</Report>
```

O reader localiza filhos **por nome** (`root.Element("...")`), portanto não depende da ordem; a ordem fixa do writer existe só para diffs estáveis. O reader exige que a raiz se chame `Report`, senão `FormatException` (`RepxReader.cs:22-24`).

### 7.4.2 `.repjson` (JSON)

A raiz é um objeto com as chaves camelCase abaixo, na ordem de inserção do writer (`RepJsonWriter.cs:15`). Coleções vazias e bandas `null` são **omitidas** (mesma política sparse do XML):

| Chave JSON | Presença | Equivalente `.repx` |
|---|---|---|
| `schemaVersion` | sempre | atributo `SchemaVersion` |
| `name` | sempre | atributo `Name` |
| `pageSetup` | sempre | `<PageSetup>` |
| `parameters` | se `Count > 0` | `<Parameters>` |
| `dataSources` | se `Count > 0` | `<DataSources>` |
| `variables` | se `Count > 0` | `<Variables>` |
| `reportHeader` | se `!= null` | `<ReportHeader>` |
| `pageHeader` | se `!= null` | `<PageHeader>` |
| `groups` | se `Count > 0` | `<Groups>` |
| `detail` | sempre | `<Detail>` |
| `pageFooter` | se `!= null` | `<PageFooter>` |
| `reportFooter` | se `!= null` | `<ReportFooter>` |
| `metadata` | se `Count > 0` | `<Metadata>` (objeto chave→valor) |

O reader exige raiz objeto (`RepJsonSerializer.cs:38-40`) e lê por chave (`root["..."]`).

## 7.5 `PageSetup`

`.repx` (`RepxWriter.cs:81`):

```xml
<PageSetup Orientation="Portrait" Margins="787,787,787,787" Columns="1" ColumnSpacing="0">
  <Paper Name="A4" Width="8268" Height="11693" />
</PageSetup>
```

`.repjson` (`RepJsonWriter.cs:69`):

```json
"pageSetup": {
  "paper": { "name": "A4", "width": "8268", "height": "11693" },
  "orientation": "Portrait",
  "margins": "787,787,787,787",
  "columns": 1,
  "columnSpacing": "0"
}
```

| Campo | Tipo | Default na leitura | Notas |
|---|---|---|---|
| `Orientation` | enum `Orientation` {`Portrait`, `Landscape`} | `Portrait` | |
| `Margins` | `Thickness` (mils L,T,R,B) | `Thickness.Zero` | |
| `Columns` | `int` | `1` | No JSON é número nativo; no XML é texto. |
| `ColumnSpacing` | `Unit` (mils) | `Unit.Zero` | |
| `Paper.Name/Width/Height` | `string`/`Unit`/`Unit` | `"A4"` / — / — | `<Paper>` é obrigatório; ausente → `FormatException` (`RepxReader.cs:86`). |

Se `<PageSetup>`/`pageSetup` estiver totalmente ausente, o reader usa `PageSetup.A4Portrait` (`RepxReader.cs:33`, `RepJsonReader.cs:27`).

## 7.6 Parameters, Variables, DataSources

### 7.6.1 `Parameter`

| Campo | `.repx` | `.repjson` | Default leitura | Política sparse |
|---|---|---|---|---|
| `Name` | attr `Name` | `name` | (obrigatório) | sempre |
| `ValueType` | attr `Type` | `type` | `typeof(object)` | sempre |
| `Required` | attr `Required` | `required` | `true` | sempre |
| `AllowMultiple` | attr `AllowMultiple` | `allowMultiple` | `false` | sempre |
| `Prompt` | attr `Prompt` | `prompt` | `null` | só se `!= null` |
| `Nullable` | attr `Nullable` | `nullable` | `false` | só se `true` |
| `AllowBlank` | attr `AllowBlank` | `allowBlank` | `false` | só se `true` |
| `Hidden` | attr `Hidden` | `hidden` | `false` | só se `true` |
| `DefaultValue` | `<DefaultValue Value="…"/>` | `defaultValue` (string) | `null` | só se `!= null`; lido via `Convert.ChangeType(…, ValueType)` |
| `AvailableValues` | `<AvailableValues>` | `availableValues` | `null` | só se há valores ou é query |

`AvailableValues` carrega `DataSet`/`ValueField`/`LabelField` (binding a query) e uma lista `Value`/`value` com `Label`/`label` opcional (`RepxWriter.cs:116`, `RepJsonWriter.cs:107`).

### 7.6.2 `Variable`

`ReportVariable` = (`Name`, `Expression`, `Scope`). Scope é enum `VariableScope`, default `Row` na leitura.

```xml
<Variable Name="vTotal" Expression="Sum(Fields.Amount)" Scope="Report" />
```

### 7.6.3 `DataSource`

`DataSourceDefinition` round-trip-a: `Name` (obrigatório), `DataMember`, `FilterExpression`, e sub-coleções emitidas só quando não vazias: `Fields` (`Name`/`Type`/`DisplayName`), `CalculatedFields` (`Name`/`Expression`/`Type` — estilo RDL), `Relations` (master-detail: `Name`/`ParentSource`/`ParentField`/`ChildSource`/`ChildField`), `SortExpressions`, e `Parameters` (dicionário chave→valor) — `RepxWriter.cs:139`, `RepJsonWriter.cs:135`.

> **Diferença de ordenação observável.** No `.repx`, `<CalculatedFields>` é escrito logo após `<Fields>`; no `.repjson`, `calculatedFields` aparece após `filterExpression`. É só ordem de chaves — semanticamente idêntico (leitura por nome).

## 7.7 Bandas

`SortExpressions` é um helper compartilhado por data sources, bandas e grupos — `<Sort Expression="…" Direction="Ascending|Descending"/>` (`RepxWriter.cs:204`).

### 7.7.1 Bandas simples (`ReportHeader`/`PageHeader`/`PageFooter`/`ReportFooter`, e `Header`/`Footer` de grupo)

`WriteBand(tagName, band)` (`RepxWriter.cs:211`):

| Campo | `.repx` attr | `.repjson` | Default leitura |
|---|---|---|---|
| `Kind` | `Kind` | `kind` | (do contexto de leitura) |
| `Height` | `Height` (mils) | `height` | `Unit.Zero` |
| `Visible` | `Visible` | `visible` | `true` |
| `PrintOnFirstPage` | `PrintOnFirstPage` | `printOnFirstPage` | `true` |
| `PrintOnLastPage` | `PrintOnLastPage` | `printOnLastPage` | `true` |
| `VisibleExpression` | `VisibleExpression` (se `!= null`) | `visibleExpression` | `null` |
| `PageBreak` | `PageBreak` (se `!= None`) | `pageBreak` (sempre no JSON) | `None` |
| elementos | `<Elements>` | `elements` | vazio |

> No XML, `Kind` é gravado por `WriteBand`, mas **na leitura é ignorado**: `ReadReportBand` recebe o `BandKind` do contexto (qual tag está sendo lida) — `RepxReader.cs:44-47`.

### 7.7.2 `Detail`

`DetailBand` (`RepxWriter.cs:231`) tem campos próprios além dos de banda:

| Campo | `.repx` | `.repjson` | Default leitura |
|---|---|---|---|
| `Height` | attr `Height` | `height` | `Unit.Zero` |
| `Visible` | attr `Visible` | `visible` | `true` |
| `CanGrow` | attr `CanGrow` | `canGrow` | `false` |
| `CanShrink` | attr `CanShrink` | `canShrink` | `false` |
| `VisibleExpression` | attr (opt) | `visibleExpression` | `null` |
| `PageBreak` | attr (se `!= None`) | `pageBreak` | `None` |
| `NoRowsMessage` | attr (opt) | `noRowsMessage` | `null` |
| `DataSetName` | attr (opt) | `dataSetName` | `null` |
| `FilterExpression` | attr (opt) | `filterExpression` | `null` |
| `SortExpressions` | `<SortExpressions>` | `sortExpressions` | vazio |
| elementos | `<Elements>` | `elements` | vazio |
| `SubDetails` | `<SubDetails>` | `subDetails` | vazio |

`Detail` ausente → `DetailBand.Empty` (`RepxReader.cs:52`).

### 7.7.3 `SubDetail` (master-detail)

Cada `<SubDetail>` carrega `Name`, `DataMember`, `Height`, `Visible`, `PrintIfEmpty`, mais `VisibleExpression`/`NoRowsMessage`/`FilterExpression`/`SortExpressions` opcionais, seus próprios `<Elements>`, e `<Header>`/`<Footer>` opcionais (`RepxWriter.cs:273`). No XML, `Header`/`Footer` reusam a forma de banda mas **anexados como filhos** do `<SubDetail>` para manter a árvore compacta; o reader os reconstrói como `ReportBand` com `BandKind.Detail` (`RepxReader.cs:208`).

### 7.7.4 `Group`

`GroupBand` round-trip-a `Name`, `GroupExpression`, flags (`KeepTogether`, `NewPageBefore`, `NewPageAfter`, `RepeatHeaderOnNewPage`, `Visible`), `VisibleExpression`/`PageBreak`/`FilterExpression`/`SortExpressions` opcionais, `Variables` (escopo de grupo) e `Header`/`Footer` opcionais (`RepxWriter.cs:315`).

## 7.8 Elementos — envelope comum

Todo `ReportElement` é serializado por `WriteElement` (`RepxWriter.cs:358`, `RepJsonWriter.cs:333`). O **envelope** (campos comuns a qualquer elemento) é idêntico entre os dois formatos; só o discriminador difere:

- **`.repx`**: o tipo é o **nome da tag** (`<Label>`, `<TextBox>`, …).
- **`.repjson`**: o tipo é o campo `"kind"` (string), e o objeto é sempre um `{…}`.

| Campo (base `ReportElement`) | `.repx` | `.repjson` | Default leitura | Sparse |
|---|---|---|---|---|
| discriminador | nome da tag | `kind` | (obrigatório no JSON) | sempre |
| `Id` | attr `Id` | `id` | `Guid.NewGuid("n")` se ausente | sempre (writer) |
| `Bounds` | attr `Bounds` (`X,Y,W,H` mils) | `bounds` | `Rectangle.Empty` | sempre |
| `Visible` | attr `Visible` | `visible` | `true` | sempre |
| `Name` | attr `Name` | `name` | `null` | só se `!= null` |
| `VisibleExpression` | attr | `visibleExpression` | `null` | só se `!= null` |
| `Style` | `<Style>` | `style` | `Style.Default` | sempre |
| `ConditionalFormats` | `<ConditionalFormats>` | `conditionalFormats` | vazio | só se `Count > 0` |
| `PropertyExpressions` | `<PropertyExpressions>` (`Path`/`Expression`) | `propertyExpressions` (obj) | vazio | só se `Count > 0` |
| `Action` | `<Action>` (filho) | `action` | `null` | só se `!= null` |
| `Bookmark` | attr | `bookmark` | `null` | só se `!= null` |
| `DocumentMapLabel` | attr | `documentMapLabel` | `null` | só se `!= null` |
| `ToggleItemId` | attr | `toggleItemId` | `null` | só se `!= null` |
| `InitiallyHidden` | attr | `initiallyHidden` | `false` | só se `true` |

> Os campos `Id`/`Visible`/`Style`/`Action`/… são `init-only` na base abstrata e por isso são aplicados na leitura via expressão `with` após construir o subtipo concreto (`RepxReader.cs:342`, `RepJsonReader.cs:381`).

`Action` (`ElementAction`) é gravado como **filho** (e não atributo) porque tem sub-árvore própria de parâmetros de drillthrough: `Kind` + `Hyperlink`/`BookmarkId`/`DrillthroughReportName` opcionais + `<Parameters>` (`Name`/`Value`/`Omit`) — `RepxWriter.cs:475`.

### 7.8.1 `Style`

`WriteStyle` (`RepxWriter.cs:619`, `RepJsonWriter.cs:768`):

| Campo | Presença | Forma |
|---|---|---|
| `HorizontalAlignment` | sempre | enum (default leitura `Left`) |
| `VerticalAlignment` | sempre | enum (default leitura `Top`) |
| `WordWrap` | sempre | bool (default leitura `true`) |
| `Format` | só se `!= null` | string (format string) |
| `Font` | só se `!= null` | `Family`/`Size`/`Style`(enum `FontStyle`); default leitura `Arial`/`10`/`Regular` |
| `ForeColor`/`BackColor` | só se `!= null` | `#hex`; **`null` round-trip-a como ausência** (= "inherit") |
| `Padding` | só se `!= null` | `Thickness` mils |
| `Border` | só se `!= null` | 4 lados `Side` (`Position`/`Style`/`Thickness`/`Color`) |
| `BackgroundImage` | só se `!= null` | `Path`/`Expression` |

> **`ForeColor`/`Font` "inherit".** O serializer preserva `null` fielmente (campo ausente no arquivo). A materialização de `Black`/`Arial-10` que se observa em alguns fluxos ocorre **acima** do serializer (no view-model do designer), não aqui.

## 7.9 Elementos — tipos hand-wired

Para cada subtipo conhecido, `WriteElement` tem um *braço* (switch) que emite seu payload específico depois do envelope. Os pares tag (XML) ↔ `kind` (JSON) são idênticos. A tabela lista o payload distintivo:

| `kind` / tag | Campos específicos | Refs |
|---|---|---|
| `Label` | `Text` | `RepxWriter.cs:362` |
| `TextBox` | `Expression`; attrs `CanGrow`/`CanShrink` (só se `true`); `<TextRuns>` (rich text RDL F1.8, só se `Count > 0`) com `Value`/`Style`/`Action` por run | `RepxWriter.cs:681` |
| `Line` | `Direction` (enum `LineDirection`); `Pen` = `BorderSide` (`Style`/`Thickness`/`Color`). Pen ausente → `Solid`, `0.5pt`, `Black` | `RepxReader.cs:374` |
| `Rectangle` | `FillColor` (vazio = `null`); `CornerRadius`; `Children` (container aninhado, bounds relativos) — XML emite `<Children>` sempre (vazio round-trip-a p/ o default); JSON só se `Count > 0` | `RepxWriter.cs:373` |
| `Ellipse` | `FillColor` | `RepxWriter.cs:383` |
| `Image` | `Source` (enum `ImageSourceKind`); `Sizing` (enum `ImageSizing`); `Path`/`Expression` opcionais; `InlineData` (Base64) | `RepxWriter.cs:501` |
| `Barcode` | `Symbology`/`Expression`/`ShowText`/`QrEcc` | `RepxWriter.cs:389` |
| `Chart` | `Kind`(JSON `chartKind`); `ShowLegend`; `Title` opcional; `Series` (`Name`/`CategoryExpression`/`ValueExpression` + `Color`/`Size`/`High`/`Low` opcionais) | `RepxWriter.cs:528` |
| `Subreport` | `ReportId`; `InlineDefinition` (um `<Report>`/objeto aninhado completo — recursivo); `DataExpression`; `ParameterBindings` | `RepxWriter.cs:560` |
| `Table` | `HeaderHeight`/`DetailHeight`/`FooterHeight`; `DataExpression`; `Columns` (`Name`/`Width` + `HeaderText`/`DetailExpression`/`FooterExpression` opcionais) | `RepxWriter.cs:583` |
| `Tablix` | `DataSetName`; `RowSubtotals`/`ColumnSubtotals` (só se `true`); `SubtotalLabel`/`GrandTotalLabel`/`NoRowsMessage`; `ColumnWidths` (`<W>`/números); `RowGroups`/`ColumnGroups`; `Cells` (`RowIndex`/`ColumnIndex` + `ColumnSpan`/`RowSpan` se `!= 1` + `Content` = um elemento aninhado) | `RepxWriter.cs:708` |
| `Code` | `Language` (enum `CodeLanguage`); `Source` (no XML em `<![CDATA[…]]>`) | `RepxWriter.cs:764` |
| `Map` | `Basemap`/`DataSetName`/`Latitude`/`Longitude`/`ShapeSet`/`ShapesGeoJson`(CDATA) opcionais; `ShowGraticule`; `ShapeFill`/`ShapeStroke` | `RepxWriter.cs:774` |
| `Gauge` | `Kind`(JSON `gaugeKind`); `Minimum`/`Maximum`/`Value`; `Ranges` (`Start`/`End`/`Color`) | `RepxWriter.cs:789` |
| `DataBar` | `Value`/`Minimum`/`Maximum`/`FillColor` | `RepxWriter.cs:808` |
| `Sparkline` | `Kind`(JSON `sparklineKind`); `Value`; `DataSetName`/`Category` opcionais | `RepxWriter.cs:816` |
| `Indicator` | `Kind`(JSON `indicatorKind`); `Value`; `States` (`Start`/`End`/`Icon`) | `RepxWriter.cs:828` |

> **Round-trip-only (RDL F2).** `Tablix`, `Code`, `Map`, `Gauge`, `DataBar`, `Sparkline`, `Indicator` têm o *renderer* parcial/incremental, mas a serialização **preserva todos os campos** — o objetivo declarado é que um `.repx` autorado em outra ferramenta (SSRS, designer terceiro) sobreviva a um ciclo load/save sem perder configuração (`RepxWriter.cs:702-706`).

## 7.10 Auto-wiring por convenção (fallback)

Os switches em `WriteElement`/`ReadElement` cobrem os tipos hand-wired. Para um **novo** `ReportElement` *all-scalar* (sem braço explícito), o caso `_ =>` delega ao `ElementSerializationRegistry` (`ElementSerializationRegistry.cs:29`), que dá round-trip `.repx` **e** `.repjson` "de graça", sem editar os 4 switches manuais.

### 7.10.1 Convenções

- **Tag/kind**: derivado do nome do tipo, removendo o sufixo `Element` (`ConventionTag`, `…:375`). Ex.: `WatermarkElement` → tag `Watermark`. Colisão de tag entre dois tipos → `InvalidOperationException` na construção do mapa (`…:407`).
- **Nome de campo na wire**: `PascalCase` no XML, `camelCase` no JSON (primeira letra minúscula) — `…:324-325`.
- **Política sparse**: campo não-`Required` igual ao seu valor numa *instância default* é **omitido** (lido de volta como esse default); `null` é sempre omitido (`…:104`, `EmittedMembers`). `Required` = decorado com `[RequiredMemberAttribute]`; ausente na leitura → `FormatException`.
- **Coleções**: `EquatableArray<T>` é serializada como itens repetidos; no XML cada item é um wrapper `<Item>` (`ItemTag`, `…:33`).

### 7.10.2 Shapes de membro suportados (recursivo)

`IsSerializable` (`…:336`) define o que o caminho genérico aceita:

| Shape | Suportado? |
|---|---|
| Escalar: `string`, `bool`, `int`, `long`, `double`, `enum`, `Unit`, `Color` (+ formas `Nullable`) | sim (`IsScalar`, `…:420`) |
| `EquatableArray<T>` onde `T` é escalar ou record serializável | sim |
| Record aninhado (init ou posicional) com ≥1 prop gravável serializável | sim |
| Um `ReportElement` aninhado (ex.: célula contendo elemento) | **não** — mantido no caminho hand-wired |
| `EquatableDictionary`, `List`, arrays, qualquer outro `IEnumerable` | **não** (rejeitado explicitamente, `…:353`) |

### 7.10.3 Critérios de elegibilidade do tipo

`BuildSchema` (`…:299`) retorna `null` (→ defere ao hand-wired) se qualquer condição falhar:

1. O tipo é subclasse **direta** e **concreta** de `ReportElement` (`BaseType == typeof(ReportElement)`); intermediários abstratos são descartados.
2. Tem **construtor sem parâmetros** (positional-only é incremento futuro, `…:305`).
3. Nenhuma propriedade colide com nomes do envelope (`EnvelopeNames`: `Id`, `Name`, `Bounds`, `Visible`, `VisibleExpression`, `Style`, `ConditionalFormats`, `PropertyExpressions`, `Bookmark`, `DocumentMapLabel`, `Action`, `ToggleItemId`, `InitiallyHidden` — `…:37`).
4. Toda propriedade declarada (`DeclaredOnly`, gravável) tem shape serializável.

`TagFor` lança `InvalidOperationException("Unsupported element type: …")` para um tipo não auto-serializável e sem braço (mesma falha que os switches originais davam) — `…:64`. `TryGetType` resolve tag→tipo na leitura (`…:68`); falha → `FormatException("Unknown element tag/kind …")` em ambos os formatos.

> **Consequência prática.** Adicionar um `ReportElement` all-scalar agora exige **zero** edição nos serializers; só o renderer é manual. Ver também o design doc `docs/serialization-auto-wiring-design.md` e a nota de memória *serialization-auto-wiring*. Elementos com sub-elemento aninhado (ex. `TablixCell.Content`) permanecem hand-wired.

## 7.11 `SchemaVersion` e migrações

`SchemaVersion` (`SchemaVersion.cs:4`) é um `readonly record struct (int Major, int Minor)` comparável:

| Membro | Valor |
|---|---|
| `Current` | `1.0` |
| `V1_0` | `1.0` |
| `Parse(text)` | divide em `Major.Minor`; formato inválido → `FormatException` |
| operadores `< > <= >=`, `CompareTo` | comparação `Major` depois `Minor` |
| `ToString()` | `"{Major}.{Minor}"` |

O writer **sempre** grava `SchemaVersion.Current` (`"1.0"`). O atributo/chave ausente na leitura assume `"1.0"` (`RepxReader.cs:26`, `RepJsonReader.cs:20`).

### 7.11.1 `IRepxMigration` (apenas `.repx`)

`RepxSerializer` aceita uma lista opcional de `IRepxMigration` no construtor (`RepxSerializer.cs:17`). Antes de ler, `ApplyMigrations` transforma o `XDocument` **in-place** numa cadeia ordenada: enquanto a versão corrente do documento for menor que `Current`, procura uma migração cujo `From` casa, aplica `Apply(document)`, e avança a versão (`RepxSerializer.cs:59-85`).

```csharp
public interface IRepxMigration
{
    SchemaVersion From { get; }  // versão ANTES de aplicar
    SchemaVersion To { get; }    // versão DEPOIS de aplicar
    void Apply(System.Xml.Linq.XDocument document);
}
```

> Migrações são **exclusivas do `.repx`** (operam sobre `XDocument`). O `.repjson` não tem mecanismo de migração equivalente. Sem migrações registradas, `ApplyMigrations` é no-op.

## 7.12 Exemplo completo do mesmo mini-relatório

Relatório `"Vendas"`, A4 retrato (margens 20mm), com um `Detail` de 25,4mm (1in = 1000 mils) contendo um `Label` "Total:" e um `TextBox` ligado a `Fields.Total`.

### 7.12.1 `.repx`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Report SchemaVersion="1.0" Name="Vendas">
  <PageSetup Orientation="Portrait" Margins="787,787,787,787" Columns="1" ColumnSpacing="0">
    <Paper Name="A4" Width="8268" Height="11693" />
  </PageSetup>
  <Detail Height="1000" Visible="true" CanGrow="false" CanShrink="false">
    <Elements>
      <Label Id="lbl1" Bounds="0,0,1000,250" Visible="true">
        <Style HorizontalAlignment="Left" VerticalAlignment="Top" WordWrap="true" />
        <Text>Total:</Text>
      </Label>
      <TextBox Id="txt1" Bounds="1000,0,2000,250" Visible="true">
        <Style HorizontalAlignment="Right" VerticalAlignment="Top" WordWrap="true" Format="C2" />
        <Expression>Fields.Total</Expression>
      </TextBox>
    </Elements>
  </Detail>
</Report>
```

### 7.12.2 `.repjson`

```json
{
  "schemaVersion": "1.0",
  "name": "Vendas",
  "pageSetup": {
    "paper": { "name": "A4", "width": "8268", "height": "11693" },
    "orientation": "Portrait",
    "margins": "787,787,787,787",
    "columns": 1,
    "columnSpacing": "0"
  },
  "detail": {
    "height": "1000",
    "visible": true,
    "canGrow": false,
    "canShrink": false,
    "pageBreak": "None",
    "elements": [
      {
        "kind": "Label",
        "id": "lbl1",
        "bounds": "0,0,1000,250",
        "visible": true,
        "style": { "horizontalAlignment": "Left", "verticalAlignment": "Top", "wordWrap": true },
        "text": "Total:"
      },
      {
        "kind": "TextBox",
        "id": "txt1",
        "bounds": "1000,0,2000,250",
        "visible": true,
        "style": { "horizontalAlignment": "Right", "verticalAlignment": "Top", "wordWrap": true, "format": "C2" },
        "expression": "Fields.Total",
        "canGrow": false,
        "canShrink": false
      }
    ]
  }
}
```

> Note as duas diferenças sintáticas: (1) o tipo do elemento é a **tag** no XML e o campo **`kind`** no JSON; (2) `pageBreak`/`canGrow`/`canShrink` aparecem **sempre** no JSON do `Detail`/`TextBox`, ao passo que no XML `PageBreak` é sparse (omitido quando `None`). Ambos carregam exatamente a mesma informação e satisfazem `Load(Save(def)).Equals(def)`.
