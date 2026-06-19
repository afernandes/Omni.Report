# Design — PropertyGrid orientado a metadados (herança automática de propriedades)

> Status: **proposta para revisão** · Autor: análise multi-agente · Alvo: `Reporting.Designer.Blazor`
>
> Objetivo: ao criar um elemento que **herda** de um existente, ele deve **automaticamente** ganhar
> as propriedades (e os editores) do pai, mais as próprias, com o **editor certo por tipo** — sem
> escrever lógica nova de UI (`@if (Kind == X)`, `HasTextContent`, …) a cada propriedade/elemento.

## 1. Problema — como é hoje e por que dói

O `PropertyGrid.razor` é uma **união discriminada** sobre `DesignerElementKind`: cada elemento tem um
bloco `@if (Element.Kind == X)` com editores escritos à mão, e seções compartilhadas são gateadas por
helpers como `HasTextContent(Element)`.

**Custo de adicionar UMA propriedade nova** — ~6 pontos acoplados:

| # | Arquivo | O que muda |
|---|---|---|
| 1 | `Reporting.Core/Elements/*.cs` | o campo no record de domínio |
| 2 | `ViewModels/ElementViewModel.cs` | backing field + getter/setter no VM |
| 3 | `ElementViewModel.ToElement()` | branch por kind que emite o campo |
| 4 | `ElementViewModel.FromElement()` | branch que lê o campo de volta |
| 5 | `ElementViewModel.Clone()` | cópia campo-a-campo |
| 6 | `Components/PropertyGrid.razor` | o editor + o gate de visibilidade |

(+ os 4 switches de serialização `.repx`/`.repjson` quando o campo precisa persistir — ver §10.)

### Insight estrutural decisivo

A **maior parte das props de aparência** (Font, ForeColor, BackColor, Border, Padding, Horizontal/
VerticalAlignment, WordWrap, Format) **não fica no record do elemento** — fica no record `Style`
(`Reporting.Core/Styling/Style.cs`) anexado em `ReportElement.Style`. Ou seja, a "herança de
aparência" **já existe de fato** via `Style`; o gate `HasTextContent` apenas a **esconde
artificialmente** das formas (Rectangle/Ellipse). O bug recente — *o `FillColor` de um Rectangle não
era editável no Designer* — é um **sintoma** desse gate, não um caso isolado.

Outros pontos:
- Todos os records de elemento são `sealed` → **não há herança real entre tipos** hoje. Um
  `RectangleComSombra` exigiria novo `DesignerElementKind`, novas branches em todos os switches e novo
  `@if`, **sem reuso automático** de `FillColor`/`CornerRadius`.
- Os elementos avançados opacos (Tablix/Gauge/DataBar/Sparkline/Map/Indicator) **já** usam um padrão
  mais limpo — `Src<T>()`/`Mutate<T>()` (`ElementViewModel.cs`) que faz `_sourceElement with { Prop =
  value }` direto sobre o record imutável, **sem backing fields**. Isso é meio caminho andado para o
  setter imutável genérico.

## 2. Como as engines maduras resolvem

WinForms/WPF `System.ComponentModel` PropertyGrid, DevExpress XtraReports, Telerik Reporting, e os
inspectors de Unity/Godot convergem na **mesma arquitetura de 3 camadas**:

1. **Metadados na definição da propriedade** — atributos no modelo (`[Category]`, `[DisplayName]`,
   `[Description]`, `[Browsable]`, `[Editor]`/`UITypeEditor`, `[TypeConverter]`) ou registro fluente.
   Os fatos sobre a propriedade vivem **junto da propriedade**, não num switch de UI distante.
2. **Descoberta por reflection que caminha a hierarquia de tipos** — `TypeDescriptor.GetProperties()`
   / `type.GetProperties(BindingFlags.Public | Instance)` já devolve props **declaradas + herdadas da
   base**. É por isso que a herança é "de graça": uma base `TextElement { ForeColor }` faz
   `LabelElement : TextElement` exibir `ForeColor` sem uma linha extra. O resultado é **cacheado por
   tipo** para evitar reflection por render.
3. **Resolução de editor por tipo** — primeiro o `[Editor]` explícito; senão um mapa por tipo da
   propriedade (`bool`→checkbox, enum→dropdown, `Color`→color picker, `double`+`[Range]`→slider,
   `string`→textbox). Um **único** componente genérico itera os descritores e despacha por tipo.

