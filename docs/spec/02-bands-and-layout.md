# 2. Bandas e layout

Esta seção especifica o modelo de **bandas** (faixas horizontais que compõem um relatório) e a **semântica de paginação/layout** que as transforma em páginas posicionadas. Os tipos normativos vivem em `Reporting.Bands` (`src/Reporting.Core/Bands/Bands.cs`); a engine de layout em `Reporting.Layout` (`ReportPaginator`, `PageAccumulator`, `BandRenderer`).

Todas as medidas verticais são `Unit` — um `readonly record struct Unit(int Mils)` onde 1 mil = 1/1000 de polegada (`src/Reporting.Core/Geometry/Unit.cs:13`). `Unit.Zero` é o valor neutro.

## 2.1. Modelo de bandas

### 2.1.1. `BandKind`

Enum que classifica cada banda (`Bands.cs:8`). Valores, em ordem de declaração:

| Valor | Ordinal | Papel |
|---|---|---|
| `ReportHeader` | 0 | Banner emitido **uma vez**, no topo da primeira página. |
| `PageHeader` | 1 | Repetido no topo de **toda** página (sujeito a supressão). |
| `GroupHeader` | 2 | Cabeçalho de instância de grupo (antes da primeira linha do grupo). |
| `Detail` | 3 | Corpo repetido **uma vez por linha** de dados. |
| `GroupFooter` | 4 | Rodapé de instância de grupo (após a última linha do grupo). |
| `PageFooter` | 5 | Repetido no rodapé de **toda** página (ancorado embaixo). |
| `ReportFooter` | 6 | Emitido **uma vez**, ao final do relatório. |

> Nota: `GroupBand.Kind` retorna sempre `BandKind.GroupHeader` (mesmo carregando header **e** footer); o `BandKind` distingue a *categoria* da banda, não cada faixa física emitida.

### 2.1.2. `IBand`

Superfície comum a qualquer banda (`Bands.cs:20`):

| Membro | Tipo | Semântica |
|---|---|---|
| `Kind` | `BandKind` | Categoria (acima). |
| `Height` | `Unit` | Altura **declarada** da banda. É um *piso* no layout (a banda nunca encolhe abaixo dela, salvo `CanShrink` opt-in — §2.3.4). |
| `Visible` | `bool` | Quando `false`, a banda inteira é omitida (estático). |
| `VisibleExpression` | `string?` | Expressão booleana de visibilidade (campo do modelo). Ver §2.4 sobre estado de implementação por banda. |
| `Elements` | `EquatableArray<ReportElement>` | Elementos desenhados na banda, posicionados em **band-space** (origem `(0,0)` no canto superior-esquerdo da banda). |

### 2.1.3. `ReportBand`

