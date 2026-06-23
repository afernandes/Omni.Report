# 1. Modelo de objetos: ReportDefinition

Esta seção especifica o **modelo de objetos** do OmniReport: a árvore de tipos imutáveis cuja raiz é `ReportDefinition`. Este é o *AST canônico* (Abstract Syntax Tree) consumido pelo motor de layout e produzido por três caminhos equivalentes — a API code-first, o Designer e os serializers (`.repx`/`.repjson`). A imutabilidade total e a igualdade estrutural deste modelo são o que garante o *round-trip* (~98%) entre persistência e memória.

Os tipos descritos aqui residem no assembly `Reporting.Core`, namespace raiz `Reporting`, com sub-namespaces `Reporting.Paper`, `Reporting.Bands`, `Reporting.Data`, `Reporting.Parameters`, `Reporting.Common` e `Reporting.Geometry`.

## 1.1. O record raiz `ReportDefinition`

Arquivo: `src/Reporting.Core/ReportDefinition.cs`.

```csharp
public sealed record ReportDefinition(
    string Name,
    PageSetup PageSetup,
    DetailBand Detail)
{
    public string SchemaVersion { get; init; } = "1.0";
    public EquatableArray<ReportParameter> Parameters { get; init; } = EquatableArray<ReportParameter>.Empty;
    public EquatableArray<DataSourceDefinition> DataSources { get; init; } = EquatableArray<DataSourceDefinition>.Empty;
    public EquatableArray<ReportVariable> Variables { get; init; } = EquatableArray<ReportVariable>.Empty;
    public ReportBand? ReportHeader { get; init; }
    public ReportBand? PageHeader { get; init; }
    public EquatableArray<GroupBand> Groups { get; init; } = EquatableArray<GroupBand>.Empty;
    public ReportBand? PageFooter { get; init; }
    public ReportBand? ReportFooter { get; init; }
    public EquatableDictionary<string, string> Metadata { get; init; } = EquatableDictionary<string, string>.Empty;

    public static ReportDefinition Empty(string name)
        => new(name, PageSetup.A4Portrait, DetailBand.Empty);
}
```

`ReportDefinition` é um `sealed record` (igualdade por valor) com **três parâmetros posicionais obrigatórios** (`Name`, `PageSetup`, `Detail`) e **dez propriedades `init`** opcionais. Não há nenhum estado mutável; toda evolução de uma definição se dá por `with`-expression, produzindo um novo objeto.

### 1.1.1. Campos

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | — (posicional, obrigatório) | Nome lógico do relatório. Identificador livre; usado por UI/serialização. Não há validação de unicidade nem de não-vazio no Core. |
| `PageSetup` | `PageSetup` | — (posicional, obrigatório) | Configuração física de papel/página (§1.3). Nunca nulo — `PageSetup` é um `record` (tipo referência) cuja convenção de fábrica fornece `A4Portrait`. |
| `Detail` | `DetailBand` | — (posicional, obrigatório) | A banda de detalhe — repetida uma vez por linha de dados. É **a única banda obrigatória**; todas as demais são opcionais. Ver §1.2. |
| `SchemaVersion` | `string` | `"1.0"` | Versão do schema do modelo persistido. Permite evolução/migração. Constante no código atual; não há lógica de migração condicional a esse valor no Core. |
| `Parameters` | `EquatableArray<ReportParameter>` | `Empty` | Parâmetros tipados do relatório (prompts), na ordem declarada. |
| `DataSources` | `EquatableArray<DataSourceDefinition>` | `Empty` | Datasets declarados (referências resolvidas por nome em runtime). |
| `Variables` | `EquatableArray<ReportVariable>` | `Empty` | Variáveis computadas globais (escopo report/row/group). |
| `ReportHeader` | `ReportBand?` | `null` | Banda emitida uma vez, no início do relatório. `null` = ausente. |
| `PageHeader` | `ReportBand?` | `null` | Banda emitida no topo de cada página. `null` = ausente. |
| `Groups` | `EquatableArray<GroupBand>` | `Empty` | Bandas de agrupamento, **ordenadas do mais externo para o mais interno** (a ordem do array é a hierarquia de aninhamento). |
| `PageFooter` | `ReportBand?` | `null` | Banda emitida no rodapé de cada página. `null` = ausente. |
| `ReportFooter` | `ReportBand?` | `null` | Banda emitida uma vez, ao final do relatório. `null` = ausente. |
| `Metadata` | `EquatableDictionary<string, string>` | `Empty` | Bag chave→valor para metadados livres (autor, descrição etc.). Opaco ao motor; preservado no round-trip. |