> Nota importante: o **switch discriminativo de serialização** (`RepxWriter`/`Reader`, `ToElement`/
> `FromElement`) **permanece manual** em todas as engines — reflection/codegen não o eliminam porque
> precisam conhecer todos os tipos concretos. Mas deixa de ser um problema de **UI**.

## 3. Opções consideradas

| Opção | Herança automática? | Esforço | Observação |
|---|---|---|---|
| **A — Atributos no modelo + reflection cacheada + setter `with` por Expression Tree** | ✅ de graça | **baixo** | **Recomendada** |
| B — Source generator (metadados + setters em build-time) | ✅ em compile-time | médio | Evolução futura; peso de Roslyn desnecessário p/ ~18 tipos |
| C — Fluent builder type-safe (registro explícito) | ❌ precisa `.Inherit<T>()` manual | médio | Mata justamente o objetivo (herança automática) |

### Opção A — detalhe
Atributos `[PropertyGrid(Category, Order, Editor, Description, Nested)]` nos records de
`Reporting.Core` (elemento **e** `Style`). Um `PropertyGridDescriptorService` faz `GetProperties()`
caminhando a hierarquia, agrupa por `Category`, infere o editor pelo tipo, e gera um
`Func<ReportElement, object?, ReportElement>` via **Expression Tree** (cacheado) que executa
`record with { Prop = value }`. Um `PropertyGridGenericSection.razor` itera os descritores e despacha
por tipo de editor. **Convive com as seções hand-coded** como fallback por kind.
- **Prós:** herança 100% automática (alinhado a WinForms/DevExpress/Unity); metadado no model =
  single source of truth (mesmo lugar que a serialização já consulta → paridade preservada); reusa o
  `record with {}` já provado em `Mutate<T>`; cache por tipo zera o custo de reflection.
- **Contras:** Expression Tree para setter de record `init-only` é não-trivial (testar imutabilidade);
  props de aparência no `Style` aninhado exigem 1 nível de recursão (`[Nested]`); `EquatableArray<T>`
  (séries, conditional formats) exige componente de lista custom.

### Opção B — source generator
Mesmos atributos, mas um Roslyn generator emite tabelas de descritores + setters fortemente tipados em
compile-time (zero reflection em runtime, type-safe, rename quebra o build). **Contra:** infra de
generator é peso novo e overkill para ~18 kinds; o payoff de performance só importa com 100+ tipos.
**É a evolução natural da Opção A** quando/se o volume crescer.

### Opção C — fluent builder
`PropertyGridBuilder<TElement>.Property(e => e.Expression, p => p.Category("Data").Editor<TextArea>())`,
herança via `.Inherit<TBase>()`. **Contra decisivo:** a herança **não** é automática — exige
`.Inherit<T>()` manual em cada tipo, contrariando o objetivo central; e o metadado fica na camada
Designer, separado do model (risco de dessincronizar).

## 4. Decisão: **Opção A**

É a **única** opção em que a herança é **realmente automática** (o objetivo do pedido): basta a
reflection caminhar a hierarquia de records. Coloca o metadado no **mesmo lugar que a serialização já
consulta** (single source of truth, paridade `.repx`/`.repjson` preservada) e **reusa o padrão `record
with {}`** já provado em `Mutate<T>`, então o atrito conceitual é mínimo. A reflection **cacheada por
tipo** neutraliza o custo de performance — único trunfo real de B/C neste volume. B fica como evolução
futura; C sacrifica a herança automática que motivou o trabalho.

## 5. Sketch concreto

**1) Atributo no model** (`src/Reporting.Core/Metadata/PropertyGridAttribute.cs`):
```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class PropertyGridAttribute : Attribute
{
    public string? Category { get; init; }
    public int Order { get; init; }
    public string? Editor { get; init; }       // sobrescreve a inferência por tipo
    public string? Description { get; init; }
    public bool Nested { get; init; }           // recursa nos descritores de um record aninhado (Style)
}
```