`sealed record` usado para todas as bandas **não-agrupadoras**: `ReportHeader`, `PageHeader`, `PageFooter`, `ReportFooter`, e também como `Header`/`Footer` de grupos e sub-detalhes (`Bands.cs:35`).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Kind` | `BandKind` | — (obrigatório) | Categoria. |
| `Height` | `Unit` | — (obrigatório) | Altura declarada. |
| `Elements` | `EquatableArray<ReportElement>` | — (obrigatório) | Elementos. |
| `Visible` | `bool` | `true` | Renderização estática. |
| `VisibleExpression` | `string?` | `null` | Expressão de visibilidade (modelo). |
| `PrintOnFirstPage` | `bool` | `true` | Quando `false`, **suprime** a banda na página 1. Aplicado a `PageHeader`/`PageFooter` (§2.3.5). |
| `PrintOnLastPage` | `bool` | `true` | Quando `false`, **suprime** a banda na última página — exige 2 passes (§2.3.5). |
| `PageBreak` | `PageBreak` | `PageBreak.None` | Quebra de página em torno da emissão da banda (§2.1.6). |

Fábrica: `ReportBand.Empty(BandKind kind)` → banda de altura zero e sem elementos.

> Semântica de `PrintOnFirstPage`/`PrintOnLastPage`: são herança do modelo header/footer-suppression e **não** mapeiam para o RDL `PageBreak`; por isso coexistem com o campo `PageBreak`. No paginador atual apenas `PageHeader` e `PageFooter` consultam esses dois flags (`ReportPaginator.cs:937,944,961,965`). Em `ReportHeader`/`ReportFooter` os flags existem no record mas a engine não os lê.

### 2.1.4. `DetailBand`

`sealed record` — a banda de detalhe, repetida uma vez por linha da iteração primária (`Bands.cs:63`). É a única banda **obrigatória** de um `ReportDefinition` (parâmetro posicional do construtor).

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Height` | `Unit` | — (obrigatório) | Altura declarada (piso). |
| `Elements` | `EquatableArray<ReportElement>` | — (obrigatório) | Elementos por linha. |
| `Visible` | `bool` | `true` | Estático. |
| `VisibleExpression` | `string?` | `null` | Expressão de visibilidade (campo do modelo). **Não consultada** pelo paginador para a banda de detalhe — ver §2.4. |
| `CanGrow` | `bool` | `false` | Campo do modelo. O *crescimento real* é decidido por elemento (`TextBoxElement.CanGrow`), não por este flag de banda — §2.3.4. |
| `CanShrink` | `bool` | `false` | Opt-in que permite a banda **colapsar abaixo** da `Height` declarada para envolver o conteúdo (§2.3.4). Consultado pela engine. |
| `SubDetails` | `EquatableArray<SubDetailBand>` | `default` (vazio) | Sub-bandas aninhadas (§2.1.7). |
| `NoRowsMessage` | `string?` | `null` | Mensagem centralizada exibida quando a iteração resulta em zero linhas (§2.3.6). Espelha RDL `<NoRows>`. |
| `FilterExpression` | `string?` | `null` | Expressão booleana por linha; linhas falsas são descartadas (§2.2.2). Espelha RDL `<Filters>`. |
| `SortExpressions` | `EquatableArray<SortDescriptor>` | `default` (vazio) | Ordenação estável aplicada antes da iteração (§2.2.2). |
| `PageBreak` | `PageBreak` | `PageBreak.None` | Quebra antes/depois da emissão do detalhe (§2.3.5). |
| `DataSetName` | `string?` | `null` | Dataset que dirige o loop de detalhe. Quando `null`, cai para `PrimaryDataSource` → primeira fonte declarada → primeira registrada (§2.2.1). |

Constante: `DetailBand.Empty` = `new(Unit.Zero, EquatableArray<ReportElement>.Empty)`.

> Semântica de `DataSetName` (do comentário em `Bands.cs:75`): diferente de `SubDetailBand.DataMember` (que é relação *ou* fonte), aqui é **um nome simples de dataset**. Se não casar com nenhum `DataSourceDefinition`, o loop ainda itera a fonte registrada com esse nome — apenas sem suas relações/calculated fields/filtro.

### 2.1.5. `GroupBand`

`sealed record` — banda agrupadora que emite header antes da primeira linha do grupo e footer após a última (`Bands.cs:134`). Não tem `Elements`/`Height` próprios: deriva-os do par `Header`/`Footer`.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | — (obrigatório) | Nome lógico do grupo. |
| `GroupExpression` | `string` | — (obrigatório) | Expressão de chave; a transição de seu valor delimita instâncias de grupo (§2.3.3). |
| `Header` | `ReportBand?` | `null` | Banda de cabeçalho (emitida ao abrir a instância). |
| `Footer` | `ReportBand?` | `null` | Banda de rodapé (emitida ao fechar a instância). |
| `KeepTogether` | `bool` | `false` | Quando `true`, garante que `Header + Footer` cabem no espaço restante antes de abrir; senão snake/quebra (§2.3.7). |
| `NewPageBefore` | `bool` | `false` | Legado: quebra antes da instância. |
| `NewPageAfter` | `bool` | `false` | Legado: quebra depois da instância. |
| `RepeatHeaderOnNewPage` | `bool` | `false` | Campo do modelo. **Não implementado** no paginador — §2.4. |
| `Visible` | `bool` | `true` | Estático. |
| `VisibleExpression` | `string?` | `null` | Expressão de visibilidade (modelo). |
| `PageBreak` | `PageBreak` | `PageBreak.None` | Controle unificado moderno; vence sobre os bools (§2.1.6). |
| `FilterExpression` | `string?` | `null` | Omite instâncias de grupo cujas linhas não passam (campo do modelo). |
| `SortExpressions` | `EquatableArray<SortDescriptor>` | `default` (vazio) | Ordena qual instância de grupo renderiza primeiro (campo do modelo). |
| `Variables` | `EquatableArray<ReportVariable>` | `default` (vazio) | Variáveis com escopo de grupo (campo do modelo). **Não materializadas** pelo paginador — §2.4. |

