# 5. Estilo, formatação e geometria

Esta seção especifica os tipos primitivos que descrevem **aparência** (`Style` e seus componentes — `Font`, `Color`, `Border`, `Thickness`/`Padding`, alinhamentos, `BackgroundImage`), **formatação de valor** (`Style.Format` + `ValueFormatter`) e **geometria** (`Unit` e as primitivas `Point`/`Size`/`Rectangle`/`Thickness`). Todos vivem em `Reporting.Core` e são consumidos uniformemente por todos os renderers (band, Tablix, KPI, chart) via `StyleResolver`/`ValueFormatter`/`BuildTextStyle`.

Namespaces:
- `Reporting.Styling` — `src/Reporting.Core/Styling/*.cs`
- `Reporting.Geometry` — `src/Reporting.Core/Geometry/*.cs`

Tipos de apoio do estágio de desenho (`Reporting.Rendering` — `TextStyle`/`PenStyle`/`BrushStyle`) também são documentados, pois são a forma "resolvida" (sem `null`, sem `Format`) que efetivamente chega ao device.

---

## 5.1. `Unit` — comprimento device-independent

`Reporting.Geometry.Unit` (`Unit.cs:13`) é o **único** tipo de comprimento do modelo. É um `readonly record struct` que envolve um **inteiro de mils** (1 mil = 1/1000 de polegada).

```csharp
public readonly record struct Unit(int Mils) : IComparable<Unit>;
```

| Campo | Tipo | Default | Semântica |
|-------|------|---------|-----------|
| `Mils` | `int` | — | Comprimento em milésimos de polegada (1 in = 1000 mils). Pode ser negativo. |

| Membro estático | Valor | Semântica |
|-----------------|-------|-----------|
| `Unit.Zero` | `new(0)` | Comprimento nulo. Usado como sentinela em `Thickness.Zero`, `BorderSide.None`, etc. |

### 5.1.1. Por que mils inteiros

A escolha (documentada em `Unit.cs:8-12`) é deliberada e **normativa**: o empilhamento de bandas e o snap-to-grid devem ser **exatos**, sem erro de ponto flutuante acumulado. Como `1 inch = 1000 mils` (exato), `1 pt = 1000/72 mils` e `1 mm = 1000/25.4 mils`, a representação inteira coordena naturalmente com pontos tipográficos (72 dpi), pixels GDI e HiMetric.

**Invariante:** toda aritmética de layout (posição/altura de banda, `EffectiveBandHeight`, paginação) opera sobre `int Mils`, portanto é associativa e reprodutível.

### 5.1.2. Construção (`From*`) e conversão (`To*`)