**2) Hierarquia de records para a herança** (resolve o bug do `FillColor` e habilita derivados):
```csharp
public abstract record ShapeElement : ReportElement
{
    [PropertyGrid(Category = "Forma", Order = 1, Editor = "color-picker")]
    public Color? FillColor { get; init; }
}
public sealed record RectangleElement : ShapeElement
{
    [PropertyGrid(Category = "Forma", Order = 2, Editor = "unit-spinner")]
    public Unit CornerRadius { get; init; } = Unit.Zero;
}
public sealed record EllipseElement : ShapeElement { }  // ganha FillColor de graça
```
E em `ReportElement.Style` marcar `[PropertyGrid(Category = "Appearance", Nested = true)]` para puxar
os descritores de `Style` (Font/ForeColor/BackColor/Border/Padding/Align) para **qualquer** elemento —
**matando o gate `HasTextContent`**.

**3) Resolver de editor por tipo + descritor com setter imutável**
(`Designer.Blazor/Services/PropertyGridDescriptorService.cs`):
```csharp
sealed record PropDescriptor(
    string Name, Type Type, string Editor, string? Category, int Order,
    Func<object, object?> Get,
    Func<ReportElement, object?, ReportElement> Set);

static string InferEditor(Type t) => t switch
{
    _ when t == typeof(bool)                                            => "toggle",
    _ when t.IsEnum                                                     => "enum",
    _ when t == typeof(Color) || t == typeof(Color?)                    => "color-picker",
    _ when t == typeof(Unit)                                            => "unit-spinner",
    _ when t.IsGenericType
        && t.GetGenericTypeDefinition() == typeof(EquatableArray<>)     => "list",
    _                                                                   => "text",
};
// Setter imutável genérico via Expression Tree, cacheado por (Type, prop):
//   monta `elem => (ReportElement)(elem with { Prop = (TProp)value })`
//   usando o método <Clone>$ do record + MemberInit. Mesma semântica de Mutate<T>.
// GetSections(element):
//   element.GetType().GetProperties()        // já inclui herdadas
//     .Where(p => tem [PropertyGrid])
//     .Concat(descritores virtuais do Style se [Nested])
//     .GroupBy(Category).OrderBy(Order);
```

**4) Componente genérico** (`Designer.Blazor/Components/PropertyGridGenericSection.razor`) faz
`@switch (prop.Editor)`: `"toggle"`→checkbox; `"enum"`→`<select>` com `Enum.GetValues`;
`"color-picker"`→`<ColorPickerEditor/>` (reusa o bloco de cor atual do PropertyGrid); `"unit-spinner"`→
`<input type=number step=0.1>` + "mm"; `"list"`→componente de lista dedicado; default→`<input
type=text>`. Cada `onchange` chama `prop.Set` sobre o record e re-materializa o VM.

## 6. O fluxo que você pediu — novo elemento herdado ganha tudo

Cenário: criar um `RoundedShadowRectangle` que herda de `RectangleElement` e adiciona `ShadowColor`.

```csharp
public sealed record RoundedShadowRectangle : RectangleElement
{
    [PropertyGrid(Category = "Forma", Order = 3, Editor = "color-picker")]
    public Color? ShadowColor { get; init; }
}
```

**Pronto.** O PropertyGrid já mostra:
- `FillColor` (herdado de `ShapeElement`),
- `CornerRadius` (herdado de `RectangleElement`),
- `ShadowColor` (próprio),
- **+ toda a seção Appearance** (vinda do `Style` aninhado),
- **+ Layout/Bounds** (da base `ReportElement`),

…**sem tocar `PropertyGrid.razor`, sem backing field, sem branch de editor, sem `@if`**, com o editor
escolhido pelo **tipo** de cada prop (`Color?` → color-picker).

O **único** trabalho manual restante é a **paridade de serialização** (§10) — inerente ao formato de
arquivo e que nenhuma engine elimina, mas que deixou de ser um problema de UI.

## 7. Migração incremental (sem big-bang) — **ENTREGUE**

> Status: a Opção A foi implementada e **a remoção do `HasTextContent` está completa**. Abaixo, o que
> foi entregue (PRs reais) — mais do que o plano original, pois também incluiu o **expression-binding
> por propriedade** (botão `fx`, ver §11), que o pedido pediu em paralelo.