Membros derivados:

- `Kind` ⇒ `BandKind.GroupHeader` (constante).
- `Height` ⇒ `(Header?.Height ?? Unit.Zero) + (Footer?.Height ?? Unit.Zero)` (`Bands.cs:152`).
- `Elements` ⇒ concatenação de `Header.Elements` e `Footer.Elements` (`Bands.cs:153`).

Método `EffectivePageBreak()` (`Bands.cs:160`) — unifica o enum moderno e o par de bools legados:

```csharp
public PageBreak EffectivePageBreak()
{
    if (PageBreak != PageBreak.None) return PageBreak;   // enum vence se != None
    return (NewPageBefore, NewPageAfter) switch
    {
        (true,  true)  => PageBreak.StartAndEnd,
        (true,  false) => PageBreak.Start,
        (false, true)  => PageBreak.End,
        _              => PageBreak.None,
    };
}
```

**Invariante:** se `PageBreak != None`, os bools legados são ignorados. O paginador sempre chama `EffectivePageBreak()`, nunca os bools diretamente (`ReportPaginator.cs:652,790`).

### 2.1.6. `PageBreak`

Enum de quebra de página de uma região de dados (banda/grupo) (`src/Reporting.Core/Bands/PageBreak.cs:27`). Espelha o `BreakLocation` do RDL.

| Valor | Ordinal | Semântica |
|---|---|---|
| `None` | 0 | Sem quebra forçada; flui com o conteúdo vizinho. |
| `Start` | 1 | Quebra **antes** da primeira instância da banda. |
| `End` | 2 | Quebra **depois** da última instância. |
| `StartAndEnd` | 3 | Quebra antes **e** depois (banda sozinha em sua página). |
| `Between` | 4 | **Só grupos**: quebra entre instâncias consecutivas; em bandas não-grupo comporta-se como `None`. |

### 2.1.7. `SubDetailBand`

`sealed record` — banda de detalhe aninhada que itera uma fonte filha para cada linha-pai (`Bands.cs:105`). Equivale ao `DetailReportBand` do DevExpress / sub-band do FastReport.

| Campo | Tipo | Default | Semântica |
|---|---|---|---|
| `Name` | `string` | — (obrigatório) | Nome lógico (designer + serialização). |
| `DataMember` | `string` | — (obrigatório) | Nome de **relação** (master-detail) **ou** de **fonte** (sub-iteração livre). Resolução: relação declarada no pai → fonte registrada → vazio. |
| `Height` | `Unit` | — (obrigatório) | Altura da linha-filha (repetida por linha). |
| `Elements` | `EquatableArray<ReportElement>` | — (obrigatório) | Elementos por linha-filha. |
| `Header` | `ReportBand?` | `null` | Renderizado **uma vez** antes da primeira linha-filha. |
| `Footer` | `ReportBand?` | `null` | Renderizado **uma vez** após a última linha-filha. |
| `Visible` | `bool` | `true` | Quando `false`, a sub-banda não renderiza (consultado — `ReportPaginator.cs:250`). |
| `VisibleExpression` | `string?` | `null` | Quando presente e avalia `false`, a sub-banda é pulada para aquela linha-pai (consultado — `ReportPaginator.cs:251`). |
| `PrintIfEmpty` | `bool` | `false` | Quando `true`, renderiza header/footer mesmo sem linhas-filhas casadas (`ReportPaginator.cs:299`). |
| `NoRowsMessage` | `string?` | `null` | Campo do modelo (mensagem para zero linhas). **Não usado** pelo caminho atual de `EmitSubDetails` — §2.4. |
| `FilterExpression` | `string?` | `null` | Campo do modelo. **Não aplicado** em `EmitSubDetails` (o casamento é só por relação) — §2.4. |
| `SortExpressions` | `EquatableArray<SortDescriptor>` | `default` (vazio) | Campo do modelo. **Não aplicado** em `EmitSubDetails` — §2.4. |

Fábrica: `SubDetailBand.Empty(name, dataMember)`.

### 2.1.8. Slots de banda em `ReportDefinition`

O `ReportDefinition` (`src/Reporting.Core/ReportDefinition.cs:16`) expõe os slots de banda. Apenas `Detail` é posicional/obrigatório; o resto são `init` opcionais:

