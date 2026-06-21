# Expressions

OmniReport usa **NCalc 5.x** como engine de avaliação, com extensões próprias para
casar com a realidade de relatórios bandeados: campos de dados, parâmetros, agregadores
com escopo, templates de formatação e funções de relatório.

## Modelo conceitual

Toda expressão é avaliada contra um `ReportExpressionContext`. O contexto carrega:

| Acessor | Conteúdo |
|---|---|
| `Fields.<Nome>` | Campos da linha corrente da fonte de dados |
| `Parameters.<Nome>` | Parâmetros do relatório (`ReportParameter`) |
| `Variables.<Nome>` | Variáveis acumuladas durante a execução |
| `Page.Number`, `Page.Total` | Numeração de páginas (resolvida no two-pass) |
| `ReportName` | Nome do relatório (RDL `Globals!ReportName`) — identificador nu; vazio se não definido. Como os outros globais nus, tem precedência sobre um campo de mesmo nome (use `Fields.ReportName` para o campo) |
| `Language` | Cultura ativa do relatório (RDL `Globals!Language` / `User!Language`) — identificador nu, retorna o nome da cultura (ex. `"en-US"`). Definida por `Metadata["Language"]` (que o import de `.rdl` preenche a partir de `<Report><Language>`, e o code-first via `.Language("en-US")`/`.Culture(...)`); ausente → cultura default do motor (pt-BR). Dirige `Format`/`FormatDateTime`/`Style.Format` no render |
| `ReportItems.<Nome>` | Valor que outro text box nomeado renderizou (RDL `ReportItems!Nome.Value`) — disponível em bandas renderizadas **depois** da referenciada (ex.: um rodapé ecoando um text box do corpo); `null` se ainda não renderizado (ex.: cabeçalho de página referenciando o corpo). Detalhes do 1º corte: registra o **texto renderizado** (string formatada), não o valor tipado; só itens **visíveis** publicam; num rodapé de página, reflete o último valor renderizado **na ou antes daquela página** (numa página sem o item, o valor da página anterior) |
| `Report.Now`, `Report.Today`, `Report.UserName` | Funções de runtime |

```csharp
var ctx = new ReportExpressionContext();
ctx.SetCurrentRow(new Dictionary<string, object?>
{
    ["Cliente"] = "Acme",
    ["Total"] = 1234.56m,
});
ctx.Parameters["Mes"] = "Maio";

var compiler = new ExpressionCompiler();
var compiled = compiler.Compile("Fields.Total * 1.10");
var result = compiled.Evaluate(ctx); // → 1358.016m
```

## Sintaxe

NCalc reconhece a maior parte de ECMAScript. As extensões internas do OmniReport
reescrevem `Fields.X` / `Parameters.X` / `Variables.X` em parâmetros NCalc antes da
compilação, então a sintaxe efetiva é:

```text
Fields.Total                    // campo
Parameters.MesReferencia        // parâmetro
Variables.SaldoAcumulado        // variável
Fields.Total + Fields.Frete     // aritmética
Fields.Total > 1000 ? "Alto" : "Baixo"
if(Fields.Tipo == "PIX", 0, Fields.Total * 0.02)
```

Decimais usam ponto, **independente da cultura do thread** — a compilação é cultura-invariant.
Para formatar a saída no padrão pt-BR, use templates (`{expr:fmt}`).

## Agregadores com escopo

```text
Sum(Fields.Total)               // total geral
Sum(Fields.Total, 'Group')      // total do grupo corrente
Sum(Fields.Total, 'Page')       // total acumulado da página
Avg(Fields.Total, 'Group')
Min(Fields.Data)
Max(Fields.Total, 'Page')
Count(Fields.Id, 'Group')       // conta itens do grupo
```

Escopos válidos: `Report` (default), `Group`, `Page`, `Running` (acumulado do início do
escopo até a linha corrente). A função `RunningTotal(...)` é o atalho para o caso `Running`.

Internamente, `AggregateCalculator` mantém pilhas de acumuladores indexadas pelo
escopo. A paginação reseta o escopo apropriado entre páginas / grupos.

## Concatenação e Like

```text
Concat('Total: ', Fields.Valor)   // concatena (alvo do operador & do VB/SSRS)
Like(Fields.Sku, 'A#*')           // padrão VB: * = qualquer, ? = 1 char, # = 1 dígito (case-sensitive, como o VB)
```

O `+` é **numérico** (não concatena strings) — use `Concat`. O import de `.rdl` reescreve o operador
`&` do VB em `Concat(...)` automaticamente.

## Funções escalares (estilo VB/SSRS)

Vocabulário comum disponível (case-insensitive), além das nativas do NCalc (`Abs`/`Round`/`Sqrt`/…):

- **Condicional/texto:** `IIf`, `Switch`, `Choose`, `IsNothing`/`IsNull`, `Coalesce`; `Len`, `Left`, `Right`,
  `Mid`, `Trim`/`LTrim`/`RTrim`, `UCase`/`LCase`, `Replace`, `InStr`.
- **Conversões VB:** `CStr`, `CInt`, `CDbl`, `CDec`, `CBool`, `CDate`.
- **Data:** `Year`/`Month`/`Day`/`Hour`/`Minute`/`Second`/`Weekday`, `DateAdd`, `DateDiff`,
  **`DatePart(interval, date)`** (`yyyy`/`q`/`m`/`d`/`y`=dia-do-ano/`ww`/`w`/`h`/`n`/`s`),
  **`MonthName(n [,abrev])`**, **`WeekdayName(n [,abrev])`** (1 = domingo).