| Fase | PR | Escopo entregue |
|---|---|---|
| **0 — Infra** | #34 | `PropertyGridAttribute` + `PropertyGridDescriptors` (reflection + cache + setter imutável via `<Clone>$`) + testes |
| **A — Expression-binding** | #35/#36 | `ReportElement.PropertyExpressions` (qualquer prop por expressão, render + serialização + code-first `.Bind`); metadado `Bindable`; o VM passa a preservar `PropertyExpressions` (fix de perda) |
| **1 — Editor genérico + fx** | #37 | `PropertyGridMetaSection.razor` (editor por tipo + toggle `fx`); substitui as seções hand-coded de `Line`/`Forma`; bridge `ApplyMetaSet`/`LoadFrom` no VM |
| **2a — Flattening `[Nested]`** | #38 | O serviço achata um record aninhado (`Style.X`) com path pontilhado + setter encadeado imutável |
| **2b — Aparência/Borda → metadados, sem `HasTextContent`** | #39/#40/#41 | Marcador `[TextStyled]` (herdável); `Style` anotado (fonte/cor/alinhamento/borda/padding) com editores ricos **portados**; seções Appearance + Border&Padding hand-coded removidas; **`HasTextContent` eliminado** (gate da Data via `Element.IsTextStyled`) |
| **Polimento** | #42 | Meta-grid agrupado por categoria (cabeçalhos "Aparência"/"Borda"/"Forma"/"Linha") |

**Resultado:** nenhuma seção do PropertyGrid depende mais de kind-list hardcoded; aparência, borda e o
gate de texto vêm todos dos metadados (`[PropertyGrid]` + `[TextStyled]`), respeitando herança e o tipo
da propriedade, com `fx` (expressão) em qualquer propriedade bindável. A seção "Data" (conteúdo
primário texto/expressão + Monaco fx + preset de formato) permanece dedicada, mas com visibilidade
metadata-driven. **Pendências conhecidas (fora do escopo):** migrar a seção Data para metadados;
preservar a semântica "inherit" (null) de `ForeColor`/`Font` no VM (o designer materializa Black/Arial
hoje — pré-existente).

## 8. Riscos e mitigações

1. **Setter de record `init-only`/`required` via Expression Tree** — usar o método `<Clone>$` do
   compilador ou um hook `IWith` por tipo. *Mitigação:* testes de imutabilidade no CI (o `with` não
   muta o original); fallback para a Opção B (source generator) se a árvore ficar frágil.
2. **Props de aparência no `Style` aninhado** — exigem descritores "virtuais" (1 nível de recursão por
   `[Nested]`) cujo setter faz `elem with { Style = elem.Style with { Prop = v } }`. Adiciona
   complexidade ao gerador de setter.
3. **Introduzir bases intermediárias (`ShapeElement`)** muda a hierarquia de records que a
   serialização consulta — rodar a suíte de round-trip `.repx`/`.repjson` antes do merge (ver
   `MEMORY: serializer-parity`; a auditoria trata advisory como erro de build).
4. **`EquatableArray<T>` não covariante** — o descritor precisa de branch `IsGenericType` específico +
   componente de lista. Deixar para o fim para não bloquear o ganho dos tipos triviais.
5. **`required string Expression` em `TextBoxElement`** impede `new T()` vazio — usar a factory
   `CreateDefault` por tipo (já existe `CreateDefaultAdvanced` em `ElementViewModel.cs`).

## 9. Critérios de aceite (quando estiver pronto)

- Uma propriedade nova num elemento **anotado** aparece no PropertyGrid **sem** tocar `.razor`.
- Um elemento que herda de outro **anotado** mostra as props do pai + as próprias automaticamente.
- O editor é escolhido pelo **tipo** (Color→picker, enum→dropdown, bool→toggle, Unit→mm-spinner).
- Round-trip `.repx`/`.repjson` **lossless** preservado (suíte verde).
- `HasTextContent` removido; formas acessam Appearance/Border/Padding.

## 10. O que **não** muda (escopo honesto)

A **paridade de serialização** (`ToElement`/`FromElement` + os 4 switches `.repx`/`.repjson`) continua
**manual** — é inerente ao formato de arquivo e nenhuma engine a elimina (codegen/reflection precisam
conhecer os tipos concretos). O ganho é que ela passa de **6 pontos para ~4**, e **nenhum** deles é de
UI. Uma evolução possível (fora deste escopo) é um registry `Type → handler` que centraliza esses 4
switches num só lugar — mas isso **não reduz** a paridade, só a concentra.

