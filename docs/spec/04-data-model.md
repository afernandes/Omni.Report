# 4. Modelo de dados: fontes, parâmetros, variáveis

Esta seção especifica o **modelo de dados declarativo** do OmniReport: como um relatório declara *de onde* vêm seus dados (`DataSourceDefinition`), *como* esses dados são transformados antes de serem consumidos pelas bandas (campos calculados, filtro, ordenação, relações master-detail), *quais* entradas o relatório recebe de fora (`ReportParameter`) e *quais* valores ele computa internamente (`ReportVariable`). Também especifica a convenção de chaves reservadas do dicionário `Parameters` usada para carregar metadados de conexão/consulta SQL no formato persistido, e como uma `DataSourceDefinition` é resolvida, em runtime, em uma fonte concreta `IReportDataSource`.

Todos os tipos do modelo declarativo vivem em `Reporting.Core` (namespaces `Reporting.Data` e `Reporting.Parameters`); os tipos de runtime (`IReportDataSource`, `DataSourceRegistry`, `ParameterValueResolver`) vivem em `Reporting.DataSources`. O `ReportDefinition` raiz (`Reporting/ReportDefinition.cs`) agrega tudo.

> **Imutabilidade.** Todos os tipos declarativos desta seção são `sealed record` com igualdade estrutural. Coleções usam `EquatableArray<T>`/`EquatableDictionary<K,V>` (de `Reporting.Common`) para preservar a semântica de valor do `record` — dois `ReportDefinition` construídos com as mesmas entradas são `Equals`. Onde um parâmetro de construtor de `record` tem o tipo de coleção `= default`, o valor é o **array/dicionário vazio equatável** (não `null`).

## 4.1. Agregação no `ReportDefinition`

O `ReportDefinition` (`src/Reporting.Core/ReportDefinition.cs`) é o AST canônico. Os três eixos do modelo de dados aparecem como propriedades `init`-only:

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Parameters` | `EquatableArray<ReportParameter>` | `Empty` | Entradas tipadas que o chamador fornece em runtime (§4.5). |
| `DataSources` | `EquatableArray<DataSourceDefinition>` | `Empty` | Declarações de datasets (§4.2). |
| `Variables` | `EquatableArray<ReportVariable>` | `Empty` | Valores computados por expressão, em escopo de linha/relatório/grupo (§4.8). |

`ReportDefinition.cs:23-27`. Note que `DataSources` é uma lista de **declarações** (metadados), não de fontes vivas — a fonte concreta é resolvida por nome em runtime (§4.9).

---

## 4.2. `DataSourceDefinition`

Referência declarativa a um dataset, resolvida em runtime *por nome*. O modelo Core armazena apenas os metadados necessários para conectar a fonte ao motor de layout; a implementação concreta `IReportDataSource` vive em `Reporting.DataSources` (`DataSourceDefinition.cs:5-8`).

```csharp
public sealed record DataSourceDefinition(
    string Name,
    string? DataMember = null,
    EquatableArray<DataField> Fields = default,
    EquatableArray<DataRelation> Relations = default,
    EquatableDictionary<string, string> Parameters = default,
    EquatableArray<CalculatedField> CalculatedFields = default,
    string? FilterExpression = null,
    EquatableArray<SortDescriptor> SortExpressions = default);
