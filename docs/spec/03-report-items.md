# 3. Report items (ReportElement)

Esta seção especifica os *report items* do modelo OmniReport: o tipo-base abstrato `ReportElement` e cada subtipo concreto. Todos vivem no namespace `Reporting.Elements` (`src/Reporting.Core/Elements/*.cs`). Um *report item* é um elemento visual posicionado dentro de uma banda (`Band`); a banda fornece o sistema de coordenadas e o contexto de dados, o elemento descreve o que desenhar.

Convenções desta seção:

- Todos os tipos são `sealed record` (subtipos) ou `abstract record` (a base). Por serem `record`, são **imutáveis**: as propriedades usam `init` e a mutação se dá via expressão `with`. A igualdade é estrutural (por valor).
- Coleções e mapas usam os tipos value-equality `EquatableArray<T>` e `EquatableDictionary<K,V>` (namespace `Reporting.Common`) — necessários para que a igualdade estrutural do `record` funcione sobre coleções (igualdade elemento-a-elemento, não por referência). O default de toda coleção é a instância vazia (`.Empty`), nunca `null`.
- As anotações `[TextStyled]` e `[PropertyGrid]` (namespace `Reporting.Metadata`) são **metadados lidos apenas pelo designer**; não participam de construção, render ou serialização (`TextStyledAttribute.cs:10-11`, `PropertyGridAttribute.cs:12-13`). São documentadas aqui apenas onde ajudam a entender a semântica de edição.

## 3.1. Tipo-base: `ReportElement`

`abstract record ReportElement` (`ReportElement.cs:8-66`) define os campos comuns a **todos** os elementos. Os subtipos herdam estes campos e adicionam os seus próprios.

| Campo | Tipo CLR | Default | Semântica |
|---|---|---|---|
| `Id` | `string` | `Guid.NewGuid().ToString("n")` | Identificador estável (32 hex, sem hífens) gerado por instância. Usado pelo designer e pela serialização para referenciar o elemento. Não há garantia de unicidade imposta pelo modelo — o produtor deve mantê-la. |
| `Name` | `string?` | `null` | Nome opcional, exibido na árvore de outline do designer. Puramente descritivo; não é chave. |
| `Bounds` | `Rectangle` | `default` ( = `Rectangle.Empty`, todos os campos `Unit.Zero`) | Posição e tamanho **relativos ao canto superior-esquerdo da banda** (ou do `RectangleElement` pai, quando aninhado — ver §3.6). Ver §3.1.1. |
| `Visible` | `bool` | `true` | Flag de visibilidade estática. Combinada com `VisibleExpression` em runtime. |
| `VisibleExpression` | `string?` | `null` | Expressão que, se não-vazia, deve avaliar para `true` para o elemento renderizar. Combina com `Visible` (ambos devem permitir a renderização). |
| `Style` | `Style` | `Style.Default` (todos os campos nulos/herdados) | Estilo visual (fonte, cores, bordas, alinhamento, formato). Ver §3.1.2. |
| `ConditionalFormats` | `EquatableArray<ConditionalFormat>` | `Empty` | Regras de formatação condicional avaliadas **em ordem**; cada regra que casa sobrepõe (layer) o seu `Style` sobre o `Style` base. Ver §3.1.3. |
| `PropertyExpressions` | `EquatableDictionary<string,string>` | `Empty` | Bindings por-propriedade estilo SSRS: *path da propriedade* → *expressão* avaliada por instância em render time, cujo resultado sobrescreve o valor estático. Ver §3.1.4. |
| `Bookmark` | `string?` | `null` | RDL `<Bookmark>`: âncora de navegação única que ações `BookmarkLink` de outros elementos podem alvejar (destino nomeado em PDF, `id` em HTML). |
| `DocumentMapLabel` | `string?` | `null` | RDL `<Label>`: quando não-nulo, o elemento contribui uma entrada para o Document Map (índice/TOC) exibido por viewers interativos. A hierarquia é derivada da estrutura de bandas. |
| `Action` | `ElementAction?` | `null` | RDL `<Action>`: no máximo **uma** ação por elemento. `null` = elemento não-interativo. Ver §3.13. |
| `ToggleItemId` | `string?` | `null` | RDL `<Visibility><ToggleItem>`: quando não-nulo, o elemento identificado por este `Id` (que deve ser um TextBox com `Bookmark` correspondente) renderiza um chevron expand/collapse que alterna a visibilidade **deste** elemento. Drill-down. |
| `InitiallyHidden` | `bool` | `false` | Quando `ToggleItemId` está setado, controla se o elemento renderiza aberto (`false`) ou recolhido (`true`) inicialmente. Espelha RDL `<Hidden>` sob `<Visibility>`. |