| Slot | Tipo | Default | Cardinalidade |
|---|---|---|---|
| `Detail` | `DetailBand` | — (obrigatório) | exatamente 1 |
| `ReportHeader` | `ReportBand?` | `null` | 0 ou 1 |
| `PageHeader` | `ReportBand?` | `null` | 0 ou 1 |
| `Groups` | `EquatableArray<GroupBand>` | vazio | 0..N (aninhados; índice 0 = mais externo) |
| `PageFooter` | `ReportBand?` | `null` | 0 ou 1 |
| `ReportFooter` | `ReportBand?` | `null` | 0 ou 1 |

## 2.2. Materialização e ordenação de dados

Antes de qualquer banda ser emitida, o paginador materializa dados (`ReportPaginator.MaterializeAsync` / `MaterializeRowsAsync`, `ReportPaginator.cs:57`).

### 2.2.1. Resolução da fonte primária

`ResolvePrimaryName` (`ReportPaginator.cs:189`) é o **único** ponto de verdade — compartilhado por materialização e render, para que nunca divirjam:

```
Detail.DataSetName
  ?? request.PrimaryDataSource
  ?? Definition.DataSources.FirstOrDefault()?.Name
  ?? request.DataSources.Names.FirstOrDefault()
```

### 2.2.2. Iteração: simples, master-detail e sub-detalhes

A iteração materializa uma `List<IterationRow>`, onde cada `IterationRow` (`ReportPaginator.cs:81`) carrega:
- `Fields` — a linha "viva" (resolve `Fields.X` não-qualificado);
- `SourceRows` — linhas correntes por fonte (resolve `Fields.SourceName.X` qualificado).

Regras (`MaterializeRowsAsync`, `ReportPaginator.cs:85`):

1. **Sem relações** *ou* **com sub-detalhes declarados** → iteração de fonte única: uma `IterationRow` por linha primária. Quando há `SubDetails`, o detalhe-pai dispara **uma vez por linha-pai** (as sub-bandas iteram os filhos no render — `ReportPaginator.cs:114`).
2. **Com relações e sem sub-detalhes** → iteração master-detail: usa a **primeira** relação (`relations[0]`) como driver; para cada linha-pai, emite uma `IterationRow` por filho casado (`KeysMatch`, com coerção numérica via `decimal` — `ReportPaginator.cs:215`). `Fields` fica ligado ao filho; pai acessível via `Fields.<Parent>.X`. É **inner-join** (pais sem filhos não emitem linha; o ramo outer está comentado — `ReportPaginator.cs:175`).

**Filtro + ordenação** (`ApplyFilterAndSort`, `ReportPaginator.cs:348`) aplicam-se em ordem, dentro do contexto de expressão (veem `Parameters`/`Variables`):

1. Nível de **data source** (`DataSourceDefinition.FilterExpression`/`SortExpressions`).
2. Nível de **região de dados** (`DetailBand.FilterExpression`/`SortExpressions`).

Filtro: linha mantida sse `IsTruthy(resultado)` — `true` bool, string não-vazia, numérico não-zero, ou referência não-nula (`ReportPaginator.cs:417`). Ordenação: estável, multi-chave, com coerção numérica e fallback ordinal por `ToString()` (`CompareValues`, `ReportPaginator.cs:432`). Sem filtro nem sort, retorna a lista de entrada sem cópia (no-op de custo zero).

Após filtro+sort, `ctx.PrimeReportScope(rows)` injeta o conjunto completo no escopo de agregação `Report`, para que agregados sem escopo (`Sum`/`Count`/…) resolvam o total do dataset **em todas as bandas**, inclusive `ReportHeader`/`PageHeader` que renderizam antes do loop (`ReportPaginator.cs:498`).

## 2.3. Paginação e layout

### 2.3.1. A invariante Measure ≡ Render

O ponto cardinal da engine: `BandRenderer.Measure(band, ctx)` (pré-check de quebra de página) deve produzir **exatamente** a mesma altura que `BandRenderer.Render(band, origin, ctx)` consome. Se divergissem, no caminho sem-piso (`CanShrink`) a próxima banda sobreporia ou deixaria gap.

Ambos delegam ao **mesmo** par de funções (`BandRenderer.cs`):

- `EffectiveElementBottom(element, ctx)` (`BandRenderer.cs:91`) — o *bottom* efetivo de um elemento em band-space: `Bounds.Y + altura efetiva`, honrando `CanGrow`/`CanShrink` de um `TextBoxElement` e o **mesmo** estilo efetivo (property bindings → conditional format → fonte/format) que o render usa. Elemento invisível contribui `Unit.Zero`.
- `EffectiveBandHeight(band, contentExtent)` (`BandRenderer.cs:140`) — altura final da banda.