```
`DataSourceDefinition.cs:22-30`.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Identificador estável do dataset. Casa com `IReportDataSource.Name` no `DataSourceRegistry`; é a chave de resolução em runtime e o nome qualificado em `Fields.{Name}.{Campo}`. |
| `DataMember` | `string?` | `null` | Sub-membro/relação dentro da fonte (ex.: tabela de um `DataSet` ADO.NET). Metadado opaco para o Core. |
| `Fields` | `EquatableArray<DataField>` | vazio | Esquema declarado (colunas físicas). Pode ser inferido do primeiro registro em runtime se vazio. |
| `Relations` | `EquatableArray<DataRelation>` | vazio | Relacionamentos master-detail entre fontes (§4.4). |
| `Parameters` | `EquatableDictionary<string,string>` | vazio | Dicionário plano `string→string` de metadados de conexão/consulta. Chaves reservadas com prefixo `_` e `param:` (§4.7). **Não confundir** com `ReportDefinition.Parameters` (parâmetros do relatório). |
| `CalculatedFields` | `EquatableArray<CalculatedField>` | vazio | Campos virtuais computados por expressão, por linha (§4.3). |
| `FilterExpression` | `string?` | `null` | Expressão booleana aplicada no nível da fonte, antes de qualquer região consumir linhas. Espelha RDL `<Filters>` no `<DataSet>`. |
| `SortExpressions` | `EquatableArray<SortDescriptor>` | vazio | Ordenação global aplicada antes de qualquer região ver as linhas. Espelha RDL `<SortExpressions>` no `<DataSet>`. |

### Ordem de aplicação das transformações (runtime)

O paginador aplica filtro e ordenação em camadas, nesta ordem (`ReportPaginator.cs:480-491`):

1. **Nível de fonte** — `DataSourceDefinition.FilterExpression` / `SortExpressions`.
2. **Nível de região de dados (Detail)** — `DetailBand.FilterExpression` / `SortExpressions`.
3. **Nível de grupo** — em torno da transição de grupo (ordena instâncias de grupo).

Filtro e ordenação rodam **por linha dentro do contexto de expressão do relatório**, portanto enxergam `Parameters` e `Variables`. Os `CalculatedFields` da fonte primária são avaliados *durante a iteração de linha* (§4.3), depois do filtro/sort de fonte e detalhe.

### `DataField`

```csharp
public sealed record DataField(string Name, Type? FieldType = null, string? DisplayName = null);
```
`DataSourceDefinition.cs:32`.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Nome do campo; acessível como `Fields.{Name}` nas expressões. |
| `FieldType` | `Type?` | `null` | Tipo CLR do campo. `null` = não declarado (string/inferido). |
| `DisplayName` | `string?` | `null` | Rótulo amigável opcional. |

---

## 4.3. `CalculatedField`

Campo virtual cujo valor é computado por linha a partir de uma expressão. Equivalente a um RDL `<Field>` com filho `<Value>` (`CalculatedField.cs:3-6`).

```csharp
public sealed record CalculatedField(string Name, string Expression, Type? ResultType = null);
```
`CalculatedField.cs:23`.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Identificador; aparece como `Fields.{Name}` em expressões — indistinguível de uma coluna física. |
| `Expression` | `string` | (obrigatório) | Template/expressão reavaliado por linha. Pode referenciar outros `Fields.*`, `Parameters.*`, `Variables.*` e funções de agregação. |
| `ResultType` | `Type?` | `null` | Tipo CLR do resultado, dirige coerção. `null` ⇒ default `string` (resultado deixado como está) — `CalculatedField.cs:21-22`. |

**Semântica em runtime** (`ReportPaginator.cs:630-642`): para cada linha da fonte primária com `CalculatedFields.Count > 0`, cada expressão é avaliada com a linha em escopo e o resultado é injetado em `Fields` sob o nome do campo. A avaliação é **sequencial**: campos calculados anteriores ficam visíveis aos posteriores. Uma falha de avaliação é capturada e produz `value = null` (`catch { value = null; }`) — não aborta a paginação.

> **Guarda de recursão.** A documentação do tipo (`CalculatedField.cs:14-16`) especifica que um campo calculado que referencia a si mesmo (ou forma ciclo) lança em tempo de avaliação com stack trace claro; o contexto de expressão rastreia a cadeia em progresso. (Garantia documentada no contrato do tipo; a injeção no paginador em si captura exceções por linha.)

---

## 4.4. `DataRelation`

Relacionamento master-detail entre duas fontes (`DataSourceDefinition.cs:34-40`).

```csharp
public sealed record DataRelation(
    string Name,
    string ParentSource,
    string ParentField,
    string ChildSource,
    string ChildField);
