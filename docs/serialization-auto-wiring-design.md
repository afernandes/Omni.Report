# Auto-wiring de serialização para novos elementos

**Status:** proposta · **Escopo:** reduzir o boilerplate ao adicionar um `ReportElement`
**Relacionado:** [property-grid-metadata-design.md](property-grid-metadata-design.md) (mesma filosofia: metadados dirigem o genérico)

## Problema

Adicionar um `ReportElement` hoje exige editar **4 switches** à mão, além de definir o record:

| Arquivo | O que se edita |
|---|---|
| `RepxWriter.cs` | braço `switch` por tipo → `(tag, WriteXxxContent)` + helper de payload |
| `RepxReader.cs` | braço `switch` por tag-string → `ReadXxxElement` factory |
| `RepJsonWriter.cs` | `ElementKindFor` (tipo→tag) + `case` no `switch(element)` |
| `RepJsonReader.cs` | braço `switch` por `kind` → `new XxxElement { ... }` |

Esquecer um dos quatro produz round-trip assimétrico (escreve mas não lê, ou vice-versa). A rede de
paridade da PR #64 (`ReflectionRoundTripTests`) detecta isso — mas só **depois** do bug, e não reduz
o trabalho. Isso contraria a meta "simplificar a criação de novos componentes".

## O que JÁ é genérico (não é o problema)

Cerca de **metade** da serialização de cada elemento já é compartilhada e escrita/lida uma única vez:

- **Envelope base** (14 campos de `ReportElement`): `Id`, `Name`, `Bounds`, `Visible`,
  `VisibleExpression`, `Style`, `ConditionalFormats`, `PropertyExpressions`, `Action`, `Bookmark`,
  `DocumentMapLabel`, `ToggleItemId`, `InitiallyHidden`. No reader, reaplicado via `element with { ... }`.
- **Conversores de leaf** (`Formats.cs`): `Unit` (mils), `Rectangle`/`Thickness` ("X,Y,W,H"), `Color`
  (hex), `Type` (AQN+fallback), tudo `InvariantCulture`.
- **A tag do elemento já é convenção**: `TypeName` menos o sufixo `Element` (`LabelElement`→`"Label"`).

O que sobra **manual** é só o miolo por-tipo: o payload específico do subtipo + os dois pares de
dispatch tag↔tipo (que a convenção acima já poderia derivar).

## Restrições (inegociáveis)

1. **Round-trip byte-a-byte** dos 18 elementos existentes. Arquivos `.repx`/`.repjson` já gravados
   (inclusive por SSRS/terceiros) devem continuar carregando idênticos. → Não podemos reescrever o
   formato dos elementos existentes.
2. **Code-first / low-level intactos.** O serializer não dita a construção do modelo; `PropertyExpressions`
   e props são setadas direto no record. Auto-wiring não pode assumir que toda prop serializa.
3. **Paridade dual-format.** Os mesmos metadados devem dirigir repx (XML) e repjson (JSON) de forma
   consistente, senão os formatos divergem.
4. **Records imutáveis** com membros `required` e construtores posicionais.

## Abordagem escolhida: caminho genérico por convenção, como *fallback* opt-in