```csharp
private static Unit EffectiveBandHeight(IBand band, Unit contentExtent)
    => BandAllowsShrink(band) && contentExtent > Unit.Zero
        ? contentExtent                       // opt-in shrink: usa só o conteúdo
        : Max(band.Height, contentExtent);    // padrão: Height declarada é PISO
```

`Render` acumula `contentExtent` de cada elemento e aplica `EffectiveBandHeight`; `Measure` percorre os mesmos elementos chamando `EffectiveElementBottom` e aplica `EffectiveBandHeight` (`BandRenderer.cs:433`). Single source of truth ⇒ não há divergência possível.

### 2.3.2. `PageAccumulator` — geometria de página

Estado mutável da página corrente (`PageAccumulator.cs:12`). Campos/propriedades-chave:

| Membro | Semântica |
|---|---|
| `CurrentY` | Y corrente de fluxo (começa em `Margins.Top`). |
| `ContentBottom` | Limite inferior do conteúdo. Em papel contínuo é `int.MaxValue/2` (efetivamente sem fundo); senão `PageHeight − Margins.Bottom − pageFooterHeight` (`ReportPaginator.cs:507`). |
| `Fits(h)` | `CurrentY + h <= ContentBottom`. |
| `RemainingInColumn` | `ContentBottom − CurrentY` (espaço restante na coluna). |
| `FullColumnHeight` | `ContentBottom − columnTop` — o máximo que um slice de banda pode ocupar. |
| `AtColumnTop` | `CurrentY <= columnTop` (nada emitido ainda na coluna). |
| `Origin` | `(Margins.Left + (colWidth+spacing)*coluna, CurrentY)`. |
| `Emit(prims, h)` | adiciona primitivos e **avança** `CurrentY += h`. |
| `EmitFixed(prims)` | adiciona primitivos **sem** avançar Y (usado pelo PageFooter ancorado). |
| `MarkColumnTop()` | grava onde o conteúdo de coluna começa (após report/page header). |
| `AdvanceColumn()` | avança para a próxima coluna snake (reset `CurrentY = columnTop`); `false` quando a última coluna está cheia. |
| `Flush()` | fecha a página, incrementa `PageNumber`, reseta Y/coluna. |

### 2.3.3. Ordem de render das bandas

Por pass (`ExecutePass`, `ReportPaginator.cs:447`), a ordem é determinística:

1. **`ReportHeader`** — emitido uma vez, sem pré-check (assume-se caber na página 1) (`ReportPaginator.cs:513`).
2. **`PageHeader`** — `EmitPageHeader` (sujeito a `PrintOnFirstPage`/`PrintOnLastPage`) (`ReportPaginator.cs:518`).
3. `MarkColumnTop()` — colunas snake começam abaixo dos headers.
4. **Loop de linhas** (`ReportPaginator.cs:546`), por iteração em 6 fases:
   - **Fase 1** — sonda as chaves de grupo da nova linha **sem** comitar ao acumulador (`SetCurrentRowNoSnapshot`), avaliando `GroupExpression` de cada grupo.
   - **Fase 2** — fecha grupos cuja chave mudou (do mais externo alterado **para dentro**, em ordem reversa). Antes de fechar, restaura `lastCommittedRow` para o footer ver os dados do grupo **que fecha**, não da nova linha (`ReportPaginator.cs:592`). `CloseGroup` emite o footer e aplica `PageBreak` `End`/`StartAndEnd`/`Between`.
   - **Fase 3** — comita a nova linha (`SetCurrentRow`), reafirma contextos de fonte, avalia **CalculatedFields** sequencialmente (cada um visível ao próximo).
   - **Fase 4** — abre grupos ainda fechados; aplica `PageBreak` `Start`/`StartAndEnd`/`Between` **antes** de abrir (só se `CurrentY > Margins.Top + pageHeaderHeight`, i.e., não no topo virgem). `OpenGroup` emite o header.
   - **Fase 5** — emite a banda de **detalhe**. Se `detailHeight > FullColumnHeight` → split por elemento (§2.3.4); senão `EnsureRoom` + `Render` + `Emit`.
   - **Fase 6** — emite cada `SubDetailBand` declarada (`EmitSubDetails`): header (uma vez) → elementos por linha-filha → footer (uma vez); ao terminar, restaura a linha-pai para footers/agregados subsequentes (`ReportPaginator.cs:684`).