- **Formatação** (usam a cultura do contexto): `Format(value, fmt)`, **`FormatCurrency(v [,casas])`**,
  **`FormatNumber(v [,casas])`**, **`FormatPercent(v [,casas])`** (`0.25` → `25%`),
  **`FormatDateTime(date [,0..4])`** (0 geral, 1 longa, 2 curta, 3 hora-longa, 4 hora-curta).
- **Numérico VB:** **`Fix(x)`** (trunca p/ zero), **`Int(x)`** (piso p/ −∞), **`Sign(x)`**.

Um argumento não-coercível degrada para `null`/#Error naquela célula (não aborta a expressão).

## Funções posicionais e de escopo (estilo SSRS)

```text
RowNumber()            // posição 1-based da linha corrente no escopo (Report por padrão)
RowNumber('Group')     // posição dentro do grupo corrente
CountRows()            // total de linhas no escopo
Previous(Fields.Total) // valor da expressão na linha ANTERIOR (null na 1ª)
First(Fields.Nome)     // expressão na 1ª linha do escopo
Last(Fields.Nome)      // expressão na última linha do escopo
CountDistinct(Fields.Cidade) // contagem de valores distintos não-nulos no escopo
```

`RowNumber`/`CountRows` aceitam um escopo opcional como 1º argumento; `Previous`/`First`/`Last`/
`CountDistinct` recebem a expressão e um escopo opcional. `First`/`Last`/`CountDistinct` são reduções
de escopo (compartilham a maquinaria dos agregadores — escopo Report usa o conjunto completo);
`RowNumber`/`Previous` são posicionais (rastreiam a posição da linha corrente).

## Lookup entre datasets

No estilo SSRS, `Lookup` / `LookupSet` cruzam **outro** dataset sem um join prévio —
útil para "puxar" um nome, uma taxa ou uma descrição a partir de um id:

```text
Lookup(Fields.ClienteId, Fields.Id, Fields.Nome, 'Clientes')
// para o ClienteId da linha corrente, acha a 1ª linha de "Clientes"
// onde Id == ClienteId e devolve Nome (ou null se nada casar)

LookupSet(Fields.Categoria, Fields.Cat, Fields.Nome, 'Itens')
// devolve um object?[] com TODOS os Nome cujo Cat == Categoria
```

Assinatura: `Lookup(origem, destino, resultado, "Dataset")`. A **origem** é avaliada no
escopo da linha corrente; **destino** e **resultado** são avaliados em cada linha do
dataset alvo (são mantidos como texto de expressão, igual aos agregadores, não avaliados
de imediato). A comparação casa por igualdade de valor, com _fallback_ para string
invariante — então um `Id` inteiro num dataset casa com um id string em outro.

Todos os datasets registrados no relatório ficam disponíveis para `Lookup` (o paginator
materializa as linhas de cada um). Funciona igual nos 3 modos de autoria, já que é uma
função de expressão: use a mesma string em code-first, low-level e no Designer.

**Atenção:** `destino` e `resultado` são **expressões**, não aspas — escreva `Fields.Id`,
não `'Id'` (uma string literal vira a constante `"Id"` em toda linha e nada casa, igual aos
agregadores). Só o nome do **dataset** (4º argumento) é string literal.

Custo: `Lookup` varre as linhas do dataset alvo a cada chamada — O(linhas-alvo) por linha
de detalhe, sem índice/hash (o SSRS mantém hash). Para tabelas de apoio pequenas é
irrelevante; para datasets grandes em loops longos, considere pré-juntar os dados.

## Templates de formatação

Para textos compostos, use `{expression:format}` dentro de strings:

```text
"Cliente: {Fields.Cliente} — Total {Fields.Total:C}"
"Página {Page.Number} de {Page.Total}"
"Vendido em {Fields.Data:dd/MM/yyyy}"
```

`TemplateRenderer` extrai cada `{...:fmt}`, compila a expressão interna, aplica
`string.Format("{0:fmt}", value)` na cultura corrente e concatena o resultado.

Por que templates em vez de `+` em strings: a aritmética NCalc trata `+` como soma
numérica quando os dois lados são numéricos. Misturar string e número com `+` força
o usuário a converter manualmente. Templates resolvem o problema sem ambiguidade.

## Texto literal

`TextBox.Text("Relatório de Vendas")` aceita texto puro sem chamar a engine — o
`BandRenderer` detecta strings sem `{` / `}` / sintaxe de expressão e renderiza
direto, evitando falha de parse para títulos e legendas.

## Funções customizadas

Registre via `ReportExpressionContext.RegisterFunction`:

```csharp
ctx.RegisterFunction("IcmsBase", args =>
{
    var total = Convert.ToDecimal(args[0]);
    var aliquota = Convert.ToDecimal(args[1]);
    return total * (1 - aliquota / 100m);
});

// usar: "IcmsBase(Fields.Total, 18)"
```

## Validação e cache

`ExpressionCompiler.Compile` cacheia a AST por chave textual. Compilações repetidas
da mesma expressão (típico em relatórios com milhares de linhas) são instantâneas
após a primeira.

Para validar uma expressão sem executar (designer: `F8`):

```csharp
var diag = compiler.Validate("Fields.Total * Parameters.Markup");
if (!diag.IsValid)
{
    Console.WriteLine(diag.Message);
}
```

## Limitações conhecidas

- Sem null-conditional `?.` nativo — use `if(IsNull(Fields.X), 0, Fields.X)`.
- Sem acesso a tipos `System.*` (sandboxing intencional contra injection).
- Funções customizadas executam síncronas; sem suporte a `async`.
- A cultura de `Metadata["Language"]` chega aos formatadores de banda e Tablix; os renderers de **Chart** e **KPI** ainda formatam em pt-BR fixo (não recebem o contexto) — follow-up para propagar a cultura até eles.