```

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Nome da relação; usado para resolver sub-detalhes por nome (ex.: `"PedidosDeCliente"`). |
| `ParentSource` | `string` | (obrigatório) | Nome da fonte master. |
| `ParentField` | `string` | (obrigatório) | Campo de junção no master. |
| `ChildSource` | `string` | (obrigatório) | Nome da fonte detail. |
| `ChildField` | `string` | (obrigatório) | Campo de junção no detail. |

O paginador usa a `DataSourceDefinition` do pai para resolver relações por nome ao emitir sub-detalhes (`ReportPaginator.cs:473-478`): `"PedidosDeCliente" → ChildSource=Pedidos, ChildField=cliente_id`. A junção em runtime é provida por `MasterDetailDataSource` em `Reporting.DataSources`.

---

## 4.5. `ReportParameter`

Parâmetro de relatório fortemente tipado. Sua entrada solicitada é coagida para `ValueType` antes da vinculação (`ReportParameter.cs:5-6`).

```csharp
public sealed record ReportParameter(
    string Name,
    Type ValueType,
    string? Prompt = null,
    object? DefaultValue = null,
    bool AllowMultiple = false,
    bool Required = true,
    ParameterAvailableValues? AvailableValues = null,
    bool Nullable = false,
    bool AllowBlank = false,
    bool Hidden = false);
```
`ReportParameter.cs:7-17`.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Identificador; acessível como `Parameters.{Name}` nas expressões. |
| `ValueType` | `Type` | (obrigatório) | Tipo CLR para o qual a entrada solicitada é coagida antes do bind. |
| `Prompt` | `string?` | `null` | Texto de prompt exibido ao usuário (host). |
| `DefaultValue` | `object?` | `null` | Valor usado quando o chamador não fornece o parâmetro (§4.6). |
| `AllowMultiple` | `bool` | `false` | Parâmetro multivalorado (lista de valores). |
| `Required` | `bool` | `true` | Indica se um valor é obrigatório. |
| `AvailableValues` | `ParameterAvailableValues?` | `null` | Domínio de valores permitidos — lista estática e/ou query (§4.6.1). |
| `Nullable` | `bool` | `false` | Permite valor `null`. |
| `AllowBlank` | `bool` | `false` | Permite string vazia. |
| `Hidden` | `bool` | `false` | Oculta o parâmetro da UI de prompt do host. |

> **Nota de fidelidade ao código.** O briefing menciona `DefaultValueExpression`. **Esse membro não existe** no `ReportParameter` atual — o default é o valor literal `DefaultValue` (`object?`). Não há campo de expressão de default. Da mesma forma, não há um `ParameterValue` separado para "valor corrente": o valor corrente vem do dicionário `PaginationRequest.Parameters` em runtime (§4.6).

### 4.6. Resolução do valor do parâmetro (runtime)

Em `ApplyParameters` (`ReportPaginator.cs:983-990`), para cada `ReportParameter` da definição:

```csharp
var value = request.Parameters.TryGetValue(p.Name, out var v) ? v : p.DefaultValue;
ctx.ParametersStore.Set(p.Name, value);
```

Ou seja: o valor solicitado em `PaginationRequest.Parameters` (`IReadOnlyDictionary<string, object?>`, default = dicionário vazio — `PaginationRequest.cs:15-17`) tem precedência; ausente, cai para `ReportParameter.DefaultValue`. O valor resultante é publicado no `ParametersStore` do contexto de expressão e fica acessível como `Parameters.{Name}`.

### 4.6.1. `ParameterAvailableValues`

Domínio de valores permitidos — uma lista estática e/ou uma query sobre um dataset, para o host renderizar um dropdown validado em vez de texto livre (SSRS "Available Values"). Quando ambos estão presentes, **as entradas estáticas vêm primeiro** (`ParameterAvailableValues` doc, `ReportParameter.cs:19-22`).

```csharp
public sealed record ParameterAvailableValues
{
    public EquatableArray<ParameterValue> Values { get; init; } = EquatableArray<ParameterValue>.Empty;
    public string? DataSet { get; init; }
    public string? ValueField { get; init; }
    public string? LabelField { get; init; }
    public bool IsQuery => !string.IsNullOrWhiteSpace(DataSet);
    public static ParameterAvailableValues FromList(params ParameterValue[] values);
    public static ParameterAvailableValues FromQuery(string dataSet, string valueField, string? labelField = null);
}
```
`ReportParameter.cs:23-48`.

| Membro | Tipo | Default | Semântica |
|---|---|---|---|
| `Values` | `EquatableArray<ParameterValue>` | `Empty` | Valores estáticos permitidos, em ordem de exibição. |
| `DataSet` | `string?` | `null` | Dataset que fornece os valores (query-driven). `null`/branco = só lista estática. |
| `ValueField` | `string?` | `null` | Campo que fornece o valor vinculado, quando `DataSet` está setado. |
| `LabelField` | `string?` | `null` | Campo do rótulo de exibição; **fallback para o valor** quando ausente/célula vazia. |
| `IsQuery` | `bool` (calc.) | — | `true` quando `DataSet` é não-branco. |

Fábricas: `FromList(params ParameterValue[])` cria um domínio estático; `FromQuery(dataSet, valueField, labelField?)` cria um domínio query-driven projetando linhas distintas para `(valueField, labelField ?? value)`.

### 4.6.2. `ParameterValue`

Um valor permitido: o `Value` vinculado (forma string, coagido para o `ReportParameter.ValueType` no bind) e um `Label` de exibição opcional (default = o valor quando `null`) — `ReportParameter.cs:50-53`.

```csharp
public sealed record ParameterValue(string Value, string? Label = null);
```

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Value` | `string` | (obrigatório) | Valor vinculado, em forma string; coagido para `ValueType` no bind. |
| `Label` | `string?` | `null` | Rótulo de exibição; `null` ⇒ usa `Value`. |