5. **Fecha grupos remanescentes** (do mais interno ao mais externo) (`ReportPaginator.cs:697`).
6. `Detail.PageBreak` `End`/`StartAndEnd` → quebra após a última linha (se houve linhas) (`ReportPaginator.cs:708`).
7. **`ReportFooter`** — sujeito a seu próprio `PageBreak` `Start`/`StartAndEnd` (footer em página própria) (`ReportPaginator.cs:715`).
8. **`PageFooter`** — `EmitPageFooter` (ancorado embaixo) + `page.Flush()` (`ReportPaginator.cs:728`).

A cada quebra de página (`BreakPage`, `ReportPaginator.cs:920`) a sequência é: `EmitPageFooter` → `Flush` → `ResetPage` → atualiza `PageNumber` → `EmitPageHeader` → `MarkColumnTop`. Ou seja, **o PageHeader repete em cada página nova; o ReportHeader não**.

### 2.3.4. Crescimento, encolhimento e split de banda

**Crescimento por elemento** — `TextBoxElement.CanGrow`: o measurer mede o texto na largura disponível; se exceder `Bounds.Height`, a altura do textbox cresce e empurra o `contentExtent` da banda (`BandRenderer.cs:107`, `577`). O fill `BackColor`/`BackgroundImage` é corrigido para a altura final (`BandRenderer.cs:226`). `Tablix` cresce à altura do grid; um `Rectangle` container cresce aos filhos.

**Encolhimento de banda** (`CanShrink`, opt-in **shrink-safe**) — `BandAllowsShrink(band)` (`BandRenderer.cs:122`) é `true` apenas quando:
- `band is DetailBand { CanShrink: true }`, **e**
- **todos** os elementos são `IsShrinkSafe` (`BandRenderer.cs:130`): `TablixElement` ⇒ `false`; `RectangleElement` com filhos ⇒ `false`; demais (Text/Label/Line/Image/Chart/…) ⇒ `true`.

A condição shrink-safe é exatamente o que preserva Measure ≡ Render: elementos que crescem **além** de `Bounds` no render-time (Tablix, container com filhos) quebrariam a igualdade, então uma banda que os contenha mantém a `Height` declarada em vez de encolher.

**Split de banda por elemento** (`EmitBandSplit`, `ReportPaginator.cs:802`) — uma banda mais alta que uma **coluna inteira** (`detailHeight > FullColumnHeight`) nunca cabe nem em página virgem, então é fatiada **elemento-a-elemento** (cada elemento permanece **inteiro** — texto não é cortado no meio de uma linha nesta engine estática):
- Elementos são ordenados por `Bounds.Y` (topo→base).
- Cada slice acumula elementos cujo `EffectiveElementBottom − sliceTop <= RemainingInColumn`.
- Slice renderizado rebaseado: `origin.Y = CurrentY − sliceTop` (o topo do slice cai no topo da nova página/coluna).
- Um único elemento mais alto que a coluna inteira (`AtColumnTop` e nada cabe) é emitido **sozinho** (transborda — texto não é line-split), garantindo **progresso e terminação**.
- **Whitespace declarado**: ao final, `trailing = bandTarget − reached` (a `Height` declarada além do conteúdo) é fluído pelas páginas, preservando `Measure = Max(Height, conteúdo)`. Banda shrink-opt-in tem `bandTarget == conteúdo` ⇒ trailing zero (no-op).

### 2.3.5. Quebras de página por banda/grupo

| Origem | Gatilho | Local de quebra | Ref |
|---|---|---|---|
| `Detail.PageBreak` `Start`/`StartAndEnd` | antes da 1ª linha (só com dados; segura o break até a 1ª iteração) | antes do detalhe | `ReportPaginator.cs:530,548` |
| `Detail.PageBreak` `End`/`StartAndEnd` | após a última linha | depois do detalhe | `ReportPaginator.cs:708` |
| `GroupBand.EffectivePageBreak()` `Start`/`StartAndEnd`/`Between` | ao abrir a instância | antes do header (se não no topo) | `ReportPaginator.cs:652` |
| `GroupBand.EffectivePageBreak()` `End`/`StartAndEnd`/`Between` | ao fechar a instância | depois do footer | `ReportPaginator.cs:790` |
| `ReportFooter.PageBreak` `Start`/`StartAndEnd` | antes do report footer | footer em página própria | `ReportPaginator.cs:718` |