> **Nota (`SchemaVersion`).** Embora não faça parte da assinatura posicional original e seja muitas vezes omitido em descrições informais, `SchemaVersion` **existe** como propriedade `init` (linha 21 de `ReportDefinition.cs`) e participa da igualdade estrutural do record. Está implementado apenas como carimbo constante `"1.0"`; nenhum código no Core ramifica comportamento com base nele.

### 1.1.2. A fábrica `Empty`

```csharp
public static ReportDefinition Empty(string name)
    => new(name, PageSetup.A4Portrait, DetailBand.Empty);
```

Produz a **definição mínima válida**: papel A4 retrato com margens uniformes de 20 mm e banda de detalhe vazia (altura zero, sem elementos). Todas as coleções/bandas opcionais ficam em seus defaults (`Empty`/`null`).

### 1.1.3. Invariantes

- **Imutabilidade total.** Não há *setters*; apenas `init`. Mutações são `with`-expressions.
- **`Detail` nunca nulo.** É obrigatório posicional. Os demais "slots" de banda (`ReportHeader`/`PageHeader`/`PageFooter`/`ReportFooter`) são `ReportBand?` e usam `null` como "ausente".
- **Coleções nunca `default`-inválidas.** As propriedades de coleção têm default `EquatableArray<T>.Empty`/`EquatableDictionary<…>.Empty`; além disso o próprio `EquatableArray` normaliza `default`/`IsDefault` para vazio (§1.4), então iterar qualquer coleção é sempre seguro.
- **Igualdade estrutural profunda.** Dois `ReportDefinition` construídos com os mesmos insumos são `Equals` (e têm o mesmo `GetHashCode`), recursivamente, graças a `record` + `EquatableArray`/`EquatableDictionary`. Esta é a base do contrato de round-trip.

## 1.2. A árvore do modelo

Tudo "pendura" na raiz `ReportDefinition`. A árvore tem três grandes ramos: **dados** (`DataSources`, `Parameters`, `Variables`), **layout de página** (`PageSetup`) e **bandas** (cabeçalhos/rodapés/`Groups`/`Detail`). As bandas, por sua vez, contêm `ReportElement`s (especificados em outra seção) e — no caso de `DetailBand` — sub-bandas aninhadas.

```
ReportDefinition  (record raiz, imutável)
├─ Name : string
├─ SchemaVersion : string = "1.0"
├─ PageSetup : PageSetup
│   ├─ Paper : PaperSize { Name, Width: Unit, Height: Unit }
│   ├─ Orientation : Orientation (Portrait | Landscape)
│   ├─ Margins : Thickness (Left/Top/Right/Bottom : Unit)
│   ├─ Columns : int = 1
│   ├─ ColumnSpacing : Unit
│   └─ (derivados) PageWidth · PageHeight · ContentWidth · ContentHeight · IsContinuous
│
├─ Parameters : EquatableArray<ReportParameter>
│   └─ ReportParameter { Name, ValueType, Prompt?, DefaultValue?, AllowMultiple,
│                        Required, AvailableValues?, Nullable, AllowBlank, Hidden }
│        └─ AvailableValues : ParameterAvailableValues
│             ├─ Values : EquatableArray<ParameterValue { Value, Label? }>
│             └─ DataSet? · ValueField? · LabelField?  (query-driven)
│
├─ DataSources : EquatableArray<DataSourceDefinition>
│   └─ DataSourceDefinition { Name, DataMember?, Fields, Relations,
│                            Parameters, CalculatedFields, FilterExpression?, SortExpressions }
│        ├─ Fields : EquatableArray<DataField { Name, FieldType?, DisplayName? }>
│        └─ Relations : EquatableArray<DataRelation { Name, ParentSource, ParentField,
│                                                     ChildSource, ChildField }>
│
├─ Variables : EquatableArray<ReportVariable { Name, Expression, Scope, InitialValue? }>
│
├─ ReportHeader : ReportBand?     ─┐
├─ PageHeader   : ReportBand?      │  ReportBand { Kind, Height, Elements,
│                                  │               Visible, VisibleExpression?,
├─ Groups : EquatableArray<GroupBand>     PrintOnFirstPage, PrintOnLastPage, PageBreak }
│   └─ GroupBand { Name, GroupExpression, Header: ReportBand?, Footer: ReportBand?,
│                  KeepTogether, NewPageBefore, NewPageAfter, RepeatHeaderOnNewPage,
│                  Visible, VisibleExpression?, PageBreak, FilterExpression?,
│                  SortExpressions, Variables }
│
├─ Detail : DetailBand
│   ├─ Height · Elements · Visible · VisibleExpression?
│   ├─ CanGrow · CanShrink
│   ├─ NoRowsMessage? · FilterExpression? · SortExpressions · PageBreak · DataSetName?
│   └─ SubDetails : EquatableArray<SubDetailBand>
│        └─ SubDetailBand { Name, DataMember, Height, Elements, Header?, Footer?,
│                          Visible, VisibleExpression?, PrintIfEmpty, NoRowsMessage?,
│                          FilterExpression?, SortExpressions }
│
├─ PageFooter   : ReportBand?     ─┘
├─ ReportFooter : ReportBand?
└─ Metadata : EquatableDictionary<string, string>
```