> **Nota de implementação (drill-down).** `ToggleItemId`/`InitiallyHidden` fazem parte do modelo e do round-trip; o suporte de render real para o toggle está marcado como tarefa pendente ("Drill-down / toggle de visibilidade"). O par `Bookmark`/`Action(BookmarkLink)` e `DocumentMapLabel` são consumidos pelos renderers interativos.

### 3.1.1. `Bounds` (`Rectangle`)

`readonly record struct Rectangle(Unit X, Unit Y, Unit Width, Unit Height)` (`Geometry/Primitives.cs:25`). Coordenadas e dimensões são `Unit` — comprimento device-independent armazenado como **número inteiro de mils (1/1000 de polegada)** (`Geometry/Unit.cs:13`). Inteiros garantem que a matemática de empilhamento de bandas seja exata e o snap-to-grid trivial.

Construtores de `Unit`: `FromMm`, `FromCm`, `FromInch`, `FromPoint` (1pt = 1/72"), `FromPixels(px, dpi=96)`. Conversões simétricas `ToMm/ToCm/ToInches/ToPoints/ToPixels`. Há helpers fluentes `.Mm() / .Cm() / .Inch() / .Pt()` (`UnitExtensions`). `Rectangle` expõe `Right = X + Width`, `Bottom = Y + Height`, `Location`, `Size`, e os testes `Contains(Point)` / `IntersectsWith(Rectangle)`.

### 3.1.2. `Style`

`sealed record Style` (`Styling/Style.cs:12-35`). Todas as propriedades são opcionais — **`null` significa "herdar do pai ou usar o default do renderer"**.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Font` | `Font?` | `null` | Descritor lógico de fonte `Font(string Family, double Size, FontStyle Style)`. `Font.Default = ("Arial", 10, Regular)`. `FontStyle` é `[Flags]`: `Regular=0, Bold, Italic, Underline, Strikeout`. |
| `ForeColor` | `Color?` | `null` | Cor do texto/traço. |
| `BackColor` | `Color?` | `null` | Cor de fundo. |
| `Border` | `Border?` | `null` | Quatro `BorderSide(BorderLineStyle Style, Unit Thickness, Color Color)`. `BorderLineStyle`: `None, Solid, Dashed, Dotted, DashDot, Double`. |
| `Padding` | `Thickness?` | `null` | Espaçamento interno por lado (`Left/Top/Right/Bottom` em `Unit`). |
| `HorizontalAlignment` | `HorizontalAlignment` | `Left` | Valores: `Left, Center, Right, Justify`. |
| `VerticalAlignment` | `VerticalAlignment` | `Top` | Valores: `Top, Middle, Bottom`. |
| `WordWrap` | `bool` | `true` | Quebra de linha automática. |
| `Format` | `string?` | `null` | String de formato (.NET / SSRS-style) aplicada ao valor antes de desenhar. Honrada por **todos** os renderers (band/Tablix/KPI/chart) via o `ValueFormatter` compartilhado. |
| `BackgroundImage` | `BackgroundImage?` | `null` | Imagem de fundo (`Path` ou `Expression`). Editor dedicado; não achatado no grid. |

`Color` é `readonly record struct Color(byte R, byte G, byte B, byte A)` sRGB (`Styling/Color.cs:7`). Helpers: `FromRgb`, `FromArgb`, `FromHex("#RRGGBB"|"#AARRGGBB")`, `ToHex()`, e `FromName(...)` que resolve a paleta CSS3/`KnownColor` completa (case-insensitive, aceita `gray`/`grey`). Constantes: `Transparent, Black, White, Red, Green, Blue, Gray, LightGray`.

> **Nota (designer materializa Style).** O ViewModel do designer normaliza `ForeColor=Black`/`Font=Arial-10` ao carregar, perdendo a semântica de `null` ("inherit"). Isso é comportamento do designer, não do modelo: no modelo, `Style.Default` mantém todos os campos `null`.

### 3.1.3. `ConditionalFormat`

`sealed record ConditionalFormat(string Condition, Style Style)` (`Styling/ConditionalFormat.cs:5`). Quando `Condition` avalia para `true` no contexto da linha corrente, o `Style` da regra é **sobreposto** (layered) sobre o estilo-base do elemento. As regras de `ReportElement.ConditionalFormats` são avaliadas em ordem; múltiplas regras que casam acumulam.

### 3.1.4. `PropertyExpressions` (binding por-propriedade)

Mapa *path → expressão*. O path pode ser raiz (`"Direction"`, `"FillColor"`) ou **aninhado** (`"Style.ForeColor"`, `"Style.Font.Size"`, `"Bounds.Width"`). Em render time, por instância, a expressão é avaliada e o resultado sobrescreve o valor estático da propriedade; o valor estático é o **fallback** quando não há binding ou a expressão falha. É independente de — e aplicado **antes** de — `ConditionalFormats`; ambos continuam funcionando. Autores code-first setam valores estáticos diretamente e só adicionam bindings quando desejado. No designer, propriedades marcadas `[PropertyGrid(Bindable = true)]` ganham um toggle `fx` que grava no `PropertyExpressions` sob o path da propriedade (`PropertyGridAttribute.cs:41-46`).

### 3.1.5. Invariantes do tipo-base

- `Id` nunca é `null` nem vazio (sempre inicializado).
- Coleções (`ConditionalFormats`, `PropertyExpressions`) nunca são `null`; o vazio é a instância `.Empty`.
- `Action` é `null` **ou** exatamente uma das três variantes (ver §3.13).
- `InitiallyHidden` só tem efeito quando `ToggleItemId != null`.
- `Bounds` é relativo à banda (ou ao `RectangleElement` pai quando o elemento é filho aninhado — §3.6).

## 3.2. `LabelElement` — rótulo literal

`sealed record LabelElement : ReportElement` (`TextElements.cs:8-12`). Marcado `[TextStyled]`. Texto literal, **sem expressões e sem data-binding**.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Text` | `string` | **`required`** (sem default) | Conteúdo literal a desenhar. Obrigatório no construtor (`required`). |

Exemplo (code-first):

```csharp
var titulo = new LabelElement
{
    Text   = "Relatório de Vendas",
    Bounds = new Rectangle(Unit.Zero, Unit.Zero, 80.Mm(), 8.Mm()),
    Style  = Style.Default with
    {
        Font = new Font("Arial", 14, FontStyle.Bold),
        HorizontalAlignment = HorizontalAlignment.Center,
    },
};
```

## 3.3. `TextBoxElement` — célula de texto data-bound

`sealed record TextBoxElement : ReportElement` (`TextElements.cs:33-48`). Marcado `[TextStyled]`. É a célula de texto com expressão/template e (opcionalmente) *runs* formatados.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Expression` | `string` | **`required`** | Expressão crua (`Fields.Total`) **ou** template com placeholders `{expr:format}`. Resolvido pelo engine em render time. |
| `CanGrow` | `bool` | `false` | Se `true`, o elemento cresce verticalmente para caber o conteúdo quebrado. |
| `CanShrink` | `bool` | `false` | Se `true`, encolhe verticalmente quando o conteúdo é mais curto que `Bounds`. (Opt-in; o motor de paginação trata como shrink-safe.) |
| `TextRuns` | `EquatableArray<TextRun>` | `Empty` | RDL `<TextRuns>` — lista ordenada de segmentos formatados. **Vazio = modo single-expression (legado)**: o renderer usa apenas `Expression`. |

### 3.3.1. `TextRun`

`sealed record TextRun(string Value, Style? Style = null, ElementAction? Action = null)` (`TextElements.cs:64`). Um segmento formatado dentro do TextBox.

| Param | Tipo | Default | Semântica |
|---|---|---|---|
| `Value` | `string` | (posicional, obrigatório) | Expressão ou literal; templates resolvidos como em `TextBoxElement.Expression`. |
| `Style` | `Style?` | `null` | Override de estilo. `null` = herda o estilo do TextBox pai. Quando não-nulo, faz merge **aditivo**: só os campos providos sobrescrevem; campos `null` do override são ignorados. |
| `Action` | `ElementAction?` | `null` | Ação inline — transforma este run em hyperlink / bookmark jump / drillthrough, independente dos demais runs. Espelha o `<ActionInfo>` por-run do RDL. |

**Status de render (importante para a spec).** O renderer concatena todos os `TextRun` em ordem de declaração e os desenha com o `Style` do próprio TextBox; overrides de estilo por-run e ações inline **fazem round-trip** em `.repx`/`.repjson` mas são tratados uniformemente até o caminho de desenho multi-fonte aterrissar (`TextElements.cs:27-31`). Contrato garantido: um relatório salvo com `TextRuns` recarrega idêntico — os runs **nunca** são silenciosamente descartados.

## 3.4. `BarcodeElement` — código de barras

`sealed record BarcodeElement : ReportElement` (`BarcodeElement.cs:49-64`). Marcado `[TextStyled]`. Renderiza um código de barras cujo valor vem da `Expression` avaliada (saída vetorial: o encoder produz geometria em unidades de módulo, o renderer escala para os `Bounds`).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Symbology` | `BarcodeSymbology` | `Code128` | Simbologia a renderizar (ver enum abaixo). |
| `Expression` | `string` | **`required`** | Expressão que produz o valor (dígitos, alfanumérico, URL…). |
| `ShowText` | `bool` | `true` | Desenha o texto legível abaixo do símbolo. Apenas 1D; QR ignora a flag. |
| `QrEcc` | `QrEccLevel` | `Medium` | Apenas QR: nível de correção de erro. Ignorado para simbologias 1D. |

`BarcodeSymbology` (`BarcodeElement.cs:8-30`): `Code128` (subset B, ASCII 32..127), `Code39`, `Codabar`, `Itf` (Interleaved 2 of 5), `Ean13`, `Ean8`, `UpcA`, `Isbn` (EAN-13 prefixo 978/979), `Issn` (EAN-13 prefixo 977), `QrCode` (2D, versões 1–40, ECC L/M/Q/H).

`QrEccLevel` (`BarcodeElement.cs:33-43`): `Low` (~7%), `Medium` (~15%, default), `Quartile` (~25%), `High` (~30%).

## 3.5. `ImageElement` — imagem

`sealed record ImageElement : ReportElement` (`ImageElement.cs:33-40`).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Source` | `ImageSourceKind` | `Path` | De onde vêm os bytes da imagem. |
| `Path` | `string?` | `null` | Caminho absoluto ou relativo (usado quando `Source = Path`). |
| `Expression` | `string?` | `null` | Expressão que resolve em runtime para bytes ou caminho (usado quando `Source = Expression`). |
| `InlineData` | `EquatableArray<byte>` | `Empty` | Bytes embutidos inline (usado quando `Source = Inline`). |
| `Sizing` | `ImageSizing` | `Fit` | Política de escala dentro dos `Bounds`. |

`ImageSourceKind` (`ImageElement.cs:20-31`): `Inline` (lê `InlineData`), `Path` (lê `Path`), `Expression` (lê `Expression`).

`ImageSizing` (`ImageElement.cs:5-18`): `Stretch` (estica, pode distorcer), `Fit` (preserva aspect, padding/letterbox — **default**), `Fill` (preserva aspect, corta para preencher), `Native` (tamanho nativo, ancorado top-left).

## 3.6. `RectangleElement` — retângulo/container

`sealed record RectangleElement : ReportElement` (`ShapeElements.cs:30-43`).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `FillColor` | `Color?` | `null` | Cor de preenchimento. `null` = sem fill. `[PropertyGrid(Bindable=true)]`. |
| `CornerRadius` | `Unit` | `Unit.Zero` | Raio dos cantos arredondados. `[PropertyGrid(Bindable=true)]`. |
| `Children` | `EquatableArray<ReportElement>` | `Empty` | Report items aninhados, posicionados por `Bounds` **relativos ao canto superior-esquerdo deste retângulo**. |

**Semântica de container.** O retângulo desenha o fill primeiro, depois os filhos por cima. Filhos que extrapolam o retângulo **não são clipados** (paridade visual com o comportamento legado achatado); o clipping real é follow-up.

> **Nota (edição aninhada).** No designer, os filhos viram VMs próprios (round-trip opaco preservado), isolados do JS de drag; a recursão de filhos atravessa `RectangleElement.Children`. A serialização dos filhos é recursiva (cada filho é serializado pelo mesmo caminho do elemento de topo).

## 3.7. `EllipseElement` — elipse

`sealed record EllipseElement : ReportElement` (`ShapeElements.cs:45-49`).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `FillColor` | `Color?` | `null` | Cor de preenchimento da elipse inscrita em `Bounds`. `[PropertyGrid(Bindable=true)]`. |

## 3.8. `LineElement` — linha

`sealed record LineElement : ReportElement` (`ShapeElements.cs:10-16`). `Bounds` define a *bounding box*; a linha é desenhada entre dois cantos, conforme `Direction`.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Direction` | `LineDirection` | `TopLeftToBottomRight` | Orientação da linha dentro de `Bounds`. `[PropertyGrid(Bindable=true)]`. |
| `Pen` | `BorderSide` | `new(BorderLineStyle.Solid, Unit.FromPoint(0.5), Color.Black)` | Estilo/espessura/cor do traço. |

`LineDirection` (`ShapeElements.cs:18-28`): `Horizontal` (na vertical-central de `Bounds`), `Vertical` (na horizontal-central), `TopLeftToBottomRight`, `BottomLeftToTopRight`.

## 3.9. `TableElement` — tabela em bandas simples

`sealed record TableElement : ReportElement` (`TableElement.cs:15-25`). Tabela banded: linha de cabeçalho, linha de detalhe repetida e linha de rodapé opcional. Composição mais rica (cabeçalhos multi-linha, grupos aninhados) é obtida via subreports — ou via `TablixElement` (§3.11).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Columns` | `EquatableArray<TableColumn>` | `Empty` | Colunas da tabela (ver abaixo). |
| `HeaderHeight` | `Unit` | `Unit.FromMm(7)` | Altura da linha de cabeçalho. |
| `DetailHeight` | `Unit` | `Unit.FromMm(6)` | Altura de cada linha de detalhe repetida. |
| `FooterHeight` | `Unit` | `Unit.Zero` | Altura do rodapé (zero = sem rodapé). |
| `DataExpression` | `string?` | `null` | Expressão que produz a fonte de dados; se `null`, usa a fonte da banda. |

`TableColumn(string Name, Unit Width, string? HeaderText = null, string? DetailExpression = null, string? FooterExpression = null)` (`TableElement.cs:6-11`): nome, largura, e expressões/textos para cabeçalho, detalhe e rodapé da coluna.

## 3.10. `SubreportElement` — sub-relatório

`sealed record SubreportElement : ReportElement` (`SubreportElement.cs:7-25`). Embute outro `ReportDefinition` nos `Bounds` deste elemento.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `ReportId` | `string?` | `null` | Referência a um report-filho resolvido por ID contra um registry. Mutuamente exclusivo com `InlineDefinition`. |
| `InlineDefinition` | `ReportDefinition?` | `null` | Definição de report inline (mutuamente exclusiva com `ReportId`). |
| `ParameterBindings` | `EquatableDictionary<string,string>` | `Empty` | Expressões de parâmetro passadas ao filho (chave = nome do parâmetro no filho). |
| `DataExpression` | `string?` | `null` | Expressão no contexto do pai que produz o `IEnumerable` entregue à fonte de dados do filho. |

> **Status.** O render real de subreports está implementado (tarefa "Subreports com render real" concluída).

## 3.11. `TablixElement` — região de dados unificada (Table + Matrix + List)

`sealed record TablixElement : ReportElement` (`AdvancedElements.cs:32-83`). RDL `Tablix`: grid aninhado linha × coluna onde qualquer eixo pode ser estático **ou** agrupar dinamicamente por expressão.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `DataSetName` | `string?` | `null` | Nome do dataset que dirige a iteração de linhas; casa com `DataSourceDefinition.Name` do pai. |
| `RowGroups` | `EquatableArray<TablixGroup>` | `Empty` | Grupos de linha hierárquicos (innermost → outermost). Vazio para matrix-pura que pivota só em colunas. |
| `ColumnGroups` | `EquatableArray<TablixGroup>` | `Empty` | Grupos de coluna hierárquicos (innermost → outermost). Vazio para tabela que cresce só na vertical. |
| `Cells` | `EquatableArray<TablixCell>` | `Empty` | Células do corpo; cada uma referencia coordenada (linha, coluna) + elemento-filho (tipicamente um TextBox). |
| `ColumnWidths` | `EquatableArray<double>` | `Empty` | Pesos relativos de largura de coluna. Vazio → colunas iguais. Ex.: `[1,2,1]` faz a coluna central 2× mais larga. Lista parcial → fallback para o peso médio. |
| `RowSubtotals` | `bool` | `false` | Renderiza linha de subtotal ao fim de cada grupo de linha externo + linha de grand total no rodapé (SSRS-style). As linhas extras crescem a altura renderizada. |
| `ColumnSubtotals` | `bool` | `false` | Espelho no eixo de colunas: coluna de subtotal após cada grupo externo + coluna de grand total à direita. Largura por-coluna encolhe (largura do elemento é fixa). Combina com `RowSubtotals` (a célula grand-total × grand-total é a soma geral). |
| `SubtotalLabel` | `string?` | `null` | Rótulo do subtotal de grupo; `{0}` é substituído pelo valor do grupo (`"Total {0}"` → "Total Sul"). `null` → default `"Total {0}"`. Aplica a linha e coluna. |
| `GrandTotalLabel` | `string?` | `null` | Rótulo do grand total. `null` → default `"Total geral"`. Aplica a linha e coluna. |
| `NoRowsMessage` | `string?` | `null` | Mensagem centralizada (no lugar do grid) quando o dataset não produz linhas — RDL `<NoRowsMessage>`. `null` → dataset vazio renderiza nada. |

`TablixGroup(string Name, string? GroupExpression = null, string? SortExpression = null, bool SortDescending = false)` (`AdvancedElements.cs:86-90`): um eixo de agrupamento — nome + expressão de chave de grupo + ordenação.

`TablixCell(int RowIndex, int ColumnIndex, ReportElement? Content, int ColumnSpan = 1, int RowSpan = 1)` (`AdvancedElements.cs:95-96`): célula do corpo. `ColumnSpan`/`RowSpan` mesclam a célula sobre colunas/linhas adjacentes (RDL ColSpan/RowSpan); `1` = célula 1×1 normal. Uma célula mesclada ocupa as células cobertas, que o renderer pula.

> **Status.** O render de matrix/pivô do Tablix está implementado (tarefa "Tablix matrix/pivô" concluída). Demais cenários do scaffold RDL F2 (§3.14) podem cair em placeholder até o pipeline dedicado aterrissar.

## 3.12. `ChartElement` — gráfico

`sealed record ChartElement : ReportElement` (`ChartElement.cs:36-46`).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Kind` | `ChartKind` | `Bar` | Tipo de gráfico. |
| `Title` | `string?` | `null` | Título opcional. |
| `ShowLegend` | `bool` | `true` | Exibe a legenda. |
| `Series` | `EquatableArray<ChartSeries>` | `Empty` | Séries do gráfico. |

`ChartKind` (`ChartElement.cs:7-23`): `Bar`, `Line`, `Pie`, `Area` (linha com área translúcida abaixo), `Scatter` (marcador por ponto, sem linha), `Radar` (polar — categorias em eixos radiais, valor = raio, série = teia fechada), `Bubble` (scatter + tamanho do marcador por `SizeExpression`), `Stock` (barra high-low — de `LowExpression` a `HighExpression` por categoria, com tick de fechamento no valor).

`ChartSeries(string Name, string CategoryExpression, string ValueExpression, Color? Color = null, string? SizeExpression = null, string? HighExpression = null, string? LowExpression = null)` (`ChartElement.cs:27-34`):

| Param | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Nome da série. |
| `CategoryExpression` | `string` | (obrigatório) | Expressão da categoria (eixo X / fatia). |
| `ValueExpression` | `string` | (obrigatório) | Expressão do valor (eixo Y / raio). |
| `Color` | `Color?` | `null` | Cor da série; `null` → cor da paleta. |
| `SizeExpression` | `string?` | `null` | Dirige o raio em `Bubble`; ignorado pelos demais kinds. |
| `HighExpression` | `string?` | `null` | Topo da barra em `Stock`; ignorado pelos demais. |
| `LowExpression` | `string?` | `null` | Base da barra em `Stock`; ignorado pelos demais. |

> **Status.** Os kinds `Area/Scatter/Bubble/Stock/Radar` têm render implementado (tarefa concluída).

## 3.13. `GaugeElement` — medidor

`sealed record GaugeElement : ReportElement` (`AdvancedElements.cs:183-197`). RDL `Gauge`: medidor radial/linear mostrando um valor contra um range.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Kind` | `GaugeKind` | `Radial` | `Radial` ou `Linear`. |
| `MinimumExpression` | `string` | `"0"` | Expressão do mínimo da escala. |
| `MaximumExpression` | `string` | `"100"` | Expressão do máximo da escala. |
| `ValueExpression` | `string` | `"0"` | Expressão do valor atual (ponteiro). |
| `Ranges` | `EquatableArray<GaugeRange>` | `Empty` | Faixas coloridas opcionais (zonas vermelho/amarelo/verde). |

`GaugeKind` (`AdvancedElements.cs:199`): `Radial`, `Linear`.

`GaugeRange(string StartExpression, string EndExpression, string ColorHex)` (`AdvancedElements.cs:201-204`): início (expr), fim (expr), cor (hex).

## 3.14. Scaffold RDL F2 (round-trip lossless, render placeholder)

Os tipos a seguir (`AdvancedElements.cs`) carregam cada *kind* RDL que o renderer **ainda não desenha nativamente**. O objetivo é **round-trip lossless**: um `.repx` autorado contra SSRS ou editado por ferramenta terceira que coloque um desses elementos pode ser carregado, editado, salvo e reaberto sem perda da configuração. Até o pipeline dedicado aterrissar, o renderer desenha um retângulo-placeholder rotulado nos `Bounds`. Adicionar render depois é puramente aditivo (o formato de fio e o schema `.repx` não mudam).

### 3.14.1. `MapElement` — mapa geográfico

`sealed record MapElement : ReportElement` (`AdvancedElements.cs:130-175`).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Basemap` | `string?` | `null` | Provedor de tiles ("BingMaps", "OpenStreetMap", "None"). Reservado para a camada online de tiles. |
| `DataSetName` | `string?` | `null` | Fonte de dados com pontos/polígonos geocodificados. |
| `LatitudeExpression` | `string?` | `null` | Expressão da latitude por linha. |
| `LongitudeExpression` | `string?` | `null` | Expressão da longitude por linha. |
| `ShapesGeoJson` | `string?` | `null` | GeoJSON inline (FeatureCollection/Feature/Geometry). Polygon/MultiPolygon preenchidos; LineString stroked. Precede `ShapeSet`. |
| `ShapeSet` | `string?` | `null` | Nome de um shape-set embutido resolvido pelo registry ("brazil", "south-america"). Ignorado quando `ShapesGeoJson` está setado. |
| `ShowGraticule` | `bool` | `false` | Desenha graticule lat/long (grid + ticks de grau) atrás dos dados. |
| `ShapeFill` | `string` | `"#E8EDE4"` | Cor de preenchimento (hex) dos polígonos — a cor da "terra". |
| `ShapeStroke` | `string` | `"#9CA3AF"` | Cor de traço (hex) de contornos/graticule. |

> **Status.** O basemap por tiles (OpenStreetMap) tem suporte implementado (tarefa concluída); o basemap vetorial offline é dirigido pelos campos `ShapesGeoJson`/`ShapeSet`/graticule.

### 3.14.2. `CodeElement` — bloco de código custom

`sealed record CodeElement : ReportElement` (`AdvancedElements.cs:111-119`). RDL `Code`: bloco de C# (originalmente VB.NET em SSRS) declarando funções-helper alcançáveis das expressões como `Code.MethodName(...)`.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Source` | `string` | `string.Empty` | Texto-fonte do bloco de código. |
| `Language` | `CodeLanguage` | `CSharp` | `CSharp` (default) ou `VisualBasic` (interop SSRS legado). |

`CodeLanguage` (`AdvancedElements.cs:121`): `CSharp`, `VisualBasic`.

> **Status.** Scaffold: o fonte é preservado verbatim; chamadas `Code.X` em expressões atualmente lançam `NotImplementedException` (compile/load via Roslyn é fase futura). O designer já tem editor de `Source`+`Language` e entrada de toolbox.

### 3.14.3. `DataBarElement`

`sealed record DataBarElement : ReportElement` (`AdvancedElements.cs:213-224`). RDL `DataBar`: barra horizontal que preenche proporcional a `ValueExpression`, tipicamente dentro de uma célula de Tablix.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `ValueExpression` | `string` | `"0"` | Valor que dirige o preenchimento. |
| `MinimumExpression` | `string` | `"0"` | Mínimo da escala. |
| `MaximumExpression` | `string` | `"100"` | Máximo da escala. |
| `FillColor` | `string` | `"#C2410C"` | Cor de preenchimento (hex literal ou expressão que retorna hex). |

### 3.14.4. `SparklineElement`

`sealed record SparklineElement : ReportElement` (`AdvancedElements.cs:232-243`). RDL `Sparkline`: mini-gráfico line/bar embutido numa célula.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Kind` | `SparklineKind` | `Line` | `Line`, `Column` ou `Area`. |
| `DataSetName` | `string?` | `null` | Fonte da série de tendência (cada linha = um ponto). |
| `ValueExpression` | `string` | `"Fields.Value"` | Expressão do valor por ponto. |
| `CategoryExpression` | `string?` | `null` | Expressão da categoria. |

`SparklineKind` (`AdvancedElements.cs:245`): `Line`, `Column`, `Area`.

### 3.14.5. `IndicatorElement`

`sealed record IndicatorElement : ReportElement` (`AdvancedElements.cs:254-263`). RDL `Indicator`: ícone KPI (seta/estrela/barras de sinal) que troca a visualização conforme em qual range de "estado" o valor cai.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Kind` | `IndicatorKind` | `DirectionalArrow` | Família de ícone. |
| `ValueExpression` | `string` | `"0"` | Valor avaliado contra os estados. |
| `States` | `EquatableArray<IndicatorState>` | `Empty` | Fronteiras de estado. |

`IndicatorKind` (`AdvancedElements.cs:265`): `DirectionalArrow`, `Shape`, `RatingBar`, `Symbol`.

`IndicatorState(string StartExpression, string EndExpression, string IconName)` (`AdvancedElements.cs:267-270`): início (expr), fim (expr), nome do ícone.

## 3.15. `ElementAction` e `ActionKind`

`sealed record ElementAction(ActionKind Kind, string? Hyperlink = null, string? BookmarkId = null, string? DrillthroughReportName = null, EquatableArray<DrillthroughParameter> DrillthroughParameters = default)` (`ElementAction.cs:31-51`). Ação RDL-compatível anexada a um `ReportElement` (no máximo uma — campo `ReportElement.Action`). Dispara quando o usuário clica/toca o elemento num renderer interativo (Viewer, export HTML, export PDF com anotações de hyperlink).

É um **sum type** com exatamente uma das três variantes populada, selecionada por `Kind`:

| `Kind` | Campos relevantes | Semântica |
|---|---|---|
| `Hyperlink` | `Hyperlink` (URL ou expressão) | Abre a URL em nova aba (ou emite anotação de link PDF). Aceita URLs literais e expressões que avaliam para URL. |
| `BookmarkLink` | `BookmarkId` | Salta para o elemento cujo `ReportElement.Bookmark` é igual a `BookmarkId`. Funciona dentro do documento renderizado (outlines PDF, scroll do Viewer). |
| `DrillthroughReport` | `DrillthroughReportName`, `DrillthroughParameters` | Abre outro report identificado por nome, passando os parâmetros. Mediado pelo host (o Viewer levanta um evento que o host trata). |

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Kind` | `ActionKind` | (obrigatório) | Seleciona a variante ativa. |
| `Hyperlink` | `string?` | `null` | URL/expressão (variante `Hyperlink`). |
| `BookmarkId` | `string?` | `null` | Alvo do bookmark (variante `BookmarkLink`). |
| `DrillthroughReportName` | `string?` | `null` | Nome do report-alvo (variante `DrillthroughReport`). |
| `DrillthroughParameters` | `EquatableArray<DrillthroughParameter>` | `default` ( = vazio) | Parâmetros passados ao report-alvo. |

`ActionKind` (`ElementAction.cs:54-64`): `Hyperlink`, `BookmarkLink`, `DrillthroughReport`.

**Construtores de conveniência** (estáticos):

```csharp
ElementAction.ToUrl("https://exemplo.com");          // ou ToUrl("=Fields.Url")
ElementAction.ToBookmark("secao-resumo");
ElementAction.ToDrillthrough("DetalhePedido",
    new DrillthroughParameter("PedidoId", "Fields.Id"));
```

`DrillthroughParameter(string Name, string Value, bool Omit = false)` (`ElementAction.cs:73`):

| Param | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | (obrigatório) | Nome do parâmetro no report-alvo. |
| `Value` | `string` | (obrigatório) | Expressão avaliada no contexto do report **fonte**; o resultado vira o valor do parâmetro no alvo. |
| `Omit` | `bool` | `false` | Se `true`, o parâmetro não é passado (RDL `<Omit>`) — útil para suprimir parâmetros condicionalmente. |

## 3.16. Resumo dos report items

| Record | Render nativo | Campos próprios principais |
|---|---|---|
| `LabelElement` | Sim | `Text` (required) |
| `TextBoxElement` | Sim | `Expression` (required), `CanGrow`, `CanShrink`, `TextRuns` |
| `BarcodeElement` | Sim | `Symbology`, `Expression` (required), `ShowText`, `QrEcc` |
| `ImageElement` | Sim | `Source`, `Path`, `Expression`, `InlineData`, `Sizing` |
| `RectangleElement` | Sim (container) | `FillColor`, `CornerRadius`, `Children` |
| `EllipseElement` | Sim | `FillColor` |
| `LineElement` | Sim | `Direction`, `Pen` |
| `TableElement` | Sim | `Columns`, `Header/Detail/FooterHeight`, `DataExpression` |
| `SubreportElement` | Sim | `ReportId`, `InlineDefinition`, `ParameterBindings`, `DataExpression` |
| `TablixElement` | Sim (matrix/pivô) | `DataSetName`, `Row/ColumnGroups`, `Cells`, subtotais, labels |
| `ChartElement` | Sim | `Kind`, `Title`, `ShowLegend`, `Series` |
| `GaugeElement` | Placeholder/scaffold | `Kind`, `Min/Max/ValueExpression`, `Ranges` |
| `MapElement` | Parcial (tiles + vetor) | geo expressions, GeoJSON/shape-set, graticule |
| `CodeElement` | Scaffold (sem compile) | `Source`, `Language` |
| `DataBarElement` | Placeholder/scaffold | `Value/Min/MaxExpression`, `FillColor` |
| `SparklineElement` | Placeholder/scaffold | `Kind`, `DataSetName`, `Value/CategoryExpression` |
| `IndicatorElement` | Placeholder/scaffold | `Kind`, `ValueExpression`, `States` |

> Os campos herdados de `ReportElement` (§3.1) aplicam-se a **todos** os records acima. O status "scaffold/placeholder" indica que o elemento faz round-trip lossless mas é desenhado como caixa rotulada até o pipeline de render dedicado aterrissar; isso é uma propriedade do renderer, não do modelo de dados.