Quebra "antes" só dispara se `CurrentY > Margins.Top + pageHeaderHeight` — evita uma página em branco quando já se está no topo.

**Supressão de header/footer de página** (`EmitPageHeader`/`EmitPageFooter`, `ReportPaginator.cs:930,952`):
- `PrintOnFirstPage = false` → suprime quando `PageNumber == 1` (conhecido no 1º pass).
- `PrintOnLastPage = false` → suprime quando `PageNumber == ctx.TotalPages` — **só conhecido no 2º pass** (`ctx.TotalPages > 0`).
- O `PageFooter` é ancorado embaixo via `EmitFixed` (não consome Y), então suprimi-lo nunca reflui conteúdo nem muda a contagem de páginas.

### 2.3.6. `NoRowsMessage` (RDL `<NoRows>`)

Quando a iteração filtrada resulta em **zero** linhas e `Detail.NoRowsMessage` é não-vazio, o paginador sintetiza uma `DetailBand` de uma linha com um `LabelElement` centralizado (horizontal+vertical), largura = área do corpo, altura = `12 mm` (`BuildNoRowsBand`, `ReportPaginator.cs:738`). É emitida **antes** do report footer, exatamente onde o detalhe apareceria (`ReportPaginator.cs:537`).

### 2.3.7. `KeepTogether` de grupo

Ao abrir um grupo com `KeepTogether = true`, o paginador exige espaço para `Header.Height + Footer.Height`; se não couber, tenta a **próxima coluna** (snake) antes de quebrar a página física inteira (`OpenGroup`, `ReportPaginator.cs:764`):

```csharp
var needed = group.Header.Height + (group.Footer?.Height ?? Unit.Zero);
if (group.KeepTogether && !page.Fits(needed) && !page.AdvanceColumn())
    BreakPage(...);
else
    EnsureRoom(page, group.Header.Height, ...);
```

> Limitação: o `KeepTogether` garante que **header + footer** cabem juntos, mas não que **todas as linhas de detalhe** do grupo fiquem na mesma página.

### 2.3.8. Modo contínuo e colunas (snake)

- **Papel contínuo** (`PageSetup.IsContinuous`, i.e. `Paper.Height == 0` — Thermal58/80): `ContentBottom` é "infinito", o `PageFooter` é emitido **inline** no Y corrente (sem âncora inferior — `ReportPaginator.cs:970`), e o `PageAccumulator` **força uma única coluna** (`PageAccumulator.cs:27`). Contínuo + colunas ⇒ 1 coluna, intencionalmente.
- **Colunas snake** (`PageSetup.Columns > 1`, papel paginado): largura de coluna = `(ContentWidth − spacing*(N−1)) / N` (`PageAccumulator.cs:29`). `EnsureRoom`/`BreakOrAdvance` tentam `AdvanceColumn()` antes de `BreakPage` (`ReportPaginator.cs:905,897`). As colunas começam abaixo do report/page header (`MarkColumnTop`).

`PageSetup` relevante (`src/Reporting.Core/Paper/PageSetup.cs:28`):

| Campo | Tipo | Default |
|---|---|---|
| `Paper` | `PaperSize` | — (obrigatório) |
| `Orientation` | `Orientation` | `Portrait` |
| `Margins` | `Thickness` | `default` (zero) |
| `Columns` | `int` | `1` |
| `ColumnSpacing` | `Unit` | `default` (zero) |

## 2.4. Estado de implementação (campos do modelo não totalmente honrados)

Por precisão de spec, os campos abaixo **existem** no modelo e fazem round-trip na serialização/designer/CodeFirst, mas **não são consumidos pela engine de layout** (`Reporting.Layout` não os referencia):