## 11. Propriedades dirigidas por **expressão** (modelo SSRS `fx`)

> Requisito: *"todas as propriedades poderem ser alteradas via expressions do relatório"* — i.e. qualquer
> propriedade pode ser uma **constante** OU uma **expressão** avaliada por instância/linha, como o botão
> `fx` do SSRS. Isso **complementa** o PropertyGrid de metadados (é onde o `fx` por campo encaixa) e **não**
> impede o code-first/low-level (o valor estático continua sendo o caminho padrão).

### Como as engines resolvem (convergência)

Todas usam uma **coleção lateral** `propriedade → expressão`, avaliada no render, **em vez de embrulhar
cada tipo** num union (`ColorOrExpression`, `UnitOrExpression`… × 50 props — explode o type-system):

| Engine | Armazenamento | UI |
|---|---|---|
| **SSRS / RDL** | cada prop é um `ReportExpression` (prefixo `=` distingue literal de expressão) | botão `fx` por campo |
| **DevExpress XtraReports** | `XRControl.ExpressionBindings` (lista `PropertyName + Expression`); a prop real continua `Color` | diálogo coletor |
| **Telerik Reporting** | `Bindings` com `PropertyPath` aninhado (`"Style.Font.Bold"`) resolvido por reflexão | árvore de props |
| **JasperReports** | cada atributo aceita constante ou `$F{...}` | inline |

**Timing idêntico:** ao renderizar cada instância, avalia a expressão, **coage ao tipo** da propriedade e
**sobrescreve** o valor estático. O OmniReport segue o modelo **DevExpress/Telerik** (coleção lateral +
`PropertyPath` aninhado + reflexão) — viabilizado pelos records imutáveis + o setter por `<Clone>$`.

### Design adotado — **Fase A entregue** (núcleo, sem UI)

- **Modelo:** `ReportElement.PropertyExpressions : EquatableDictionary<string,string>` (path → expressão).
  Reusa o tipo que **já** round-trippa (provado por `SubreportElement.ParameterBindings`). O valor estático
  permanece como **fallback**. Convive com `VisibleExpression` e `ConditionalFormats` (mantidos como estão).
- **Render:** `BandRenderer` aplica `ApplyPropertyExpressions` no início do loop, **antes** de
  `IsVisible`/`ResolveStyle`, produzindo um elemento efetivo → **zero mudança** nos renderers especializados.
  O `PropertyPathBinder` (`Reporting.Layout/Internal`) navega o path (`"Style.Font.Size"`), clona a cadeia
  de records de baixo p/ cima via `<Clone>$`, coage a folha (`Color.FromHex` / `Unit.FromMm` / `Enum.Parse`
  / `Convert.ChangeType`) e seta. Planos cacheados por `(Type, path)`. **Falha = skip gracioso** (mantém o
  estático), nunca quebra o render.
- **Serialização:** espelha o padrão `ParameterBindings` nos 4 switches (`.repx` `<PropertyExpressions>` /
  `.repjson` `propertyExpressions`), emitido só quando `Count > 0`.
- **Code-first/low-level:** `.Bind("Style.ForeColor", "Fields.Total > 1000 ? '#C00' : '#000'")` no
  `BandContent` — só concatena no dict; o estático (`.Color(...)`, `e.Style with {…}`) é intocado. Os dois
  **convivem no mesmo elemento**.
- **Coerção:** centralizada no binder; tipos de domínio (`Color`, `Unit`, enums) + `Convert.ChangeType`,
  respeitando `Nullable<T>` e a cultura do contexto.

### Fase B (designer — próximo passo)

Reusa a Fase 0: flag `Bindable` no `[PropertyGrid]` (curar quais props aceitam expressão) + `PropertyPath`
canônico no descriptor + botão `fx` ao lado de cada editor genérico, reusando o `ExpressionEditorDialog`
existente, gravando em `PropertyExpressions[path]` via um `PropertyExpressionService` isolado (sem tocar o
`ElementViewModel`). Fase C (opcional): unificar `VisibleExpression`/`ConditionalFormat` como açúcar sobre
`PropertyExpressions`.