Cada banda implementa a interface `IBand` (`src/Reporting.Core/Bands/Bands.cs:20`):

```csharp
public interface IBand
{
    BandKind Kind { get; }
    Unit Height { get; }
    bool Visible { get; }
    string? VisibleExpression { get; }
    EquatableArray<ReportElement> Elements { get; }
}
```

O enum `BandKind` enumera os papéis de banda na ordem vertical de emissão:

```csharp
public enum BandKind
{
    ReportHeader, PageHeader, GroupHeader, Detail, GroupFooter, PageFooter, ReportFooter,
}
```

Notas de design relevantes para a raiz:

- **`Groups` é uma lista achatada, não aninhada.** A hierarquia master→sub-grupo é dada **pela ordem** do array (índice 0 = grupo mais externo). Cada `GroupBand` carrega seu próprio `Header`/`Footer` (ambos `ReportBand?`) e seu `GroupExpression`.
- **`GroupBand` é uma banda "virtual".** Seu `Height` é derivado (`Header.Height + Footer.Height`) e seus `Elements` são a concatenação de `Header.Elements` e `Footer.Elements` (`Bands.cs:152-155`); ela não possui elementos próprios.
- **Aninhamento de detalhe vive em `Detail.SubDetails`**, não na raiz. `SubDetailBand` itera um data source filho por linha-pai (master-detail), com `Header`/`Footer` próprios.

## 1.3. `PageSetup` (layout físico de página)

Arquivo: `src/Reporting.Core/Paper/PageSetup.cs`.

```csharp
public sealed record PageSetup(
    PaperSize Paper,
    Orientation Orientation = Orientation.Portrait,
    Thickness Margins = default,
    int Columns = 1,
    Unit ColumnSpacing = default)
{
    public static readonly PageSetup A4Portrait = new(
        PaperSize.A4, Orientation.Portrait, Thickness.Uniform(Unit.FromMm(20)));

    public Unit PageWidth     => Orientation == Orientation.Portrait ? Paper.Width  : Paper.Height;
    public Unit PageHeight    => Orientation == Orientation.Portrait ? Paper.Height : Paper.Width;
    public Unit ContentWidth  => PageWidth  - Margins.Horizontal;
    public Unit ContentHeight => PageHeight - Margins.Vertical;
    public bool IsContinuous  => Paper.Height == Unit.Zero;
}
```

