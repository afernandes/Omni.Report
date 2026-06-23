# 6. Linguagem de expressão OmniReport

Esta seção especifica o *dialeto de expressões* do OmniReport — a linguagem usada em propriedades calculadas (`Style.Visibility`, `Value` de TextBox, campos calculados, chaves de grupo, filtros etc.) e em *templates* de texto interpolado. O dialeto é processado pelo projeto `Reporting.Expressions` e construído sobre o motor [NCalc](https://github.com/ncalc/ncalc) (pacote `NCalcSync`, ver `Reporting.Expressions.csproj:15`), sobre o qual o OmniReport sobrepõe: um *rewriter* léxico para caminhos pontuados, um conjunto de funções (agregados, lookup, posicionais, escalares estilo SSRS/VB), e um contexto de runtime (`IReportExpressionContext`) que expõe campos, parâmetros, variáveis, globais e *scope* de agregação.

> **Contraste de uma linha com o RDL/SSRS.** No RDL a sintaxe é VB-like com `=` inicial e `!` para membros: `=Fields!Total.Value`, `=Globals!PageNumber`, `a & b`, `x Like "A*"`. No OmniReport a sintaxe é *caminho pontuado sem `=`*: `Fields.Total`, `PageNumber`, `Concat(a, b)`, `Like(x, "A*")`. A tradução RDL→OmniReport é feita na desserialização por `RdlExpression.Convert` (`Reporting.Serialization/Internal/RdlExpression.cs`), fora do runtime de expressão; o runtime nunca vê `!` nem `=`.

---

## 6.1. Pipeline de avaliação

Uma expressão é uma `string`. Avaliá-la passa por três estágios (`ExpressionEvaluator.Evaluate`, `ExpressionEvaluator.cs:26`):

1. **Rewrite léxico** (`ExpressionRewriter.Rewrite`) — converte caminhos pontuados reconhecidos (`Fields.X`, `Parameters.X`, `Variables.X`, `Page.X`, `ReportItems.X`) na forma de parâmetro entre colchetes do NCalc (`[Fields.X]`), e achata `Code.Metodo(` em `Code_Metodo(`. Strings literais e trechos já entre colchetes são preservados.
2. **Parse + cache** (`ExpressionCompiler.Compile`) — o texto reescrito é parseado uma vez via `LogicalExpressionFactory.Create` e a AST resultante é cacheada (`ConcurrentDictionary<string, LogicalExpression>`, chave por texto reescrito, comparador `Ordinal`). Reavaliações por linha pagam só o custo de *evaluate*. Falha de parse lança `ExpressionParseException`.
3. **Bind + evaluate** — uma instância fresca de `Expression` (ligada à AST cacheada) recebe os *handlers* `EvaluateParameter` (resolve identificadores/escopos) e `EvaluateFunction` (resolve funções), depois `Evaluate()`. Exceções de runtime (exceto parse) são embrulhadas em `ExpressionEvaluationException` carregando o texto original.

### 6.1.1. Opções do compilador

`ExpressionCompiler.DefaultOptions` (`ExpressionCompiler.cs:24`) define o comportamento de avaliação:

| Opção NCalc | Estado | Efeito |
|---|---|---|
| `IgnoreCaseAtBuiltInFunctions` | **ligada** | Nomes de função *built-in* do NCalc são *case-insensitive*. (As funções do OmniReport também são *case-insensitive* por implementação própria — ver 6.5.) |
| `DecimalAsDefault` | **ligada** | Literais numéricos e aritmética preferem `decimal` a `double` (matemática monetária precisa). |
| `StringConcat` | **DESLIGADA (intencional)** | O operador `+` permanece aritmético. Se ligada, `+` viraria concatenação de strings e corromperia silenciosamente somas numéricas. Para misturar texto e valores use `Concat(...)` ou a sintaxe de *template* `"... {expr:fmt} ..."`. |

A AST é parseada com `ExpressionOptions.None`; as `DefaultOptions` são aplicadas na `Expression` final. A *culture* da `Expression` é sempre `InvariantCulture` — **a linguagem de expressão é invariante**: o literal `'1.5'` significa `1.5`, nunca `15` (ponto é separador decimal). A *culture* do relatório (`context.Culture`) afeta apenas a *saída* formatada (templates, `Format`, conversões `CStr`/`FormatXxx`), não o parsing de literais.

`ExpressionCompiler` também expõe `Invalidate(expr)` e `Clear()` para cenários de *hot-reload*/testes.

---

## 6.2. Referências de dados (identificadores e escopos)

O *rewriter* (`ExpressionRewriter.cs`) reconhece exatamente cinco prefixos de escopo pontuado:

```
Scopes = ["Fields", "Parameters", "Variables", "Page", "ReportItems"]
```

Um token `Escopo.membro` (com `membro` possivelmente uma cadeia `a.b.c`) é reescrito para `[Escopo.membro]`. No *bind* (`ExpressionEvaluator.Bind`, `ExpressionEvaluator.cs:58`) o handler `EvaluateParameter` divide o nome em `scope` + `member`, e resolve:

| Escopo | Sintaxe | Resolve para | Fonte |
|---|---|---|---|
| `Fields` | `Fields.Nome` | Valor do campo `Nome` da linha corrente; com *fallback* master-detail (ver 6.2.2) | `context.TryResolveUnqualifiedField` |
| `Fields` (qualificado) | `Fields.Fonte.Nome` | Quando `Fonte` é uma *source* registrada, valor de `Nome` na linha corrente daquela source; senão cai para cadeia de membros (ver 6.2.3) | `context.GetSource(Fonte)` |
| `Parameters` | `Parameters.P` | Valor do parâmetro `P` | `context.Parameters["P"]` |
| `Variables` | `Variables.V` | Valor da variável `V` | `context.Variables["V"]` |
| `Page` | `Page.Number` / `Page.PageNumber` | `context.PageNumber` | — |
| `Page` | `Page.Total` / `Page.TotalPages` | `context.TotalPages` | — |
| `ReportItems` | `ReportItems.Caixa` | Valor que outro TextBox nomeado `Caixa` renderizou | `context.GetReportItem` |

Um escopo não reconhecido (ou membro não reconhecido de `Page`) resolve para `null`.

### 6.2.1. Identificadores nus (globais e campos não qualificados)

Identificadores *sem* ponto são tratados como nomes bem-conhecidos antes de qualquer *fallback* de campo (`ExpressionEvaluator.cs:115`). A ordem de tentativa é:

| Identificador nu | Resolve para | Tipo CLR | Notas |
|---|---|---|---|
| `Now` | `context.Now` | `DateTime` | "relógio de parede" da geração; RDL `Globals!ExecutionTime` mapeia para cá |
| `Today` | `context.Today` | `DateTime` | parte de data de `Now` (`Now.Date`) |
| `UserName` | `context.UserName` | `string` | identificador do usuário; default `Environment.UserName ?? "anonymous"` |
| `GroupKey` | `context.GroupKey` | `object?` | chave do grupo ativo mais interno; `null` fora de grupo |
| `PageNumber` | `context.PageNumber` | `int` | página corrente, base 1 |
| `TotalPages` | `context.TotalPages` | `int` | total de páginas (resolvido no 2º passo de paginação) |
| `ReportName` | `context.ReportName` | `string` | nome do relatório (RDL `Globals!ReportName`); vazio se não definido |
| `Language` | `context.Culture.Name` | `string` | nome da *culture* ativa (ex. `"pt-BR"`); RDL `Globals!Language`/`User!Language` colapsam aqui |

Se o identificador não for nenhum acima, segue a cadeia de *fallback* (na ordem):

1. **Campo** via `context.TryResolveUnqualifiedField(name, …)` — live `Fields` primeiro, depois cada *source* registrada (ver 6.2.2);
2. **Variável** (`context.Variables.Contains(name)`);
3. **Parâmetro** (`context.Parameters.Contains(name)`);
4. caso contrário, `null`.

> **Globais via prefixo.** As mesmas globais paginais também são alcançáveis por `Page.Number`/`Page.Total` (6.2). Não há um escopo `Globals.` no *rewriter*; o RDL `Globals!X` é desfeito para a forma nua/`Page.` na desserialização.

### 6.2.2. Fallback master-detail (campos não qualificados)

`TryResolveUnqualifiedField` (`ReportExpressionContext.cs:210`) implementa a UX de master-detail: ao iterar pai→filho, o `Fields` *live* é a linha-filho, mas o usuário frequentemente referencia campos do pai sem qualificar. A busca:

1. `Fields` *live* primeiro (escopo de iteração — match mais específico);
2. depois cada *source* registrada (`_sources`), em ordem de registro, retornando o primeiro que **contém** o campo.

Retorna `true`/valor no primeiro match, ou `false`/`null`. Isso replica o comportamento implícito de Crystal/SSRS/FastReport na camada de expressão, beneficiando relatórios *code-first* e de *designer*.

A mesma lógica é aplicada à forma `Fields.X` (`ExpressionEvaluator.cs:94`).

### 6.2.3. Caminhos de membro aninhados

Quando o `member` após o escopo contém mais pontos (`Fields.Cliente.Endereco.Cidade`), há duas semânticas, decididas em `Bind`:

- **`Fields.Fonte.Tail`** — se `Fonte` é uma *source* registrada (`context.GetSource(Fonte) != null`) **e** há `Tail`, resolve `Tail` (cabeça + resto) contra a linha corrente daquela source (`ExpressionEvaluator.cs:72`).
- **Caso contrário** — o valor base é resolvido (campo/param/var) e o `tail` restante é caminhado por reflexão via `MemberPathResolver.Resolve` (`MemberPathResolver.cs`), que acessa propriedades/campos públicos de instância (cache de *accessors* compilados por `(Type, nome)`). Isso preserva `Fields.Cliente.Nome` como cadeia de membros sobre um objeto aninhado quando *não* existe source chamada `Cliente`.

`MemberPathResolver` retorna `null` em qualquer salto irresolúvel (propriedade/campo inexistente ou `root` nulo).

---

## 6.3. Operadores

Os operadores são os do NCalc (não redefinidos pelo OmniReport). Resumo do dialeto efetivo:

| Categoria | Operadores | Semântica |
|---|---|---|
| Aritméticos | `+` `-` `*` `/` `%` | Aritmética numérica. `+` é **sempre** soma (não concatena — `StringConcat` desligada). Com `DecimalAsDefault`, literais/contas preferem `decimal`. |
| Comparação | `==` `=` `!=` `<>` `<` `<=` `>` `>=` | Igualdade e ordem (NCalc aceita `=`/`==` e `!=`/`<>`). |
| Lógicos | `&&` `\|\|` `and` `or` `not` `!` | Booleanos. (`and`/`or` *case-insensitive*.) |
| Bit a bit | `&` `\|` `^` `<<` `>>` | Operadores bit a bit do NCalc. **Atenção:** `&` é *bitwise AND*, **não** concatenação de strings (diferente do VB). Para concatenar use `Concat(...)`. |
| Ternário | `cond ? a : b` | Condicional (alternativa a `IIf`). |
| Agrupamento | `( )` | Precedência. |
| Indexação/chamada | `f(args)` | Chamada de função (ver 6.5). |

Literais: números (`123`, `1.5`), strings entre aspas simples (`'texto'`) ou duplas, booleanos `true`/`false`, datas via funções (`CDate('2026-01-31')`). O *rewriter* preserva strings literais e não as toca.

---

## 6.4. Templates de texto interpolado

`TemplateRenderer` (`TemplateRenderer.cs`) renderiza cadeias com *placeholders* `{ expr }` ou `{ expr : formato }`:

```csharp
var tr = new TemplateRenderer(evaluator);
tr.Render("Total: {Fields.Total:C} (página {PageNumber}/{TotalPages})", ctx);
// → "Total: R$ 1.234,50 (página 1/3)"   (culture pt-BR)
```

Regras:

| Construção | Significado |
|---|---|
| `{ expr }` | Avalia `expr` e formata sem *format spec* (ver `ValueFormatter`). |
| `{ expr : fmt }` | Avalia `expr` e formata com `fmt` (string de formato .NET padrão), honrando `context.Culture`. O separador `:` é detectado no nível superior (fora de parênteses/colchetes/strings) por `FindFormatSeparator`. |
| `{{` / `}}` | Chave literal `{` / `}`. |
| `}` solto | `FormatException` ("Unexpected '}'…"). |
| `{` sem fechamento | `FormatException` ("Unterminated…"). |

`FindClosing` é *brace-balanced* e *quote-aware* (ignora `{`/`}` dentro de strings `'…'`/`"…"` da expressão), permitindo *placeholders* contendo chamadas e literais.

Auxiliares estáticos:

- `TemplateRenderer.HasPlaceholders(s)` — `true` se há ao menos um `{` não escapado.
- `TemplateRenderer.TryGetSingleExpression(template, out expr)` — `true` quando o template é **um único** *placeholder* cobrindo toda a string (já *trimada*) **sem** `:fmt` inline (ex. `"{Fields.preco}"`). Permite ao chamador avaliar o valor *tipado* e aplicar o `Format` do **próprio elemento** (estilo SSRS: um TextBox ligado a um único valor é formatado por sua propriedade `Format`). Retorna `false` para templates mistos, múltiplos *placeholders*, ou *placeholder* com `:fmt` inline (que vence).

### 6.4.1. Formatação de valores

`ValueFormatter.Format(value, format, culture)` (`ValueFormatter.cs`):

- `value == null` → `""` (string vazia);
- `format` vazio/nulo → `IFormattable.ToString(null, culture)` se aplicável, senão `Convert.ToString(value, culture)`;
- `format` presente e `value is IFormattable` → `value.ToString(format, culture)`;
- *fallback* → `string.Format(culture, "{0:" + format + "}", value)`.

---

## 6.5. Funções

O handler `EvaluateFunction` (`ExpressionEvaluator.cs:135`) despacha por *categorias*, **nesta ordem** — a primeira que reconhece o nome vence:

1. **Agregados** (`TryEvaluateAggregate`) — `Sum`, `Avg`/`Average`, `Count`, `Min`, `Max`, `First`, `Last`, `CountDistinct`, `Var`, `VarP`, `StDev`, `StDevP`, `RunningTotal`, e o especial `RunningValue`.
2. **Lookup** (`TryEvaluateLookup`) — `Lookup`, `LookupSet`, `MultiLookup`.
3. **Posicionais** (`TryEvaluatePositional`) — `RowNumber`, `CountRows`, `Previous`.
4. **Built-ins** (`TryEvaluateBuiltin`) — `Today`, `Now`, `PageNumber`, `TotalPages`, `Coalesce`, `IsNull`, `Format`.
5. **Escalares estilo SSRS/VB** (`TryEvaluateScalarFunction`) — conjunto extenso (6.5.5).
6. **`Code.Metodo(...)`** — roteado ao `CodeFunctionResolver` opt-in (6.5.6), via nome achatado `Code_Metodo`.

Se nenhuma categoria reconhecer o nome, o NCalc tenta suas próprias *built-ins* (`Abs`, `Round`, `Sqrt`, `Sign`, `Pow`, `Min`/`Max` numéricas, etc.) — estas **não** são reimplementadas pelo OmniReport. Nomes não resolvidos por ninguém produzem erro de avaliação do NCalc.

> **Avaliação preguiçosa de argumentos.** Os handlers recebem `FunctionEventArgs args`; `args.Parameters.Evaluate(i)` avalia o i-ésimo argumento sob demanda, e `args.Parameters[i]` dá o nó de AST **bruto** (não avaliado). Funções que precisam do *texto* de uma sub-expressão (agregados, lookup, posicionais) extraem-no com `ExtractRawExpression` (6.5.7), pois esse texto deve ser reavaliado por-linha em outro escopo, não no escopo do chamador.

### 6.5.1. Agregados

`AggregateNames` (conjunto *case-insensitive*, `ExpressionEvaluator.cs:726`):

```
Sum, Avg, Average, Count, Min, Max, RunningTotal, First, Last,
CountDistinct, Var, VarP, StDev, StDevP
```

Assinatura geral: **`Agg(expr [, scope])`**.

- O **1º argumento** é a *expressão por-linha*, extraída como **texto bruto** (`ExtractRawExpression`) — é reavaliada uma vez por linha do escopo, não no escopo do chamador.
- O **2º argumento** (opcional) é o *scope*, avaliado e mapeado por `ParseScope` (6.5.8). Default `AggregateScope.Report`.
- Sem argumentos → a função "falha" (retorna `false` do `Try`), caindo para a próxima categoria.

A computação é delegada a `context.EvaluateAggregate(function, expressionText, scope)` → `AggregateCalculator.Calculate` (`AggregateCalculator.cs`). Semântica por função:

| Função | Semântica | Tipo de retorno | Vazio |
|---|---|---|---|
| `Count` | Conta linhas onde a expressão é avaliável (cada linha enumerada conta; veja nota) | `int` | `0` |
| `Sum`, `RunningTotal` | Soma via `decimal` (cada valor por `ToDecimal`, `null`→0) | `decimal` | `0m` |
| `Avg`, `Average` | `Sum / count` em `decimal` | `decimal` | `0m` |
| `Min` | Menor valor não-nulo (ver `Compare`) | `object?` | `null` |
| `Max` | Maior valor não-nulo | `object?` | `null` |
| `First` | Primeiro valor por-linha (`FirstOrDefault`) | `object?` | `null` |
| `Last` | Último valor (`LastOrDefault`) | `object?` | `null` |
| `CountDistinct` | Nº de valores distintos não-nulos; numéricos normalizados a `decimal` (int 1 = long 1 = 1.0 = 1m) | `int` | `0` |
| `Var` | Variância **amostral** (÷ n−1, correção de Bessel); 0 se n<2 | `decimal` | `0m` |
| `VarP` | Variância **populacional** (÷ n) | `decimal` | `0m` |
| `StDev` | Desvio-padrão amostral = √Var (via `double`, depois volta a `decimal`) | `decimal` | `0m` |
| `StDevP` | Desvio-padrão populacional = √VarP | `decimal` | `0m` |

Detalhes de implementação relevantes (`AggregateCalculator.cs`):

- **`ToDecimal`** usa `InvariantCulture`; `null`→`0m`.
- **`Compare`** (`Min`/`Max`): usa `IComparable.CompareTo` quando os tipos coincidem; senão compara como `decimal`.
- **`CountDistinct`**: `HashSet` com chave normalizada (numéricos→`decimal`).
- **Avaliação por-linha** (`EvaluatePerRow`): troca a linha *live* de `Fields` por cada *snapshot* histórico (`SetCurrentRowNoSnapshot`), avalia, e **restaura** a linha *live* ao final (`finally`). Uma linha cuja avaliação lança exceção é **pulada** (`catch { continue; }`) — não aborta o agregado. *Var/StDev* materializam os decimais em lista (duas passadas: média, depois desvios²).

> **Nota sobre `Count`.** A implementação conta cada linha enumerada (a expressão é avaliada mas o valor não é testado para nulidade — o `foreach` apenas itera). Linhas cuja avaliação lança exceção são puladas pelo `EvaluatePerRow`.

#### `RunningValue` (SSRS)

`RunningValue(expr, funcAgregada [, scope])` (`ExpressionEvaluator.cs:191`) é tratado **antes** do conjunto `AggregateNames`:

- 1º arg = expressão (texto bruto);
- 2º arg = **nome** da agregação interna (identificador nu como `Sum`), extraído como **texto bruto** (não avaliado); se não estiver em `AggregateNames`, default `Sum`;
- 3º arg (opcional) = scope; **default `AggregateScope.Running`** (note: diferente do default `Report` dos demais agregados).

O comportamento cumulativo é inerente ao *buffer* de scope `Running`/`Group`, que cresce por linha e zera nos limites de grupo (6.5.8). Precisa de ≥2 argumentos, senão falha.

#### Buffers de scope e *priming*

`ReportExpressionContext` mantém três acumuladores (`_reportRows`, `_groupRows`, `_pageRows`) preenchidos por `SetCurrentRow`, e zerados por `ResetGroup`/`ResetPage`/`ResetAll`. `EvaluateAggregate` seleciona o *buffer* por scope (`ReportExpressionContext.cs:245`):

| Scope | Buffer usado |
|---|---|
| `Report` | `_reportScopeOverride ?? _reportRows` |
| `Group` | `_groupRows` |
| `Page` | `_pageRows` |
| `Running` | `_groupRows` |

`PrimeReportScope(rows)` pré-popula `_reportScopeOverride` com **todas** as linhas da iteração, de modo que agregados de scope `Report` (ex. `Sum(Fields.Total)` sem scope) rendam o **total geral** do dataset em *qualquer* banda — inclusive `ReportHeader`/`PageHeader`, que renderizam antes do loop de detalhe acumular linhas (espelha o SSRS). O paginador chama `PrimeReportScope` uma vez por passo; chamadores diretos que nunca primam mantêm o comportamento incremental histórico.

### 6.5.2. Lookup cross-dataset

`TryEvaluateLookup` (`ExpressionEvaluator.cs:233`). Três variantes, todas exigindo **≥4 argumentos**:

| Função | Retorno | `all` |
|---|---|---|
| `Lookup(source, dest, result, "Dataset")` | Primeiro match, ou `null` | `false` |
| `LookupSet(source, dest, result, "Dataset")` | `object?[]` de todos os matches | `true` |
| `MultiLookup(keys, dest, result, "Dataset")` | `object?[]`: um `Lookup` por chave do array `keys` | (vetorizado) |

Semântica de argumentos:

- **`source`** (1º) — avaliado **no escopo do chamador** (valor a casar).
- **`dest`** (2º) e **`result`** (3º) — mantidos como **texto bruto** (`ExtractRawExpression`), pois são avaliados **por linha do dataset alvo**.
- **`dataset`** (4º) — convertido a string com `InvariantCulture` (é uma chave/identificador, não um valor de exibição).

`MultiLookup` (`ExpressionEvaluator.cs:252`) trata `keys` como **array de chaves** e roda um `Lookup` de valor único por elemento, devolvendo array (Lookup *vetorizado*, **não** chave composta). Uma chave escalar é tratada como array de um elemento (via `EnumerateValues`, que não explode strings em chars).

A computação está em `context.EvaluateLookup` (`ReportExpressionContext.cs:336`):

- O dataset deve ter sido registrado por `RegisterDataset(name, rows)` (snapshots das linhas); se ausente/vazio → `Array.Empty<object?>()` (all) ou `null`.
- Para cada linha do dataset: troca `Fields` *live* pela linha, avalia `dest`; se casar `source` (ver abaixo), avalia `result`. Restaura `Fields` *live* no `finally`. Erros ao avaliar `dest` pulam a linha; erro em `result` produz `null` para aquela entrada.
- **Igualdade de chave** (`LookupKeyEquals`, `ReportExpressionContext.cs:379`): `null` nunca casa; `Equals` direto primeiro; *fallback* cross-tipo **restrito a pares numéricos** (número↔número, ou número↔string-numérica, ambos via `decimal` invariante). Isso permite int↔string-de-dígitos casarem sem `bool`/`DateTime` falso-casando por `ToString`.

### 6.5.3. Posicionais

`TryEvaluatePositional` (`ExpressionEvaluator.cs:292`) → `context.EvaluatePositional` (`ReportExpressionContext.cs:258`):

| Função | Assinatura | Retorno | Semântica |
|---|---|---|---|
| `RowNumber([scope])` | scope opcional (1º arg é *scope*, não expressão) | `int` | Posição base-1 na *scope* (= contagem de linhas vistas até a corrente; lista incremental) |
| `CountRows([scope])` | scope opcional | `int` | Total de linhas na *scope* (completo para `Report` via override primado) |
| `Previous(expr [, scope])` | 1º arg = expressão bruta; scope opcional | `object?` | Avalia `expr` contra a linha **anterior** na scope; `null` na 1ª linha |

Defaults de scope: `RowNumber`/`CountRows`/`Previous` usam `AggregateScope.Report` quando o scope é omitido.

`Previous` (`ReportExpressionContext.cs:291`): se a lista de posição tem <2 linhas → `null`; senão troca para a penúltima linha (`rows[Count-2]`), avalia, restaura a *live*; erro → `null`.

Listas usadas (`ReportExpressionContext.cs:284`): `Group`/`Running`→`_groupRows`; `Page`→`_pageRows`; demais→`_reportRows` (posição) / `_reportScopeOverride ?? _reportRows` (contagem completa).

### 6.5.4. Built-ins

`TryEvaluateBuiltin` (`ExpressionEvaluator.cs:316`):

| Função | Retorno | Semântica |
|---|---|---|
| `Today()` | `DateTime` | `context.Today` |
| `Now()` | `DateTime` | `context.Now` |
| `PageNumber()` | `int` | `context.PageNumber` |
| `TotalPages()` | `int` | `context.TotalPages` |
| `Coalesce(a, b, …)` | `object?` | Primeiro argumento não-nulo (avaliação preguiçosa, da esquerda p/ direita); `null` se todos nulos |
| `IsNull(x)` | `bool` | `true` se o 1º arg é `null` (e há ao menos 1 arg) |
| `Format(value, fmt)` | `string` | `ValueFormatter.Format(value, fmt, culture)`; precisa de ≥2 args |

Esses nomes também existem como identificadores nus (6.2.1) e/ou prefixados; a forma de *função* (com parênteses) é resolvida aqui.

### 6.5.5. Escalares estilo SSRS/VB

`TryEvaluateScalarFunction` (`ExpressionEvaluator.cs:363`). Nomes *case-insensitive* (`name.ToLowerInvariant()`). Helpers internos: `S(i)` (string via `context.Culture`), `Int(i)`, `Dt(i)`, `Bool(i)`; conversões numéricas dos `Cxxx`/`Fix`/`Int`/`Val` usam `InvariantCulture`.

> **Degradação a `null` (estilo `#Error` do SSRS).** O `switch` inteiro é envolvido por `try/catch (FormatException | InvalidCastException | OverflowException)`; um argumento incoercível (ex. texto onde se espera data/número) faz a célula degradar a `null` em vez de abortar a expressão toda. Erros aritméticos (divisão por zero) num ramo *tomado* ainda propagam.

**Condicionais**

| Função | Semântica |
|---|---|
| `IIf(cond, a, b)` | Apenas o ramo tomado é avaliado (mais seguro que o SSRS, que avalia ambos). |
| `IsNothing(x)` | `true` se `x` é `null`. |
| `Switch(c1, v1, c2, v2, …)` | Primeiro `vi` cujo `ci` é verdadeiro; senão `null`. |
| `Choose(idx, v1, v2, …)` | Índice **base-1** na lista de valores; fora de faixa → `null`. |

**Texto**

| Função | Semântica |
|---|---|
| `Concat(a, b, …)` | Concatenação variádica de strings (alvo do `&` do VB). |
| `Like(value, pattern)` | VB `Like`: `*`=qualquer sequência, `?`=um char, `#`=um dígito; **case-sensitive** (Option Compare Binary), match de string inteira; classes `[...]` **não** suportadas; *timeout* de 1s contra backtracking. |
| `Len(s)` | Comprimento. |
| `Left(s, n)` / `Right(s, n)` | n clampado a `[0, len]`. |
| `Mid(s, start [, len])` | `start` base-1; `len` opcional. |
| `Trim`/`LTrim`/`RTrim` | Remoção de espaços. |
| `UCase`/`Upper`, `LCase`/`Lower` | Caixa, usando `context.Culture`. |
| `Replace(s, old, new)` | Substituição. |
| `Space(n)` | n espaços. |
| `StrDup(n, ch)` | n cópias do 1º char do 2º arg (VB `String(n, ch)`). |
| `StrReverse(s)` | Inverte. |
| `Asc(s)` | Code point do 1º char (0 se vazio). |
| `Chr(n)` | Char do code point (clampado a `[0, 0x10FFFF]`). |
| `Val(s)` | Prefixo numérico inicial, parse invariante; 0 se nenhum. |
| `InStr([start,] s, sub)` | Posição **base-1**, 0 se ausente (start base-1 quando dado). |
| `InStrRev(s, sub)` | Última ocorrência, base-1, 0 se ausente. |
| `StrComp(a, b)` | −1/0/1 (ordinal). |
| `StrConv(s, t)` | 1=Upper, 2=Lower, 3=Title (ProperCase); outro=inalterado. |

**Partes de data**

| Função | Semântica |
|---|---|
| `Year`/`Month`/`Day`/`Hour`/`Minute`/`Second(d)` | Componente; 0 sem arg. |
| `Weekday(d)` | Dia da semana, **VB: domingo=1**. |
| `DatePart(interval, d)` | Componente nomeado pela string de intervalo (ver 6.5.9); `y`/`dy`=dia do **ano**, `w`/`weekday`=dia da semana, `ww`=semana ISO. |
| `MonthName(1..12 [, abreviar])` | Nome do mês na *culture*. |
| `WeekdayName(1..7 [, abreviar])` | Nome do dia (VB: 1=domingo). |

**Matemática de data (intervalos VB)**

| Função | Semântica |
|---|---|
| `DateAdd(interval, n, d)` | Adiciona n unidades do intervalo; intervalo desconhecido → data inalterada (6.5.9). |
| `DateDiff(interval, d1, d2)` | Contagem de fronteiras do intervalo (year/month/quarter = baseado em fronteira; day/week/hour/minute/second = decorrido). |

**Conversões (VB `Cxxx`, parse `InvariantCulture`)**

| Função | Resultado |
|---|---|
| `CStr(x)` | String via `context.Culture` (lado de exibição). |
| `CInt(x)` | `double`→arredonda (banker's, `MidpointRounding.ToEven`)→`int` ("2.5"→2). |
| `CDbl(x)` | `double`. |
| `CDec(x)` | `decimal`. |
| `CBool(x)` | `bool`. |
| `CDate(x)` | `DateTime` (já-`DateTime` passa direto; senão parse invariante). |

**Formatação (VB `Format*` → `ValueFormatter` com format string .NET)**

| Função | Format string |
|---|---|
| `FormatCurrency(v [, dec=2])` | `"C"+dec` |
| `FormatNumber(v [, dec=2])` | `"N"+dec` |
| `FormatPercent(v [, dec=2])` | `"P"+dec` (0.25 → "25%") |
| `FormatDateTime(d [, fmt 0..4])` | 0=`G`, 1=`D`(longa), 2=`d`(curta), 3=`T`(hora longa), 4=`t`(hora curta) |

O número de decimais é clampado a `[0, 15]` (`FormatDecimals()`); default 2.

**Numéricas (VB)**

| Função | Semântica |
|---|---|
| `Fix(x)` | Trunca em direção a zero (`Fix(-2.7) = -2`). |
| `Int(x)` | **Piso** em direção a −∞ (`Int(-2.7) = -3`) — VB `Int`, distinto de `CInt`. |

> `Sign`, `Abs`, `Round`, `Sqrt`, `Pow` etc. são deixados para as built-ins nativas do NCalc; não estão na tabela acima.

### 6.5.6. `Code.Metodo(...)` (opt-in)

O *rewriter* achata `Code.Metodo(` → `Code_Metodo(` (`ExpressionRewriter.cs:36`), e o handler roteia funções com prefixo `Code_` ao delegate `ExpressionEvaluator.CodeFunctionResolver` (`ExpressionEvaluator.cs:23`):

```csharp
public Func<string, object?[], object?>? CodeFunctionResolver { get; set; }
```

- **Default `null`** — o motor *core* nunca executa C#. Sem resolver, chamadas `Code.*` simplesmente não são resolvidas por esta categoria.
- O pacote opt-in `Reporting.Expressions.Roslyn` o define como um compilador Roslyn que executa o bloco `<Code>` do relatório (RDL `Code` element).
- O resolver recebe o nome do método (sem prefixo `Code_`) e os argumentos **já avaliados** (`args.Parameters.Evaluate(i)`), e retorna o resultado.

### 6.5.7. `ExtractRawExpression`

`ExtractRawExpression(LogicalExpression)` (`ExpressionEvaluator.cs:717`) reserializa um nó de AST de volta ao texto-fonte via `ToExpressionString()` (NCalc v6 expõe o nó parseado ao handler) e remove colchetes externos. Para nós Identifier/Bracket, `[Fields.Total]` → `Fields.Total`. É como agregados/lookup/posicionais recuperam o texto de uma sub-expressão para reavaliá-la por-linha em outro escopo.

### 6.5.8. `ParseScope` (mapeamento de scope)

`ParseScope(object?)` (`ExpressionEvaluator.cs:701`) converte o valor do argumento de scope (via `Convert.ToString` invariante) para `AggregateScope` (enum em `Reporting.Aggregates`, `Reporting.Core/Aggregates/AggregateScope.cs`):

| Texto de entrada | `AggregateScope` |
|---|---|
| `"Group"` / `"group"` | `Group` |
| `"Page"` / `"page"` | `Page` |
| `"Running"` / `"running"` | `Running` |
| qualquer outro / `null` | `Report` (default) |

Valores do enum (ordem de declaração): `Report` (dataset inteiro), `Page` (página corrente), `Group` (grupo corrente), `Running` (acumulado do início do scope até a linha corrente).

### 6.5.9. Strings de intervalo de data (VB)

Usadas por `DateAdd`/`DateDiff`/`DatePart`. *Case-insensitive*. Intervalo desconhecido: `DateAdd`→data inalterada; `DateDiff`/`DatePart`→0.

| Intervalo (e aliases) | `DateAdd` | `DateDiff` | `DatePart` |
|---|---|---|---|
| `yyyy` / `year` | AddYears | dif. de anos | ano |
| `q` / `quarter` | AddMonths(×3) | fronteiras de trimestre | trimestre (1..4) |
| `m` / `month` | AddMonths | dif. de meses | mês |
| `d` / `day` (e `y`/`dy` em DateAdd/DateDiff) | AddDays | dias decorridos | `d`/`day`=dia do mês; **`y`/`dy`=dia do ANO** |
| `ww` / `week` / `wk` | AddDays(×7) | semanas decorridas | semana ISO (`ISOWeek`) |
| `w` / `weekday` | — | — | dia da semana (domingo=1) |
| `h` / `hour` | AddHours | horas | hora |
| `n` / `minute` / `mi` | AddMinutes | minutos | minuto |
| `s` / `second` | AddSeconds | segundos | segundo |

> **Cuidado:** o significado de `y`/`dy` difere entre funções: em `DateAdd`/`DateDiff` `y`/`dy` é aritmética de **dia** (igual a `d`); em `DatePart`, `y`/`dy` é **dia do ano**. Comportamento intencional, herdado do VB.

---

## 6.6. Contexto de runtime (`IReportExpressionContext`)

Interface implementada por `ReportExpressionContext` (mutável, default). Superfície (`IReportExpressionContext.cs`):

| Membro | Tipo | Semântica |
|---|---|---|
| `Fields` | `IValueLookup` | Valores da linha corrente |
| `Parameters` | `IValueLookup` | Parâmetros do relatório |
| `Variables` | `IValueLookup` | Variáveis declaradas |
| `GroupKey` | `object?` | Chave do grupo ativo mais interno; `null` fora de grupo |
| `PageNumber` | `int` | Página corrente (base 1) |
| `TotalPages` | `int` | Total (resolvido no 2º passo) |
| `Now` | `DateTime` | Relógio de geração |
| `Today` | `DateTime` | `Now.Date` |
| `UserName` | `string` | Usuário (configurável) |
| `ReportName` | `string` | Nome do relatório; vazio se não definido |
| `Culture` | `CultureInfo` | Usada para formatação (templates, `Format`, saída numérica/texto) |
| `EvaluateAggregate(func, expr, scope)` | `object?` | Computa agregado |
| `EvaluateLookup(source, dest, result, dataset, all)` | `object?` | Lookup cross-dataset |
| `EvaluatePositional(func, expr, scope)` | `object?` | `RowNumber`/`CountRows`/`Previous` |
| `GetSource(name)` | `IValueLookup?` | Linha corrente de uma source nomeada (referências qualificadas) |
| `TryResolveUnqualifiedField(name, out value)` | `bool` | Fallback master-detail (6.2.2) |
| `GetReportItem(name)` / `SetReportItem(name, value)` | `object?` / `void` | `ReportItems!Name.Value` |

`IValueLookup` (`IReportExpressionContext.cs:94`): indexer `this[string]` (retorna `null` p/ chave desconhecida), `Contains(key)`, `Keys`. Implementação default `DictionaryLookup` é *case-insensitive* (`StringComparer.OrdinalIgnoreCase`).

### 6.6.1. `ReportExpressionContext` — estado e métodos de população

`ReportExpressionContext` (`ReportExpressionContext.cs`) é a implementação concreta usada pelo paginador. Defaults do construtor: `Culture` = `pt-BR` se não fornecida; `Now` = `DateTime.Now`; `UserName` = `Environment.UserName ?? "anonymous"`; `PageNumber`/`TotalPages` = 1; `ReportName` = `""`.

Métodos de população (chamados pelo paginador / *code-first*):

| Método | Efeito |
|---|---|
| `SetCurrentRow(fields)` | Define a linha *live* **e** empurra *snapshot* em `_reportRows`/`_groupRows`/`_pageRows` |
| `SetCurrentRowNoSnapshot(row)` | Define a linha *live* sem tocar acumuladores (usado por agregados/lookup/`Previous` para caminhar histórico) |
| `SetCalculatedField(name, value)` | Injeta um campo calculado na linha *live* (aditivo; RDL `<CalculatedField>`) |
| `PrimeReportScope(rows)` | Pré-popula o override de scope `Report` (total geral em qualquer banda) |
| `RegisterDataset(name, rows)` | Registra todas as linhas de um dataset nomeado p/ `Lookup`/`LookupSet` |
| `SetSourceCurrentRow(name, fields)` / `ClearSourceCurrentRow(name)` | Linha corrente de uma source (referências `Fields.Fonte.X`) |
| `ResetGroup()` / `ResetPage()` / `ResetAll()` | Zeram acumuladores (limites de grupo/página; re-execução 2-passos) |

> **Campos calculados.** `SetCalculatedField` é aditivo e não impõe verificação de ciclo — campos calculados podem referenciar campos calculados anteriores desde que o paginador os avalie em ordem de declaração; recursão infinita estoura naturalmente via a pilha do NCalc (`ReportExpressionContext.cs:85`).

---

## 6.7. Coerção e tratamento de erros

- **`Evaluate<T>`** (`ExpressionEvaluator.cs:42`): `null`→`default(T)`; match direto se já é `T`; senão `Convert.ChangeType(value, typeof(T), context.Culture)`.
- **Parse** falho → `ExpressionParseException` (carrega o texto).
- **Runtime** falho → `ExpressionEvaluationException` (embrulha a interna, carrega o texto), exceto `ExpressionParseException` que propaga.
- **Escalares** (6.5.5): coerção impossível → `null` para a célula (`FormatException`/`InvalidCastException`/`OverflowException` capturadas).
- **Agregados/Lookup/Previous**: linha cuja sub-expressão lança é **pulada** (agregados/lookup) ou produz `null` (`Previous`), sem abortar a operação.

---

## 6.8. Invariantes

1. A *culture* de **parsing** é sempre `InvariantCulture`; a *culture* de **saída** é `context.Culture`. `'1.5'` é sempre 1,5.
2. `+` é aritmético, nunca concatenação (`StringConcat` desligada). Concatenação só via `Concat(...)` ou templates.
3. Apenas cinco prefixos de escopo são reescritos: `Fields`, `Parameters`, `Variables`, `Page`, `ReportItems`. Outros tokens pontuados não são reescritos (ficam como acesso de membro NCalc, geralmente não suportado).
4. O 1º argumento de agregados (e `dest`/`result` de lookup, e a expr de `Previous`) é **texto bruto reavaliado por-linha**, não um valor avaliado no escopo do chamador.
5. Avaliação por-linha **sempre restaura** a linha *live* de `Fields` (`finally`), garantindo aninhamento seguro (agregado dentro de agregado, lookup dentro de expressão).
6. Funções do OmniReport são *case-insensitive*; o despacho segue a ordem fixa agregado → lookup → posicional → built-in → escalar → `Code_`.
7. O motor *core* nunca executa C# arbitrário; `Code.*` requer o resolver opt-in.

---

## 6.9. Exemplo integrado

```csharp
var ctx = new ReportExpressionContext(culture: CultureInfo.GetCultureInfo("pt-BR"));
ctx.ReportName = "Vendas";
ctx.RegisterDataset("Clientes", clientesRows);

// alimenta linhas (cada SetCurrentRow empurra snapshot nos buffers)
ctx.SetCurrentRow(new Dictionary<string, object?> { ["Total"] = 100m, ["ClienteId"] = 7 });

var ev = new ExpressionEvaluator();

ev.Evaluate("Fields.Total * 1.1", ctx);                    // 110m  (decimal, invariante)
ev.Evaluate("Sum(Fields.Total)", ctx);                     // total report-scoped
ev.Evaluate("RunningValue(Fields.Total, Sum, 'Group')", ctx);
ev.Evaluate("Lookup(Fields.ClienteId, Fields.Id, Fields.Nome, 'Clientes')", ctx);
ev.Evaluate("IIf(Fields.Total > 50, 'alto', 'baixo')", ctx);  // "alto"
ev.Evaluate("Concat('Pg ', PageNumber, '/', TotalPages)", ctx);
ev.Evaluate("FormatCurrency(Fields.Total)", ctx);          // "R$ 100,00"

// template
new TemplateRenderer(ev).Render("Cliente {Fields.ClienteId}: {Fields.Total:C}", ctx);
// → "Cliente 7: R$ 100,00"
```

Equivalência RDL → OmniReport (feita na desserialização, não no runtime):

| RDL (VB) | OmniReport |
|---|---|
| `=Fields!Total.Value` | `Fields.Total` |
| `=Parameters!P.Value` | `Parameters.P` |
| `=Globals!PageNumber` | `PageNumber` |
| `=Globals!ExecutionTime` | `Now` |
| `=ReportItems!Caixa.Value` | `ReportItems.Caixa` |
| `=User!UserID` | `UserName` |
| `=a & b` | `Concat(a, b)` |
| `=x Like "A*"` | `Like(x, "A*")` |