### 4.6.3. `ParameterValueResolver` (materialização do domínio)

`ParameterValueResolver.ResolveAsync(available, sources, ct)` (estático, `Reporting.DataSources/ParameterValueResolver.cs:12-61`) materializa um `ParameterAvailableValues` numa lista ordenada e **de-duplicada** de `ParameterValue` que o host vincula a um dropdown validado. Regras:

- **Estáticos primeiro**, na ordem de `Values`. De-dup por `Value` com `StringComparer.Ordinal` (`HashSet<string>`).
- Se `IsQuery` **e** `ValueField` não-branco **e** o `DataSet` existe no `DataSourceRegistry` (`sources.TryGet`), itera os registros via `ds.ReadAsync(ct)`:
  - lê `record[ValueField]`; `null` é pulado.
  - converte para string com `CultureInfo.InvariantCulture`.
  - de-dup por valor (primeiro-visto vence; ordem de primeira aparição).
  - se `LabelField` não-branco, lê o rótulo; **célula vazia/null ⇒ rótulo deixado `null`** (consumidor cai para o valor), honrando o contrato de fallback.
- Retorna **lista vazia** quando o domínio é vazio ou o dataset referenciado é desconhecido.

---

## 4.7. Convenção de chaves do dicionário `Parameters` do DataSource

O dicionário plano `DataSourceDefinition.Parameters` (`EquatableDictionary<string,string>`) carrega, no formato persistido (`.repx`/`.repjson`), os metadados de conexão e de consulta SQL — **sem alterar o schema Core**. As chaves reservadas usam prefixo `_` (metadados) e o prefixo `param:` (parâmetros SQL). Isso evita colisão com nomes de parâmetro SQL fornecidos pelo usuário, que usam prefixos `@`/`$`/`:`.

Convenção definida em dois lugares espelhados — o catálogo do designer (`DataSourceCatalog.cs:295-302`, classe `RepxKeys`) e o leitor/escritor RDL:

| Chave | Constante | Tipo do valor | Semântica |
|---|---|---|---|
| `_kind` | `RepxKeys.Kind` | `DataConnectionKind` (string) | Provedor/tipo de conexão. **Omitido quando `InMemory`** (default). Re-parseado com `Enum.TryParse(..., ignoreCase: true)`. |
| `_connection` | `RepxKeys.Conn` | `string` | Connection string. Gravado só quando não-vazio. |
| `_sql` | `RepxKeys.Sql` | `string` | Texto SQL da consulta (ou nome de stored proc). Gravado só quando não-vazio. |
| `_storedProc` | `RepxKeys.StoredProc` | `"true"` | Presente e `"true"` ⇒ `_sql` é uma stored procedure (RDL `<CommandType>StoredProcedure</CommandType>`). |
| `_timeout` | `RepxKeys.Timeout` | `int` (invariant) | Command timeout em segundos. |
| `param:{sqlName}` | `RepxKeys.SqlParamPrefix` | `"{reportParameter}|{literal}"` | Um parâmetro SQL. Valor codificado: lado esquerdo = nome de um `ReportParameter` a vincular; lado direito = literal. Qualquer lado pode ser vazio. Split por `'|'` em 2 partes; parte vazia ⇒ `null`. |