| Campo | Status |
|---|---|
| `GroupBand.RepeatHeaderOnNewPage` | Round-trip completo (RepX/RepJSON/RDL writer + `GroupBuilder.RepeatHeader` + Designer), mas **zero referências no paginador** — o cabeçalho de grupo **não** é repetido ao quebrar página. |
| `GroupBand.Variables` (`ReportVariable` com `VariableScope.Group`) | `context.Variables` é resolvível pelo evaluator (`ExpressionEvaluator.cs:96`), porém o paginador **não materializa** `group.Variables` no contexto durante o ciclo de vida do grupo. |
| `GroupBand.FilterExpression` / `GroupBand.SortExpressions` | Campos do modelo (doc-comment descreve a intenção RDL); o `ExecutePass` aplica filtro/sort apenas nos níveis **data source** e **Detail** — não há passo equivalente em torno da transição de grupo. |
| `SubDetailBand.NoRowsMessage` / `FilterExpression` / `SortExpressions` | Não aplicados em `EmitSubDetails` — o casamento de filhos é só por relação; sem filtro/sort/no-rows local. |
| `DetailBand.VisibleExpression` / `DetailBand.CanGrow` | `VisibleExpression` da banda de detalhe não é avaliado pelo loop (visibilidade é decidida por elemento, via `BandRenderer.IsVisible`); o crescimento real vem de `TextBoxElement.CanGrow`, não do flag de banda. |
| `ReportBand.PrintOnFirstPage` / `PrintOnLastPage` em `ReportHeader`/`ReportFooter` | Lidos apenas para `PageHeader`/`PageFooter`; nas bandas de relatório os flags existem mas a engine não os consulta. |

## 2.5. Exemplo (code-first / records)

Definição mínima com header de relatório, um grupo, detalhe que cresce, e rodapé de página suprimido na primeira página:

```csharp
var detail = new DetailBand(
    Height: Unit.FromMm(8),
    Elements: new EquatableArray<ReportElement>(new ReportElement[]
    {
        new TextBoxElement { Expression = "{Fields.nome}", CanGrow = true,
                             Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(80), Unit.FromMm(8)) },
    }),
    CanShrink: true,                       // banda colapsa ao conteúdo (shrink-safe)
    NoRowsMessage: "Sem registros.");      // RDL <NoRows>

var grupo = new GroupBand(
    Name: "porUF",
    GroupExpression: "{Fields.uf}",
    Header: new ReportBand(BandKind.GroupHeader, Unit.FromMm(6),
                           EquatableArray<ReportElement>.Empty),
    Footer: new ReportBand(BandKind.GroupFooter, Unit.FromMm(6),
                           EquatableArray<ReportElement>.Empty),
    KeepTogether: true,
    PageBreak: PageBreak.Between);          // cada UF começa em página nova

var def = new ReportDefinition("Clientes", PageSetup.A4Portrait, detail)
{
    ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(20),
                                  EquatableArray<ReportElement>.Empty),
    Groups = new EquatableArray<GroupBand>(new[] { grupo }),
    PageFooter = new ReportBand(BandKind.PageFooter, Unit.FromMm(10),
                                EquatableArray<ReportElement>.Empty,
                                PrintOnFirstPage: false),  // suprime no rodapé da pág. 1
};
```

## 2.6. Resumo de invariantes

1. **Measure ≡ Render** — `BandRenderer.Measure` e `BandRenderer.Render` produzem a mesma altura (compartilham `EffectiveElementBottom` + `EffectiveBandHeight`). Pré-check de quebra nunca diverge da emissão.
2. **Altura declarada é piso** — `EffectiveBandHeight = Max(Height, conteúdo)`, exceto banda `DetailBand{CanShrink:true}` shrink-safe com conteúdo > 0, onde vira `conteúdo`.
3. **Fonte única de resolução** — `ResolvePrimaryName` é idêntico em materialização e render; divergir faria iterar uma fonte e publicar/filtrar outra.
4. **Quebra "antes" não cria página em branco** — só dispara se `CurrentY > Margins.Top + pageHeaderHeight`.
5. **Enum vence bools** — `GroupBand.EffectivePageBreak()`: `PageBreak != None` ignora `NewPageBefore`/`NewPageAfter`.
6. **2 passes ⇔ `Page.Total` ou last-page gating** — o segundo pass só ocorre se alguma expressão referencia `Page.Total`/`Page.TotalPages` **ou** algum `PageHeader`/`PageFooter` tem `PrintOnLastPage = false` (`UsesTotalPages`/`UsesLastPageGating`, `ReportPaginator.cs:998,1003`); suprimir a última página nunca altera a contagem.
7. **Split termina** — `EmitBandSplit` sempre faz progresso (um elemento sozinho, mesmo transbordando, é emitido e removido), garantindo terminação.
8. **PageHeader repete; ReportHeader não** — `BreakPage` reemite o PageHeader em cada página nova; o ReportHeader é emitido uma única vez no início do pass.