### 1.3.1. Campos

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Paper` | `PaperSize` | — (posicional, obrigatório) | Dimensões físicas do papel em `Unit` (mils). Ver §1.3.3. |
| `Orientation` | `Orientation` | `Portrait` | Retrato ou paisagem. Em paisagem, largura e altura do papel são trocadas pelos derivados (não muta o `PaperSize`). |
| `Margins` | `Thickness` | `default` (= `Thickness.Zero`, todas as bordas em `Unit.Zero`) | Margens por lado. **Atenção ao default posicional:** um `PageSetup` construído sem `Margins` tem margem **zero**; é a fábrica `A4Portrait` que injeta 20 mm uniformes. |
| `Columns` | `int` | `1` | Número de colunas "jornal". `1` = layout single-column. |
| `ColumnSpacing` | `Unit` | `default` (= `Unit.Zero`) | Espaçamento horizontal (gutter) entre colunas quando `Columns > 1`. |

### 1.3.2. Propriedades derivadas (computed)

Todas são *read-only* e calculadas a partir dos campos; não participam da serialização nem da igualdade (são expressões, não estado).

| Derivado | Tipo | Fórmula | Semântica |
|---|---|---|---|
| `PageWidth` | `Unit` | `Portrait ? Paper.Width : Paper.Height` | Largura efetiva da página **após orientação, antes das margens**. |
| `PageHeight` | `Unit` | `Portrait ? Paper.Height : Paper.Width` | Altura efetiva da página após orientação, antes das margens. |
| `ContentWidth` | `Unit` | `PageWidth − Margins.Horizontal` | Largura útil de conteúdo (`Margins.Horizontal = Left + Right`). |
| `ContentHeight` | `Unit` | `PageHeight − Margins.Vertical` | Altura útil de conteúdo (`Margins.Vertical = Top + Bottom`). |
| `IsContinuous` | `bool` | `Paper.Height == Unit.Zero` | `true` para papel de altura "infinita" (bobina térmica) — sinaliza ao paginador que **não há quebra de página** vertical. |

> **Interação colunas × contínuo.** Conforme a nota de engenharia do paginador, papel contínuo combinado com `Columns > 1` é tratado intencionalmente como **1 coluna**.

### 1.3.3. `PaperSize` e `Orientation`

```csharp
public enum Orientation { Portrait, Landscape }

public sealed record PaperSize(string Name, Unit Width, Unit Height)
{
    public static readonly PaperSize A4        = new("A4",        Unit.FromMm(210), Unit.FromMm(297));
    public static readonly PaperSize A5        = new("A5",        Unit.FromMm(148), Unit.FromMm(210));
    public static readonly PaperSize Letter    = new("Letter",    Unit.FromInch(8.5), Unit.FromInch(11));
    public static readonly PaperSize Legal     = new("Legal",     Unit.FromInch(8.5), Unit.FromInch(14));
    public static readonly PaperSize Thermal58 = new("Thermal58", Unit.FromMm(58), Unit.Zero);
    public static readonly PaperSize Thermal80 = new("Thermal80", Unit.FromMm(80), Unit.Zero);