`DataSourceCatalog.ToDefinition()` (`DataSourceCatalog.cs:308-343`) escreve essas chaves; `FromDefinition()` (`:348-389`) as recupera no round-trip. Exemplo de dicionário persistido:

```text
_kind        = "SqlServer"
_connection  = "Server=.;Database=Vendas;Trusted_Connection=True"
_sql         = "SELECT * FROM Pedidos WHERE data >= @dataInicio"
_timeout     = "30"
param:@dataInicio = "DataInicio|"          # vincula ao ReportParameter "DataInicio"
param:@status     = "|ativo"               # literal "ativo"
```

### Round-trip RDL

No export RDL (`RdlWriter.WriteDataSet`, `RdlWriter.cs:265-320`):

- `_sql` → `<Query><CommandText>` (SQL bruto, não passa por reverse de expressão).
- `_storedProc == "true"` → `<CommandType>StoredProcedure</CommandType>`.
- cada `param:{sqlName}` → `<QueryParameter Name="{sqlName}"><Value>…</Value>`. Se há `reportParameter`, o valor vira `=Parameters!P.Value` (via `RdlExpressionReverse.ToRdl("Parameters." + repParam)`); senão usa o literal.
- **Aviso (warning):** um literal que comece com `'='` não round-trippa (o RDL o trataria como expressão) — emite warning em vez de corromper.
- **Aviso:** `_kind`/`_connection`/`_timeout` não têm lar no `<DataSet>` RDL e o importador não lê `<DataSources>` — emite warning de perda no round-trip. Chaves com `_` (exceto `_sql`/`_storedProc`) **nunca** são emitidas como `<QueryParameter>` (`RdlWriter.cs:269-271`).

---

## 4.8. `ReportVariable` e `VariableScope`

Variável computada, avaliada uma vez por linha (ou uma vez por relatório/grupo, conforme escopo) — `ReportParameter.cs:55-60`.

```csharp
public sealed record ReportVariable(
    string Name,
    string Expression,
    VariableScope Scope = VariableScope.Row,
    object? InitialValue = null);
```

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Identificador; acessível como `Variables.{Name}`. |
| `Expression` | `string` | (obrigatório) | Expressão avaliada conforme o escopo. |
| `Scope` | `VariableScope` | `Row` | Frequência/escopo de reavaliação (abaixo). |
| `InitialValue` | `object?` | `null` | Valor inicial antes da primeira avaliação. |

### `enum VariableScope`

`ReportParameter.cs:62-72`.

| Valor | Semântica |
|---|---|
| `Row` | Reavaliada para cada linha da banda de detalhe. |
| `Report` | Avaliada uma vez, no início do relatório. |
| `Group` | Avaliada uma vez por instância de grupo. |

`ReportVariable` aparece em dois lugares: como `ReportDefinition.Variables` (variáveis globais do relatório, `ReportDefinition.cs:27`) e em `Bands.cs:148` (variáveis em escopo de banda).

---

## 4.9. Resolução de datasets em runtime

A `DataSourceDefinition` é puramente declarativa; a fonte viva é uma `IReportDataSource` resolvida **por nome** via `DataSourceRegistry`.

### `IReportDataSource`

Fonte de registros em streaming, produzida assincronamente para suportar datasets grandes e provedores de banco (`IReportDataSource.cs:3-15`):

```csharp
public interface IReportDataSource
{
    string Name { get; }                          // casa com DataSourceDefinition.Name
    IReportRecordSchema Schema { get; }           // pode ser inferido do primeiro registro
    IAsyncEnumerable<IReportRecord> ReadAsync(CancellationToken cancellationToken = default);
}
```