Os fábricas convertem de unidades físicas para mils com **arredondamento ao inteiro mais próximo** (`Math.Round`, banker's rounding padrão do .NET, `MidpointRounding.ToEven`):

| Fábrica | Fórmula (mils) | Exemplo |
|---------|----------------|---------|
| `FromMm(double mm)` | `round(mm * 1000 / 25.4)` | `FromMm(10)` → `394` mils |
| `FromCm(double cm)` | `FromMm(cm * 10)` | `FromCm(1)` → `394` mils |
| `FromInch(double inch)` | `round(inch * 1000)` | `FromInch(1)` → `1000` mils |
| `FromPoint(double pt)` | `round(pt * 1000 / 72)` | `FromPoint(10)` → `139` mils |
| `FromPixels(double px, double dpi = 96)` | `round(px * 1000 / dpi)` | `FromPixels(96)` → `1000` mils |

As conversões inversas retornam `double` e **não** arredondam:

| Conversor | Fórmula | Exemplo (de `394` mils) |
|-----------|---------|--------------------------|
| `ToMm()` | `Mils * 25.4 / 1000` | `10.0076 mm` |
| `ToCm()` | `ToMm() / 10` | `1.00076 cm` |
| `ToInches()` | `Mils / 1000` | `0.394 in` |
| `ToPoints()` | `Mils * 72 / 1000` | `28.368 pt` |
| `ToPixels(double dpi = 96)` | `Mils * dpi / 1000` | `37.824 px` |

#### Precisão / arredondamento de mm — **normativo**

A conversão mm→mils→mm **não** é round-trip exata, porque mils são inteiros. O erro máximo de quantização é de meio mil em cada direção, ou seja `< 25.4/2000 ≈ 0.0127 mm`. Consequências práticas:

- `Unit.FromMm(10)` armazena `394` mils, e `.ToMm()` devolve `10.0076 mm`, **não** `10.0000`.
- Larguras "redondas" em mm geralmente exibem casas decimais residuais. Ferramentas de UI que mostram mm devem arredondar para exibição (tipicamente 2 casas).
- `ToString()` formata como mm com 2 casas em `InvariantCulture`: `$"{ToMm():F2}mm"` (`Unit.cs:43-44`). Ex.: `new Unit(394).ToString()` → `"10.01mm"`.

### 5.1.3. Operadores e ordenação

`Unit` implementa `IComparable<Unit>` (compara `Mils`) e sobrecarrega aritmética e comparação:

| Operador | Resultado |
|----------|-----------|
| `a + b`, `a - b` | soma/subtração de mils |
| `a * int`, `a * double` | escala; a sobrecarga `double` **arredonda** (`round(Mils * factor)`) |
| `a / int` | divisão **inteira** de mils (trunca; ex. `5 mils / 2 = 2 mils`) |
| `-a` | negação |
| `<`, `>`, `<=`, `>=` | comparação por `Mils` |
| `==`, `!=` | igualdade de valor (gerada pelo `record struct`) |

> Nota: não há operador `/` por `double`, nem `*` comutativo `int * Unit`; o fator vem sempre à direita.

### 5.1.4. `UnitExtensions` — açúcar fluente

Para autoria code-first (`Unit.cs:48-58`):

```csharp
using Reporting.Geometry;

Unit a = 10.Mm();     // FromMm(10)  → 394 mils
Unit b = 2.5.Cm();    // FromCm(2.5)
Unit c = 1.Inch();    // FromInch(1) → 1000 mils
Unit d = 12.Pt();     // FromPoint(12)
```

Métodos: `Mm`/`Cm`/`Inch`/`Pt`, cada um com sobrecarga `int` e `double`.

---

## 5.2. Primitivas geométricas

Em `Reporting.Geometry.Primitives.cs`. Todas são `readonly record struct` cujas coordenadas são `Unit` (device-independent).

### 5.2.1. `Point`

```csharp
public readonly record struct Point(Unit X, Unit Y);
```

| Membro | Tipo/Valor | Semântica |
|--------|-----------|-----------|
| `X`, `Y` | `Unit` | Coordenadas. |
| `Point.Origin` | `(Zero, Zero)` | Origem. |
| `p + Size` / `p - Size` | `Point` | Translada por `(Width, Height)`. |
| `Point - Point` | `Size` | Vetor diferença como `Size`. |

### 5.2.2. `Size`

```csharp
public readonly record struct Size(Unit Width, Unit Height);
```

| Membro | Tipo/Valor | Semântica |
|--------|-----------|-----------|
| `Width`, `Height` | `Unit` | Dimensões. |
| `Size.Empty` | `(Zero, Zero)` | Tamanho nulo. |
| `IsEmpty` | `bool` | `Width == Zero && Height == Zero`. |
| `Size + Size`, `Size - Size` | `Size` | Soma/subtração componente a componente. |

### 5.2.3. `Rectangle`

```csharp
public readonly record struct Rectangle(Unit X, Unit Y, Unit Width, Unit Height);
```

| Membro | Tipo/Valor | Semântica |
|--------|-----------|-----------|
| `X`, `Y`, `Width`, `Height` | `Unit` | Posição do canto superior-esquerdo e dimensões. |
| `Rectangle.Empty` | tudo `Zero` | Retângulo nulo. |
| `Location` | `Point` | `(X, Y)`. |
| `Size` | `Size` | `(Width, Height)`. |
| `Right` | `Unit` | `X + Width`. |
| `Bottom` | `Unit` | `Y + Height`. |
| `FromLocationSize(Point, Size)` | `Rectangle` | Constrói de localização + tamanho. |
| `Contains(Point p)` | `bool` | Teste **inclusivo** nas quatro bordas: `p.X ∈ [X, Right] ∧ p.Y ∈ [Y, Bottom]`. |
| `IntersectsWith(Rectangle other)` | `bool` | Sobreposição AABB (inclusiva nas bordas). |

> O sistema de coordenadas é **Y para baixo** (origem no topo), coerente com layout de página: `Bottom = Y + Height` cresce para baixo.

### 5.2.4. `Thickness` — espaçamento por lado

```csharp
public readonly record struct Thickness(Unit Left, Unit Top, Unit Right, Unit Bottom);
```

Usado para **margens, padding e bordas**. É também o tipo de `Style.Padding`.

| Membro | Tipo/Valor | Semântica |
|--------|-----------|-----------|
| `Left`, `Top`, `Right`, `Bottom` | `Unit` | Espessura por lado. |
| `Thickness.Zero` | tudo `Zero` | Sem espaçamento. **Default efetivo de `Style.Padding`** quando não definido. |
| `Uniform(Unit value)` | `Thickness` | Mesmo valor nos 4 lados. |
| `Symmetric(Unit h, Unit v)` | `Thickness` | `(h, v, h, v)` — horizontal e vertical. |
| `Horizontal` | `Unit` | `Left + Right`. |
| `Vertical` | `Unit` | `Top + Bottom`. |

---

## 5.3. `Color` — cor sRGB com alfa

`Reporting.Styling.Color` (`Color.cs:7`) é um `readonly record struct` de 4 canais byte (0–255).

```csharp
public readonly record struct Color(byte R, byte G, byte B, byte A);
```

| Campo | Tipo | Semântica |
|-------|------|-----------|
| `R`, `G`, `B` | `byte` | Canais sRGB, 0–255. |
| `A` | `byte` | Alfa: `0` = totalmente transparente, `255` = opaco. |

### 5.3.1. Constantes

| Constante | RGBA | Hex |
|-----------|------|-----|
| `Transparent` | `(0,0,0,0)` | — |
| `Black` | `(0,0,0,255)` | `#000000` |
| `White` | `(255,255,255,255)` | `#FFFFFF` |
| `Red` | `(255,0,0,255)` | `#FF0000` |
| `Green` | `(0,128,0,255)` | `#008000` |
| `Blue` | `(0,0,255,255)` | `#0000FF` |
| `Gray` | `(128,128,128,255)` | `#808080` |
| `LightGray` | `(211,211,211,255)` | `#D3D3D3` |

> Atenção: `Green` é o verde "web/CSS" (`#008000`), **não** `#00FF00` (este é `lime`/`Color.FromName("lime")`).

### 5.3.2. Construção

| Fábrica | Semântica |
|---------|-----------|
| `FromRgb(byte r, byte g, byte b)` | Alfa fixado em `255`. |
| `FromArgb(byte a, byte r, byte g, byte b)` | Ordem de parâmetros A-R-G-B; armazena como `(r,g,b,a)`. |
| `FromHex(string hex)` | Veja 5.3.3. |
| `FromName(string? name)` | Veja 5.3.4. |

### 5.3.3. `FromHex` / `ToHex` — round-trip textual

`FromHex` (`Color.cs:23`) aceita **apenas** dois comprimentos (após remover um `#` inicial opcional via `TrimStart('#')`), em `HexNumber`/`InvariantCulture`:

| Entrada | Interpretação |
|---------|---------------|
| 6 dígitos `RRGGBB` | `A = 255` (opaco). |
| 8 dígitos `AARRGGBB` | Alfa **primeiro** (ordem ARGB). |
| qualquer outro comprimento | lança `FormatException` (`"Invalid color hex literal ..."`). |
| `hex == null` | lança `ArgumentNullException`. |

`ToHex` (`Color.cs:43`) é o inverso e **omite o alfa quando opaco**:

- `A == 255` → `"#RRGGBB"` (6 dígitos).
- `A != 255` → `"#AARRGGBB"` (8 dígitos).

`ToString()` delega para `ToHex()`. Dígitos hex são sempre **maiúsculos** (`X2`).

```csharp
Color.FromHex("#1E90FF").ToHex();      // "#1E90FF"
Color.FromHex("#801E90FF").ToHex();    // "#801E90FF" (alfa 0x80)
new Color(30,144,255,255).ToString();  // "#1E90FF"
```

> Invariante de round-trip: `FromHex(c.ToHex()) == c` para todo `c`. (Cores opacas perdem o `FF` na serialização e o recuperam na leitura.)

### 5.3.4. `FromName` — paleta nomeada CSS3 / RDL

`FromName(string? name)` (`Color.cs:55`) resolve um nome de cor **case-insensitive** (com `Trim`) para uma `Color`, ou retorna `null` quando o nome é desconhecido (`name == null` também ⇒ `null`).

- A tabela `NamedColors` (`Color.cs:60-113`) é a paleta **CSS3 / X11** completa — o mesmo conjunto que `System.Drawing.KnownColor` e o RDL usam (≈148 nomes).
- Comparação via `StringComparer.OrdinalIgnoreCase`.
- Inclui `"transparent"` mapeado para `Color.Transparent`.
- Ambas as grafias **`gray` e `grey`** são aceitas (e variantes: `darkgray`/`darkgrey`, `dimgray`/`dimgrey`, `lightgray`/`lightgrey`, `slategray`/`slategrey`, etc.).
- Inclui `rebeccapurple` (`#663399`).

Esta tabela é **compartilhada** (e normativa) entre o importador RDL, os readers repx/repjson e a coerção de binding de expressão — todos concordam nos mesmos nomes (ver doc-comment em `Color.cs:50-54`). Literais `#hex` passam por `FromHex`, não por `FromName`.

```csharp
Color.FromName("CornflowerBlue");  // (100,149,237,255)
Color.FromName("grey");            // (128,128,128,255)  — = "gray"
Color.FromName("notacolor");       // null
```

---

## 5.4. `Font` e `FontStyle`

`Reporting.Styling.Font` (`Font.cs:14`) é um **descritor lógico** de fonte; é resolvido para uma fonte de plataforma pelo renderer/`ITextMeasurer`.

```csharp
public sealed record Font(string Family, double Size, FontStyle Style = FontStyle.Regular);
```

| Campo | Tipo | Default | Semântica |
|-------|------|---------|-----------|
| `Family` | `string` | — (obrigatório) | Nome da família (ex. `"Arial"`). Não há resolução de fallback no modelo; é responsabilidade do renderer. |
| `Size` | `double` | — (obrigatório) | Tamanho em **pontos** tipográficos (pt). É `double`, não `Unit`. |
| `Style` | `FontStyle` | `FontStyle.Regular` | Combinação de flags (negrito/itálico/...). |

| Membro | Valor | Semântica |
|--------|-------|-----------|
| `Font.Default` | `new("Arial", 10)` | Fonte default do sistema, usada quando `Style.Font == null`. |
| `WithSize(double)` | `Font` | Cópia com novo `Size`. |
| `WithStyle(FontStyle)` | `Font` | Cópia **substituindo** o `Style`. |
| `AddStyle(FontStyle)` | `Font` | Cópia com `Style | style` (acumula flags). |

### 5.4.1. `FontStyle` (enum `[Flags]`)

`Font.cs:3`:

| Valor | Bit | Semântica |
|-------|-----|-----------|
| `Regular` | `0` | Sem decorações. |
| `Bold` | `1 << 0` (1) | Negrito. |
| `Italic` | `1 << 1` (2) | Itálico. |
| `Underline` | `1 << 2` (4) | Sublinhado. |
| `Strikeout` | `1 << 3` (8) | Tachado. |

Combinável por OR: `FontStyle.Bold | FontStyle.Italic` (valor `3`).

---

## 5.5. Borda — `Border`, `BorderSide`, `BorderLineStyle`

`Reporting.Styling.BorderStyle.cs`.

### 5.5.1. `BorderLineStyle` (enum)

`Alignment.cs:18` (declarado junto aos alinhamentos):

| Valor | Semântica |
|-------|-----------|
| `None` | Sem linha (borda invisível). |
| `Solid` | Linha contínua. |
| `Dashed` | Tracejada. |
| `Dotted` | Pontilhada. |
| `DashDot` | Traço-ponto. |
| `Double` | Linha dupla. |

### 5.5.2. `BorderSide` — um lado da borda

```csharp
public sealed record BorderSide(BorderLineStyle Style, Unit Thickness, Color Color);
```

| Campo | Tipo | Semântica |
|-------|------|-----------|
| `Style` | `BorderLineStyle` | Estilo da linha. |
| `Thickness` | `Unit` | Espessura. |
| `Color` | `Color` | Cor. |

| Membro | Valor | Semântica |
|--------|-------|-----------|
| `BorderSide.None` | `(None, Unit.Zero, Color.Transparent)` | Lado ausente. |
| `IsVisible` | `bool` | `Style != None && Thickness > Unit.Zero`. **Borda só pinta se este predicado for verdadeiro.** |

### 5.5.3. `Border` — quatro lados

```csharp
public sealed record Border(BorderSide Left, BorderSide Top, BorderSide Right, BorderSide Bottom);
```

| Membro | Valor | Semântica |
|--------|-------|-----------|
| `Border.None` | quatro lados `BorderSide.None` | Sem borda. |
| `Uniform(BorderSide side)` | `Border` | Mesmo lado nas quatro arestas. |
| `Uniform(BorderLineStyle, Unit, Color)` | `Border` | Conveniência: constrói o `BorderSide` e replica. |

### 5.5.4. Limitação de renderização de borda — **normativo**

Embora `Border` modele **quatro lados independentes**, o renderer de banda atual (`ResolveBorderPen`, `BandRenderer.cs:627-636`) usa **apenas o lado `Top` como representante** para retângulos/elipses, convertendo-o em um único `PenStyle` via `PenStyle.FromBorderSide(border.Top)` (ver 5.9). Ou seja: na renderização atual a borda é tratada como **uniforme** (todos os lados desenhados com o estilo do `Top`); bordas por-lado assimétricas são preservadas no modelo e na serialização, mas **não** distinguidas no desenho. Isto é uma limitação de implementação, não do modelo.

---

## 5.6. `HorizontalAlignment` / `VerticalAlignment`

`Alignment.cs`.

```csharp
public enum HorizontalAlignment { Left, Center, Right, Justify }
public enum VerticalAlignment   { Top, Middle, Bottom }
```

| Enum | Valores | Default em `Style` |
|------|---------|--------------------|
| `HorizontalAlignment` | `Left`, `Center`, `Right`, `Justify` | `Left` |
| `VerticalAlignment` | `Top`, `Middle`, `Bottom` | `Top` |

---

## 5.7. `BackgroundImage`

`Reporting.Styling.BackgroundImage` (`BackgroundImage.cs:7`) — imagem pintada **atrás do conteúdo** do elemento, mapeando o `<Style><BackgroundImage>` do RDL.

```csharp
public sealed record BackgroundImage(string? Path = null, string? Expression = null);
```

| Campo | Tipo | Default | Semântica |
|-------|------|---------|-----------|
| `Path` | `string?` | `null` | Fonte estática **External**: caminho de arquivo ou URL. |
| `Expression` | `string?` | `null` | Expressão por-linha que produz bytes ou caminho. |
| `IsExpression` | `bool` (computado) | — | `!string.IsNullOrEmpty(Expression)` — verdadeiro ⇒ usa a expressão; caso contrário usa `Path`. |

### 5.7.1. Estado de implementação — **normativo**

O doc-comment (`BackgroundImage.cs:3-6`) declara: **Phase B** suporta a fonte **External** (caminho estático ou expressão por-linha), **esticada às bounds** (`ImageSizing.Stretch`). **Não** implementados ainda (phase C): imagens embutidas (embedded), tiling (`BackgroundRepeat`) e tamanho máximo.

No render (`BandRenderer.cs:188-205`), a `BackgroundImage` é pintada **depois** do `BackColor` (portanto por cima dele) e **antes** do conteúdo, esticada às bounds do elemento; cresce/encolhe junto com a banda quando `CanGrow`/`CanShrink` alteram a altura final (`BandRenderer.cs:230-233`). A resolução de bytes (`ResolveBackgroundBytes`, `BandRenderer.cs:671-672`) segue: expressão por-linha tem precedência sobre `Path`.

---

## 5.8. `Style` — o estilo agregado

`Reporting.Styling.Style` (`Style.cs:12`) é o estilo visual aplicado a um `ReportElement`. **Todas** as propriedades são opcionais; um valor não definido (`null`) significa **"herdar do pai ou usar o default do renderer"** (`Style.cs:6-7`).

```csharp
public sealed record Style(
    Font? Font = null,
    Color? ForeColor = null,
    Color? BackColor = null,
    Border? Border = null,
    Thickness? Padding = null,
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    bool WordWrap = true,
    string? Format = null,
    BackgroundImage? BackgroundImage = null);
```

| Campo | Tipo | Default | "inherit"? | Semântica |
|-------|------|---------|------------|-----------|
| `Font` | `Font?` | `null` | sim (null) | Fonte do texto. Fallback de render: `Font.Default` (`Arial 10`). |
| `ForeColor` | `Color?` | `null` | sim (null) | Cor do texto. Fallback de render: `Color.Black`. **Bindable.** |
| `BackColor` | `Color?` | `null` | sim (null) | Cor de fundo. Sem fundo quando `null` (nenhum retângulo de fill é emitido — `BandRenderer.cs:177`). **Bindable.** |
| `Border` | `Border?` | `null` | sim (null) | Borda. Sem pen quando `null`. Render usa só o lado `Top` (5.5.4). |
| `Padding` | `Thickness?` | `null` | sim (null) | Espaçamento interno do texto. Fallback de render: `Thickness.Zero`. |
| `HorizontalAlignment` | `HorizontalAlignment` | `Left` | **não** (valor, nunca null) | Alinhamento horizontal do texto. |
| `VerticalAlignment` | `VerticalAlignment` | `Top` | **não** | Alinhamento vertical do texto. |
| `WordWrap` | `bool` | `true` | **não** | Quebra automática de linha. |
| `Format` | `string?` | `null` | sim (null) | Spec de formatação de valor (ver 5.10). **Bindable.** |
| `BackgroundImage` | `BackgroundImage?` | `null` | sim (null) | Imagem de fundo (5.7). |

| Membro | Valor | Semântica |
|--------|-------|-----------|
| `Style.Default` | `new()` | Estilo "tudo inherit": campos nullable em `null`, alinhamentos em `Left`/`Top`, `WordWrap = true`. **É o valor inicial de `ReportElement.Style`** (`ReportElement.cs:25`). |

### 5.8.1. Distinção valor-vs-inherit — **normativo**

Os campos **nullable** (`Font`, `ForeColor`, `BackColor`, `Border`, `Padding`, `Format`, `BackgroundImage`) usam `null` como sinal de **herdar**. Os campos **não-nullable de layout** (`HorizontalAlignment`, `VerticalAlignment`, `WordWrap`) **sempre** carregam um valor concreto e, portanto, **não distinguem "não definido" de "default"**. Isto tem consequência direta na sobreposição de estilos (5.9) e em serialização: um `WordWrap = true` explícito é indistinguível do default.

> Observação de fidelidade (memória do projeto): o Designer/VM **materializa** alguns campos no load (ex. normaliza `ForeColor = Black` e `Font = Arial-10`), perdendo o estado `null`/"inherit". É comportamento pré-existente do VM, fora do escopo deste modelo de dados.

### 5.8.2. Metadados de PropertyGrid

Cada propriedade escalar/enum de `Style` carrega `[PropertyGrid(...)]` (`Style.cs:13-30`) para ser **achatada** (flattened) na grade de metadados do elemento quando este expõe seu `Style` via `[PropertyGrid(Nested = true)]`. `Font`, `Border` e `Padding` **não** são achatados (são records complexos com editores dedicados: `"font"`, `"border"`, `"padding"`). `ForeColor`, `BackColor`, `WordWrap` e `Format` são marcados `Bindable = true` (podem ser dirigidos por expressão). Estes atributos são **puro metadado de designer**: não participam de construção, render nem serialização (ver `PropertyGridAttribute.cs:12-13`).

---

## 5.9. Resolução de estilo e formatação condicional — `StyleResolver`

`Reporting.Layout.Internal.StyleResolver` (`StyleResolver.cs`) é **compartilhado por todos os renderers** (band, Tablix, KPI) para que a formatação condicional se comporte identicamente em qualquer componente.

### 5.9.1. `ConditionalFormat`

`Reporting.Styling.ConditionalFormat` (`ConditionalFormat.cs:5`):

```csharp
public sealed record ConditionalFormat(string Condition, Style Style);
```

| Campo | Tipo | Semântica |
|-------|------|-----------|
| `Condition` | `string` | Expressão booleana avaliada no contexto da linha atual. |
| `Style` | `Style` | Estilo a sobrepor quando `Condition` é verdadeira. |

Um `ReportElement` carrega um `EquatableArray<ConditionalFormat> ConditionalFormats` (default `Empty`, `ReportElement.cs:28`).

### 5.9.2. `Resolve` — sobreposição em ordem de declaração

`StyleResolver.Resolve(element, evaluator, ctx)` (`StyleResolver.cs:16`):

1. Começa com `element.Style`.
2. Para cada `cf` em `element.ConditionalFormats` **na ordem de declaração**: se `evaluator.Evaluate<bool>(cf.Condition, ctx)` for `true`, faz `style = Merge(style, cf.Style)`.
3. Retorna o estilo efetivo acumulado.

> Múltiplos formatos condicionais verdadeiros são aplicados **em cascata** (o último vence, campo a campo, segundo as regras de `Merge`).

### 5.9.3. `Merge` — regras de sobreposição — **normativo**

`Merge(baseStyle, overlay)` (`StyleResolver.cs:33-45`) combina dois estilos com **duas regras distintas**:

- **Campos nullable** (`Font`, `ForeColor`, `BackColor`, `Border`, `Padding`, `Format`): o overlay vence **somente se definido** — `overlay.X ?? baseStyle.X`. Um `null` no overlay preserva o valor base.
- **Campos de layout** (`HorizontalAlignment`, `VerticalAlignment`, `WordWrap`): o overlay **sempre** vence — o valor do overlay é tomado incondicionalmente.

Esta assimetria é consequência direta de 5.8.1: como os campos de layout não têm estado "inherit", o overlay não consegue dizer "não mexa"; portanto ele sempre sobrescreve.

```
Merge(base, overlay):
  Font     = overlay.Font     ?? base.Font
  ForeColor= overlay.ForeColor?? base.ForeColor
  BackColor= overlay.BackColor?? base.BackColor
  Border   = overlay.Border   ?? base.Border
  Padding  = overlay.Padding  ?? base.Padding
  Format   = overlay.Format   ?? base.Format
  HAlign   = overlay.HorizontalAlignment   // sempre
  VAlign   = overlay.VerticalAlignment      // sempre
  WordWrap = overlay.WordWrap               // sempre
```

> **Cuidado de autoria:** um `ConditionalFormat` cujo `Style` é `Style.Default` (alinhamentos `Left`/`Top`, `WordWrap=true`) **vai** forçar esses valores de layout no resultado quando a condição disparar, mesmo que a intenção fosse mudar só a cor.

---

## 5.10. Formatação de valor — `Style.Format` e `ValueFormatter`

### 5.10.1. `ValueFormatter`

`Reporting.Expressions.ValueFormatter.Format(object? value, string? format, CultureInfo? culture = null)` (`ValueFormatter.cs:9`) é o **ponto único** de formatação de valor de todo o sistema. Algoritmo:

| Caso | Resultado |
|------|-----------|
| `culture == null` | usa `CultureInfo.CurrentCulture`. |
| `value == null` | retorna `string.Empty`. |
| `format` vazio/null **e** `value is IFormattable` | `value.ToString(null, culture)`. |
| `format` vazio/null **e** não-`IFormattable` | `Convert.ToString(value, culture) ?? ""`. |
| `format` definido **e** `value is IFormattable` | `value.ToString(format, culture)`. |
| `format` definido **e** não-`IFormattable` | `string.Format(culture, "{0:" + format + "}", value)`. |

O `format` é uma **format string padrão do .NET** (numérica: `"C"`, `"N2"`, `"P"`, `"#,##0.00"`; data: `"d"`, `"yyyy-MM-dd"`; etc.), aplicada respeitando a cultura.

### 5.10.2. `Style.Format` aplicado em TODOS os renderers — **normativo**

`Style.Format` (o campo, após resolução condicional via `StyleResolver`) é honrado por **todo** renderer que exibe valores, sempre via `ValueFormatter.Format(valor, style.Format, ctx.Culture)`:

| Renderer | Local | Uso |
|----------|-------|-----|
| Banda (`TextBoxElement`) | `BandRenderer.cs:222`, `:505` | `ResolveTextBoxText(tb, ctx, effectiveStyle.Format)` formata o valor do textbox. |
| Tablix (células) | `TablixRenderer.cs:667` | `ValueFormatter.Format(ev.Evaluate(...), elementFormat, ctx.Culture)`. |
| KPI | `KpiRenderer.cs:128`, `:178`, `:486` | `FormatValue(val, el.Style.Format, culture)`. |
| Chart (rótulos de eixo/valor) | `ChartRenderer.cs:258`, `:674` | `FormatValue(v, chart.Style.Format, culture)`. |
| Templates `{expr:format}` | `TemplateRenderer.cs:121`, `ExpressionEvaluator.cs:352` | Mesmo `ValueFormatter`. |

**Invariante:** qualquer componente novo que exiba valores **deve** formatar via `ValueFormatter` honrando `Style.Format` resolvido — esta é a fonte única, garantindo consistência entre band/Tablix/KPI/chart.

Diferença importante entre `Style.Format` (a spec de formatação de **valor**) e o `TextStyle` (estilo de **desenho**): o `TextStyle` que chega ao device **não** carrega `Format` — ele é consumido **antes**, ao converter o valor em string (ver doc-comment `BandRenderer.cs:638-639`).

### 5.10.3. Persistência de `Format`

Round-trip nos três formatos, sempre só quando presente (não-null/não-vazio):

| Formato | Local | Forma |
|---------|-------|-------|
| RDL | `RdlWriter.cs:1169-1171` | `<Format>` (filho de `<Style>`). |
| repjson | `RepJsonWriter.cs:776-778` | chave `"format"`. |
| repx | `RepxWriter.cs:625-627` | atributo `Format`. |

---

## 5.11. Tipos de desenho resolvidos — `TextStyle`, `PenStyle`, `BrushStyle`

`Reporting.Rendering.Styles.cs` define a forma **resolvida** (sem `null`, sem `Format`) que efetivamente chega ao backend de desenho. A conversão acontece em `BandRenderer.BuildTextStyle` / `ResolveBorderPen` / construção de `BrushStyle`.

### 5.11.1. `TextStyle`

```csharp
public sealed record TextStyle(
    Font Font, Color ForeColor,
    HorizontalAlignment HorizontalAlignment = HorizontalAlignment.Left,
    VerticalAlignment VerticalAlignment = VerticalAlignment.Top,
    bool WordWrap = true, Thickness Padding = default);
```

| Membro | Valor/Default | Semântica |
|--------|---------------|-----------|
| `Font` | obrigatório | Fonte concreta (já com fallback resolvido). |
| `ForeColor` | obrigatório | Cor concreta. |
| `TextStyle.Default` | `new(Font.Default, Color.Black)` | `Arial 10`, preto. |
| `WithFont`/`WithColor` | — | Cópias. |

**Mapeamento `Style` → `TextStyle`** (`BuildTextStyle`, `BandRenderer.cs:643-650`) — é aqui que os fallbacks de "inherit" se materializam:

| `Style` (origem) | `TextStyle` (destino) | Fallback se `null` |
|------------------|------------------------|--------------------|
| `Font` | `Font` | `Font.Default` |
| `ForeColor` | `ForeColor` | `Color.Black` |
| `HorizontalAlignment` | `HorizontalAlignment` | (sempre presente) |
| `VerticalAlignment` | `VerticalAlignment` | (sempre presente) |
| `WordWrap` | `WordWrap` | (sempre presente) |
| `Padding` | `Padding` | `Thickness.Zero` |

> Note que `BackColor`, `Border`, `Format` e `BackgroundImage` **não** entram no `TextStyle`: são tratados em estágios separados do render (fill de retângulo, pen, formatação de valor e imagem de fundo, respectivamente).

### 5.11.2. `PenStyle`

```csharp
public sealed record PenStyle(Color Color, Unit Thickness, BorderLineStyle Style = BorderLineStyle.Solid);
```

| Membro | Valor | Semântica |
|--------|-------|-----------|
| `PenStyle.Default` | `(Black, 0.5pt)` | Pen padrão. |
| `PenStyle.Thin` | `(Black, 0.25pt)` | Pen fino. |
| `IsVisible` | `bool` | `Style != None && Thickness > Unit.Zero`. |
| `FromBorderSide(BorderSide side)` | `PenStyle?` | `side.IsVisible ? new(side.Color, side.Thickness, side.Style) : null` — converte um lado de borda em pen, ou `null` quando o lado é invisível. |

`ResolveBorderPen` (`BandRenderer.cs:627`) usa `FromBorderSide(element.Style.Border.Top)`; retorna `null` quando `Border` é `null` ou o lado superior é invisível (5.5.4).

### 5.11.3. `BrushStyle`

```csharp
public sealed record BrushStyle(Color Color);
```

| Membro | Valor | Semântica |
|--------|-------|-----------|
| `BrushStyle.Black`/`White`/`Transparent` | cores correspondentes | Constantes. |
| `IsVisible` | `bool` | `Color.A > 0` (alfa positivo). |

Atualmente **apenas preenchimento sólido**; gradientes/padrões são reservados para o futuro (doc-comment `Styles.cs:36`). Usado para o fill de `BackColor` (`new BrushStyle(backColor)`, `BandRenderer.cs:184`) e fills de formas (`RectangleElement.FillColor`, `EllipseElement.FillColor`).

---

## 5.12. Resumo de defaults e invariantes

- **Unidade única:** todo comprimento é `Unit` (mils inteiros). Aritmética de layout é exata; conversões mm sofrem quantização ≤ 0.0127 mm.
- **`Style.Default` = tudo inherit:** nullable em `null`, `HorizontalAlignment=Left`, `VerticalAlignment=Top`, `WordWrap=true`. É o estilo inicial de todo elemento.
- **Fallbacks de render** (quando `null`): `Font.Default` (Arial 10), `ForeColor=Black`, `Padding=Zero`, `BackColor`→sem fundo, `Border`→sem pen.
- **`Merge`/condicional:** nullable usa `??` (overlay-se-definido); layout sobrescreve sempre.
- **`Format` é universal:** honrado por band/Tablix/KPI/chart/templates via `ValueFormatter` único; consumido antes do `TextStyle` (que não o carrega).
- **`Color` round-trip:** `FromHex(c.ToHex()) == c`; `ToHex` omite alfa quando opaco; `FromName` compartilha a paleta CSS3/RDL (gray/grey ambos).
- **Limitação atual:** bordas por-lado são modeladas mas o render usa só o lado `Top`; `BackgroundImage` só suporta fonte External esticada (phase B).