> Os 18 elementos existentes **mantêm seus switches** (risco zero à restrição #1). Um elemento **novo**
> que siga a convenção cai num **caminho genérico** e ganha os 4 serializers **sem editar switch nenhum**.
> Elementos simples existentes podem migrar depois, um a um, **só quando provado byte-idêntico**.

Por que fallback e não rewrite: reproduzir o formato ad-hoc atual (nomes de tag não-identidade,
attr-vs-elemento, omissões) por reflexão para 18 elementos é arriscado e de baixo retorno. O ganho real
("novos componentes de graça") não exige tocar nos existentes.

### Componentes

1. **`ElementSerializationRegistry`** (reflexão, montado uma vez, cacheado):
   - Descobre todo subtipo concreto de `ReportElement`.
   - `tag` = atributo `[SerializedElement("Tag")]` se presente, senão convenção (`TypeName` sem `Element`).
   - Mapas `tag→Type` e `Type→tag`. É a fonte única do dispatch (elimina a duplicação dos 4 switches
     para elementos no caminho genérico).

2. **Convenção de propriedade** (só as props **declaradas no subtipo** — as 14 da base já são genéricas):
   - Nome no wire: `[SerializedAs("nome")]` se presente, senão camelCase (json) / PascalCase (xml).
   - Placement: **sempre elemento-filho** (uniforme) no genérico — sem hoist ad-hoc de atributo. Novos
     elementos aceitam essa forma; não estão presos a arquivos legados.
   - Esparso: omite quando o valor é igual ao default da prop. **O "default" tem que ser o valor lido de
     uma _instância materializada_ do record** (via o construtor de defaults), **não `default(T)`** — senão
     props com default não-`default(T)` (ex.: `MapElement.ShapeFill = "#E8EDE4"`) seriam omitidas no write e
     lidas como `null`, perdendo o valor. Writer e reader devem usar a **mesma** instância-default como fonte.
   - **Membros posicionais obrigatórios** (sem default no record: `TablixGroup.Name`, `GaugeRange.*`,
     `IndicatorState.*`, `TextRun.Value`, `ChartSeries.*`) **não têm** instância-default → política fixa:
     **sempre emitir, nunca por omissão**.

3. **Registro de conversores de leaf** (`CLR type → (toString, parse)`), reusando `Formats.*`:
   - `Unit`, `Color`, `Rectangle`, `Thickness`; primitivos (`string`/`bool`/`int`/`double`, invariantes);
     enums (`ToString`/`Enum.Parse`); `byte[]`/`EquatableArray<byte>` → Base64.
   - **`Color?` é conversor de PRIMEIRA CLASSE** com a regra vazio↔`null`: hoje `ParseColor("")` retorna
     `Color.Transparent` (≠ `null`) e a regra "vazio→null" vive **inline no reader** (`RepxReader.ReadOptionalColor`).
     O conversor genérico precisa internalizar essa regra, senão quebra o round-trip de `Color?`.
   - **NÃO incluir `Type`** no conjunto de conversores de elemento: `Formats.ParseType` lança para tipos
     fora da lista BCL; nenhum elemento usa `Type` hoje — oferecê-lo convida quebra futura.
   - **Record aninhado**: recursão (objeto-filho com as props declaradas dele). Posicionais → ver §5.
   - **`EquatableArray<T>`**: filhos repetidos / array JSON de `T`.
   - **`ReportElement` aninhado** (ex.: `TablixCell.Content`): recursão no próprio registry/dispatch.

4. **Write genérico** (xml+json): envelope existente + `<tag>` + para cada prop específica →
   conversor → filho esparso.

5. **Read genérico** (xml+json): `tag→Type`; parse das props específicas num mapa `nome→valor`; constrói:
   - Casa parâmetros do **construtor posicional** por **nome** (case-insensitive) com os valores; defaults
     para ausentes. Para records só-`init` (sem ctor posicional), usa o ctor implícito + `SetValue`.
   - Seta as init-only restantes via reflexão. **Nota CLR**: `required`/`init` são regras só do *compilador*
     — em IL o setter `init` é um `set_X` comum, então `PropertyInfo.SetValue` o chama normalmente fora de
     object-initializer (a `Populate` da rede #64 já faz exatamente isso, e a suíte é verde). O risco é o
     inverso: o compilador **não** avisa se o registry esquecer um `required` → daí a **validação
     pós-construção** (§Pontos de toque, item 7).
   - O envelope base continua reaplicado pelo bloco genérico existente — `ReadGeneric` só devolve a casca
     concreta com as props próprias.
   - **Metadados**: avaliar **reusar/estender o `[PropertyGrid]` existente** (já anota props dos elementos)
     em vez de criar `[SerializedAs]`/`[SerializedElement]` paralelos — dois sistemas de metadados de
     reflexão sobre a mesma prop é dívida garantida. Decisão a fechar na PR 1.

6. **Wiring:** o `_ =>` (fallthrough) dos 4 switches passa a chamar o caminho genérico em vez de lançar.
   Os `case`/braços existentes têm precedência.

### Pontos de toque que o genérico precisa cobrir (zero-touch real)

Não são "4 switches": são **cinco** pontos de dispatch + duas regras. Um elemento novo só é zero-touch se
o caminho genérico cobrir TODOS:

1. `RepxWriter.WriteElement` — `switch` por tipo (`_ =>` lança).
2. `RepxReader.ReadElement` — `switch` por tag-string (`_ =>` lança).
3. `RepJsonWriter.WriteElement` — `switch(element)` por tipo (sem `default` → perda silenciosa).
4. `RepJsonReader.ReadElement` — `switch` por `kind` (`_ =>` lança).
5. **`RepJsonWriter.ElementKindFor`** (linha 636-658) — `switch` SEPARADO tipo→tag (`_ =>` lança). É o
   quinto ponto; precisa cair no registry junto com os outros.
6. **Exclusão de nomes-de-envelope**: o reader genérico de props específicas deve **ignorar** os nomes
   reservados do envelope (`Style`, `Action`, `ConditionalFormats`, `PropertyExpressions`, `Id`, `Bounds`,
   `Visible`, `Name`, `VisibleExpression`, `Bookmark`, `DocumentMapLabel`, `ToggleItemId`, `InitiallyHidden`).
   Se um subtipo novo declarar uma prop com um desses nomes, é **colisão de wire** → o registry deve rejeitar
   em tempo de descoberta.
7. **Validação pós-construção**: como `required`/`init` são **inertes sob reflexão** (o compilador não
   protege), o materializador deve verificar que todo parâmetro de ctor + todo membro `required` foi
   atribuído, lançando caso contrário — senão um `TextBox` com `Expression = null` (impossível em C# normal)
   vaza silenciosamente.

> **Elementos que NUNCA migram** (placement divergente entre formatos, irreproduzível pela convenção
> uniforme): `TextBox` (`CanGrow/CanShrink` são **atributo** no repx mas **propriedade** no json),
> `Image`, `Line` (`Pen` achatado em 3 atributos). Eles ficam nos switches permanentemente — e tudo bem,
> a meta é só **novos** elementos.

### Validação

- A rede #64 **já cobre** qualquer elemento novo (descobre todos os subtipos e valida round-trip nos dois
  formatos). É o guarda-costas da implementação.
- **Round-trip contra um elemento NOVO de teste** (não byte-identidade contra um existente). Nenhum
  elemento atual é byte-compatível com a convenção esparsa proposta — eles misturam campos sempre-emitidos,
  vazios não-esparsos (`Ellipse` emite `<FillColor></FillColor>` no repx) e atributos hasteados. A prova
  correta é: definir um `record _ProbeElement : ReportElement` só-de-teste, **nascido na convenção**, e
  assert que round-trip (save→load) preserva todas as props nos dois formatos, **sem** editar switch algum.

## Rollout incremental

- **PR 1** — `ElementSerializationRegistry` (descoberta + tag↔Type por convenção, com exclusão de
  nomes-de-envelope) + write/read genérico para props **escalares só-`init`**
  (string/bool/enum/Unit/Color/Color?/numérico) + conversor `Color?` de primeira classe + validação
  pós-construção, wired no `_ =>` dos 4 switches **e** no `ElementKindFor`. Decide a relação com
  `[PropertyGrid]`. **Prova: round-trip (save→load nos dois formatos) de um `record _ProbeElement` de teste
  nascido na convenção, com zero edição de switch** — NÃO byte-identidade contra existente (impossível, ver
  §Validação). Os 18 switches/cases existentes permanecem intactos.
- **PR 2** — records aninhados + **construtores posicionais** (casamento por nome) + coleções
  `EquatableArray<T>` + Base64 + `ReportElement` aninhado (recursão). É aqui que o casamento de ctor
  posicional é exercido de verdade.
- **PR 3** — um elemento **genuinamente novo e útil** adicionado com **zero** edição de switch, provando a
  meta end-to-end; doc "como adicionar um componente" reduzido a: definir o record (+ atributos opcionais).

## Riscos

| Risco | Mitigação |
|---|---|
| Construção por reflexão de records `required`/posicionais | casar params por nome; testes unitários + rede #64 |
| Convenção diverge do formato legado | por isso existentes mantêm switches; genérico só p/ novos/opt-in |
| Drift entre repx e repjson | registry único + convenção única + uma tabela de conversores |
| Performance | registry estático cacheado (montado uma vez) |

## Alternativas descartadas

- **Rewrite reflexivo total** (todos os 18 pelo genérico): viola a restrição #1 (byte-a-byte) com alto
  risco e baixo retorno incremental.
- **Source generator**: elimina reflexão em runtime, mas adiciona complexidade de build e ainda precisa
  dos mesmos metadados; pode ser uma evolução futura do registry sem mudar a superfície de convenção.
- **Discriminador polimórfico do System.Text.Json**: resolve só o json e só o dispatch, não o repx nem o
  payload; não dá paridade dual-format.