Cada `IReportRecord` (`IReportRecord.cs:7-20`) expõe acesso por nome (`record[string]`, retorna `null` se desconhecido/vazio) e por ordinal (`record[int]`), além de `ToKeyValuePairs()` (snapshot empurrado para o contexto de expressão). O esquema `IReportRecordSchema` lista `ReportField(string Name, Type Type)` e resolve `IndexOf(name)` (ou `-1`). A implementação padrão `ReportRecordSchema` indexa nomes com `StringComparer.OrdinalIgnoreCase` (`ReportRecordSchema.cs:10-12`).

### `DataSourceRegistry`

Registry name-keyed, resolvido em runtime pelo motor de layout para vincular uma `DataSourceDefinition` a uma `IReportDataSource` concreta (`DataSourceRegistry.cs:5-30`):

```csharp
public sealed class DataSourceRegistry
{
    public void Register(IReportDataSource source);            // chave = source.Name
    public bool TryGet(string name, out IReportDataSource source);
    public IReportDataSource Get(string name);                 // lança se ausente
    public bool Remove(string name);
    public IEnumerable<string> Names { get; }
}
```

- Indexação interna: `ConcurrentDictionary<string, IReportDataSource>` com `StringComparer.OrdinalIgnoreCase` (`DataSourceRegistry.cs:11`) — **a resolução por nome é case-insensitive**.
- `Register` usa a chave `source.Name` e **sobrescreve** um registro existente de mesmo nome.
- `Get(name)` ausente ⇒ `InvalidOperationException($"No data source named '{name}' is registered.")`.

### Vínculo na paginação

`PaginationRequest` (`PaginationRequest.cs:7-24`) é a entrada da paginação:

| Campo | Tipo | Semântica |
|---|---|---|
| `Definition` | `ReportDefinition` (required) | O AST a paginar. |
| `DataSources` | `DataSourceRegistry` (required) | Fontes vivas keyed por `DataSourceDefinition.Name`. |
| `Parameters` | `IReadOnlyDictionary<string, object?>` | Valores de parâmetro fornecidos; ausentes caem para `ReportParameter.DefaultValue`. Default = dicionário vazio. |
| `PrimaryDataSource` | `string?` | Dataset que dirige a banda de detalhe. `null` ⇒ a **primeira fonte registrada** é usada (`PaginationRequest.cs:23-24`). |

Resolução da fonte primária: o paginador calcula `primarySourceName` (via `ResolvePrimaryName`) e busca a `DataSourceDefinition` correspondente por nome com `StringComparison.Ordinal` (`ReportPaginator.cs:472-478`). Cada dataset materializado é exposto ao contexto de expressão via `ctx.RegisterDataset(sourceName, sourceRows)` para `Lookup`/`LookupSet` cross-dataset estilo SSRS (`ReportPaginator.cs:463-467`).

> **Determinismo/offline.** O motor Core nunca busca dados de rede por conta própria: a `IReportDataSource` concreta (ADO.NET, SQL Server, SQLite, Json, Xml, WebService, FileSystem, Enumerable, DataTable, etc., em `Reporting.DataSources.*`) é fornecida pelo host e registrada no `DataSourceRegistry` antes de paginar.

---

## 4.10. Resumo de invariantes

1. `DataSourceDefinition.Name` é a **chave de resolução** em runtime e deve casar (case-insensitive no registry) com um `IReportDataSource.Name` registrado.
2. As chaves `_kind`/`_connection`/`_sql`/`_storedProc`/`_timeout`/`param:*` do dicionário `Parameters` são **reservadas**; nomes de parâmetro SQL do usuário usam prefixos `@`/`$`/`:` para não colidir.
3. O domínio de um parâmetro (`ParameterAvailableValues`): estáticos primeiro, depois query-driven distintos por valor (de-dup ordinal, primeiro-visto); rótulo cai para o valor quando ausente.
4. Precedência do valor de parâmetro: `PaginationRequest.Parameters[Name]` > `ReportParameter.DefaultValue`. Não existe `DefaultValueExpression`.
5. Transformações de fonte aplicadas em ordem: filtro/sort de fonte → filtro/sort de detalhe → sort de grupo; `CalculatedFields` injetados por linha (sequenciais, tolerantes a falha por `try/catch`).
6. `VariableScope` controla a frequência de reavaliação: `Row` (por linha), `Report` (uma vez), `Group` (por instância de grupo).