    public PaperSize Rotated() => new(Name, Height, Width);
}
```

| Campo de `PaperSize` | Tipo | Semântica |
|---|---|---|
| `Name` | `string` | Rótulo do tamanho ("A4", "Letter", "Thermal80"…). Identifica o preset; arbitrário para tamanhos custom. |
| `Width` | `Unit` | Largura física (mils). |
| `Height` | `Unit` | Altura física (mils). **`Unit.Zero` ⇒ contínuo** (bobina sem quebra). |

Presets estáticos disponíveis: `A4`, `A5`, `Letter`, `Legal`, `Thermal58`, `Thermal80`. Os dois térmicos têm `Height = Unit.Zero` (bobinas de recibo brasileiras de 58 mm e 80 mm), o que os marca como contínuos via `IsContinuous`. O método `Rotated()` devolve um novo `PaperSize` com largura/altura trocadas, preservando o `Name` (usado quando se quer rotacionar o próprio papel em vez de usar `Orientation`).

`Orientation` tem exatamente dois valores: `Portrait` (padrão) e `Landscape`. A orientação **não** muta o `PaperSize`; ela só altera qual dimensão entra como largura/altura nos derivados `PageWidth`/`PageHeight`.

### 1.3.4. Unidade de medida: `Unit` e `Thickness`

`PageSetup`, e o modelo inteiro, expressam comprimentos em `Unit` — um `readonly record struct` que armazena um inteiro de **mils** (1/1000 de polegada). Arquivo: `src/Reporting.Core/Geometry/Unit.cs`.

```csharp
public readonly record struct Unit(int Mils) : IComparable<Unit>
{
    public static readonly Unit Zero = new(0);
    public static Unit FromMm(double mm)       => new((int)Math.Round(mm * 1000.0 / 25.4));
    public static Unit FromInch(double inch)   => new((int)Math.Round(inch * 1000.0));
    public static Unit FromPoint(double pt)    => new((int)Math.Round(pt * 1000.0 / 72.0));
    public static Unit FromPixels(double px, double dpi = 96.0) => new((int)Math.Round(px * 1000.0 / dpi));
    // … ToMm/ToInches/ToPoints/ToPixels, operadores +,-,*,/ e comparação …
}
```

A escolha de inteiro em mils torna a aritmética de empilhamento de bandas **exata** (sem erro de ponto-flutuante) e o snap-to-grid trivial. `1 inch = 1000 mils`; `1 mm ≈ 39.37 mils` (arredondado ao inteiro mais próximo). `Unit` implementa `IComparable<Unit>` e operadores aritméticos/relacionais completos, por isso comparações como `Paper.Height == Unit.Zero` (usada em `IsContinuous`) são igualdade estrutural de struct.

`Thickness` (margens/padding/bordas) é um `readonly record struct` de quatro `Unit` por lado, com derivados `Horizontal = Left + Right` e `Vertical = Top + Bottom`, e fábricas `Uniform`/`Symmetric` (`src/Reporting.Core/Geometry/Primitives.cs:45`):

```csharp
public readonly record struct Thickness(Unit Left, Unit Top, Unit Right, Unit Bottom)
{
    public static readonly Thickness Zero = new(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero);
    public static Thickness Uniform(Unit value) => new(value, value, value, value);
    public static Thickness Symmetric(Unit horizontal, Unit vertical) => new(horizontal, vertical, horizontal, vertical);
    public Unit Horizontal => Left + Right;
    public Unit Vertical   => Top + Bottom;
}
```

> **Invariante de default.** Como `Thickness` e `Unit` são `struct`s, o default de C# (`default(Thickness)` / `default(Unit)`) já equivale a `Thickness.Zero` / `Unit.Zero`. Por isso `Margins = default` e `ColumnSpacing = default` em `PageSetup` são, respectivamente, margens zero e espaçamento zero — sem necessidade de inicialização explícita.

## 1.4. Igualdade estrutural: `EquatableArray<T>` e `EquatableDictionary<TKey,TValue>`

O contrato de igualdade por valor de um `record` cobre automaticamente campos escalares e outros `record`s, mas **não** cobre coleções `ImmutableArray<T>`/`ImmutableDictionary<,>` da BCL — estas usam igualdade por referência. Sem um wrapper, dois `ReportDefinition` "iguais" jamais seriam `Equals`, quebrando o round-trip. `EquatableArray<T>` e `EquatableDictionary<TKey,TValue>` (namespace `Reporting.Common`) resolvem isso encapsulando as coleções imutáveis com igualdade **elemento-a-elemento**. É essa mecânica que entrega a taxa de round-trip ~98%.

### 1.4.1. `EquatableArray<T>`

Arquivo: `src/Reporting.Core/Common/EquatableArray.cs`.

```csharp
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);
    public EquatableArray(ImmutableArray<T> items) => _items = items.IsDefault ? ImmutableArray<T>.Empty : items;
    public EquatableArray(IEnumerable<T> items) : this(items?.ToImmutableArray() ?? ImmutableArray<T>.Empty) { }
    // Count, this[int], GetEnumerator, Equals, GetHashCode, ==/!= …
}
```

Características e invariantes:

- **`readonly struct`** envolvendo `ImmutableArray<T>`; implementa `IReadOnlyList<T>` (acesso por índice + enumeração) e `IEquatable<EquatableArray<T>>`.
- **Normalização de `default`.** Tanto o construtor quanto a propriedade interna `Items` convertem `ImmutableArray` "default" (`IsDefault`) para `ImmutableArray<T>.Empty`. Consequência crucial: um campo de coleção declarado com `= default` (como muitos parâmetros de `SubDetailBand`/`DataSourceDefinition`) **nunca lança `NullReferenceException`** ao ser iterado/contado — ele se comporta como vazio.
- **Igualdade ordenada elemento-a-elemento** (`Equals`, linhas 29-46): mesma `Length` e `EqualityComparer<T>.Default.Equals` posição a posição. A **ordem importa**.
- **`GetHashCode`** combina os hashes na ordem dos elementos (via `HashCode`).
- **Conversões implícitas** de `ImmutableArray<T>` e de `T[]`, mais helpers `EquatableArray.Create(params T[])` / `EquatableArray.From(IEnumerable<T>)`.

### 1.4.2. `EquatableDictionary<TKey,TValue>`

Arquivo: `src/Reporting.Core/Common/EquatableDictionary.cs`.

```csharp
public readonly struct EquatableDictionary<TKey, TValue>
    : IEquatable<EquatableDictionary<TKey, TValue>>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public static readonly EquatableDictionary<TKey, TValue> Empty = new(ImmutableDictionary<TKey, TValue>.Empty);
    // …
}
```

Características e invariantes:

- **`readonly struct`** sobre `ImmutableDictionary<TKey,TValue>`; implementa `IReadOnlyDictionary<TKey,TValue>` e `IEquatable<…>`. Restrição `where TKey : notnull`.
- **Normalização de nulo:** construtor e `Items` substituem dicionário nulo por `ImmutableDictionary<…>.Empty`.
- **Igualdade independente de ordem** (`Equals`, linhas 36-53): mesma contagem e, para cada par, a chave existe no outro com valor igual por `EqualityComparer<TValue>.Default`. Diferente do array, **a ordem das chaves não afeta a igualdade**.
- **`GetHashCode` determinístico:** itera os pares **ordenados por chave** (`OrderBy(k => k.Key)`) antes de combinar, garantindo hash estável independentemente da ordem de inserção.
- Usado para `ReportDefinition.Metadata` e para `DataSourceDefinition.Parameters`.

### 1.4.3. Por que isso dá o round-trip

Como `ReportDefinition` é um `record` cujos campos de coleção são `EquatableArray`/`EquatableDictionary` (e cujos campos escalares/aninhados são `record`s ou structs com igualdade por valor), a igualdade do record se torna **estrutural e recursiva**:

```csharp
var a = ReportDefinition.Empty("rel")
    with { Metadata = new(new Dictionary<string,string> { ["author"] = "ana" }) };

var b = ReportDefinition.Empty("rel")
    with { Metadata = new(new Dictionary<string,string> { ["author"] = "ana" }) };

// true — apesar de instâncias distintas e dicionários distintos:
bool iguais = a == b;
```

Esta propriedade é o que permite afirmar `Deserialize(Serialize(def)).Equals(def)` para a vasta maioria dos relatórios (round-trip ~98%). Os ~2% restantes são divergências conhecidas de materialização fora do escopo do modelo de objetos (por exemplo, normalização de estilos `inherit`→concreto feita pelo Designer ao carregar), e não falhas da igualdade estrutural aqui descrita.

## 1.5. Exemplo code-first completo (mínimo)

```csharp
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Paper;

var def = ReportDefinition.Empty("Vendas")
    with
    {
        PageSetup = new PageSetup(
            PaperSize.A4,
            Orientation.Landscape,
            Thickness.Uniform(Unit.FromMm(15)),
            Columns: 2,
            ColumnSpacing: Unit.FromMm(8)),

        PageHeader  = ReportBand.Empty(BandKind.PageHeader) with { Height = Unit.FromMm(12) },
        Detail      = new DetailBand(Unit.FromMm(8), EquatableArray<ReportElement>.Empty, CanGrow: true),
        PageFooter  = ReportBand.Empty(BandKind.PageFooter) with { Height = Unit.FromMm(10) },

        Metadata = new(new Dictionary<string, string> { ["author"] = "ana" }),
    };

// Derivados de página (A4 paisagem ⇒ 297×210 mm, menos 15 mm de margem por lado):
Unit larguraUtil = def.PageSetup.ContentWidth;   // 297 − 30 = 267 mm
bool continuo    = def.PageSetup.IsContinuous;    // false (A4 tem altura > 0)
```

Os três caminhos de produção (code-first acima, Designer e desserialização de `.repx`/`.repjson`) convergem para um `ReportDefinition` estruturalmente idêntico — e, portanto, `Equals` — sempre que partem dos mesmos insumos.
