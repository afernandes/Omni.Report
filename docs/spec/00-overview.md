# 0. Introdução e escopo

Este documento é a **especificação formal do formato/modelo OmniReport** — o análogo, para o nosso formato nativo, do que a [MS-RDL] é para o RDL da Microsoft. Ele descreve normativamente o **modelo de objetos** (o AST canônico de um relatório), as duas **serializações nativas** (`.repx` XML e `.repjson` JSON), a **linguagem de expressão**, a **semântica de layout/paginação** e o **mapeamento de interop com RDL**.

- **Versão da spec:** 1.0 (alinhada a `ReportDefinition.SchemaVersion = "1.0"`).
- **Status:** normativa para a linha 0.1.x do OmniReport.
- **Público-alvo:** quem implementa serializers, ferramentas (Designer/CLI), integrações que produzem ou consomem relatórios OmniReport, e quem precisa entender exatamente o que cada construção significa.

## 0.1. Filosofia: o modelo nativo é a fonte da verdade

O OmniReport é projetado em torno de um **modelo de objetos imutável** (`sealed record` + igualdade estrutural), e **não** em torno de um formato de arquivo. Decorrências:

1. **O modelo é o AST canônico.** `ReportDefinition` (§1) é a representação autoritativa de um relatório em memória. Tudo o mais — arquivos, o Designer, os exportadores — é uma *projeção* desse modelo.
2. **Três modos de autoria, um só modelo.** *Code-first* (a fachada fluente `ReportBuilder`), *low-level* (construção direta dos `record`s) e *Designer* (WYSIWYG) **produzem exatamente o mesmo `ReportDefinition`**. Nenhum modo é privilegiado; nenhuma construção do modelo é inacessível a qualquer modo. Esta é uma invariante de produto: toda feature descrita nesta spec é exprimível nos três modos.
3. **Duas serializações nativas, lossless.** `.repx` (§7) e `.repjson` (§7) são representações 1:1 do modelo. O contrato de round-trip é `Load(Save(def)).Equals(def)` — igualdade estrutural total (~98% na prática; os ~2% são identidades efêmeras como `ReportElement.Id`, regeneradas, e por isso excluídas da comparação).
4. **RDL é interop, não a fonte da verdade.** O OmniReport **lê, edita e grava RDL** (§8), mas como uma **projeção** do modelo nativo — não como o formato interno. Isso preserva a ergonomia de autoria (bandas nomeadas, dialeto de expressão enxuto) sem abrir mão da interoperabilidade com o ecossistema SSRS.

### 0.1.1. Estratégia de interop RDL (decisão de design)

O RDL é tratado como **formato de projeção/intercâmbio**:

- **Export/import por valor.** O modelo nativo é projetado para a forma do RDL na escrita e reconstruído na leitura. Onde o RDL tem um slot equivalente, o mapeamento é direto (§8).
- **Round-trip RDL-canônico *lossless* via `<CustomProperties>`.** Os poucos campos do modelo nativo sem equivalente direto no RDL (campos *órfãos* — ver §8) são persistidos no elemento `<CustomProperties>` do próprio RDL. `<CustomProperties>` **faz parte do XSD oficial** e é ignorado por leitores que não o conhecem (ex.: Report Builder). Logo, um arquivo RDL produzido pelo OmniReport é **válido pelo XSD, abre no SSRS, e ainda assim preserva 100% do modelo** ao reabrir no OmniReport.
- **"XSD-válido" ≠ "sem extensão".** A validade XSD admite `<CustomProperties>` e o namespace `rd:` (ambos sancionados pela spec do RDL). A pureza "RDL sem nenhuma extensão" é uma restrição *mais forte*, não requerida por esta estratégia.

## 0.2. Invariante de saída estática

O OmniReport produz **relatórios estáticos**. A saída renderizada (PDF, imagem, DOCX, texto) **não tem interatividade de runtime**: sem drill-down, sem toggle de expansão, sem ações de servidor. A **visibilidade estática** é suportada (`Visible` / `VisibleExpression`, avaliada uma vez no render), bem como links/bookmarks de navegação *dentro* do documento. Construções do RDL que dependem de interatividade de runtime (ex.: `ToggleItem` de drill-down) são importadas como visibilidade estática ou sinalizadas, nunca emuladas dinamicamente.

## 0.3. Convenções

- **Palavras-chave normativas.** "DEVE/MUST", "NÃO DEVE/MUST NOT", "DEVERIA/SHOULD" e "PODE/MAY" seguem o sentido usual de especificações.
- **Tipos.** Nomes de tipos, `record`s e campos são citados no original (C#). Tipos CLR (`string`, `int`, `DateTime`, `decimal`) referem-se aos tipos .NET.
- **Defaults.** A coluna "Default" das tabelas de campos reflete o valor-padrão do construtor/`init` no código. `—` indica parâmetro posicional obrigatório (sem default).
- **Rastreabilidade.** Referências `arquivo.cs:linha` apontam para a implementação no momento desta versão; podem deslocar-se, mas o nome do tipo/membro é estável.
- **Imutabilidade.** Salvo nota em contrário, todo tipo do modelo é um `sealed record` imutável; "definir um campo" significa construí-lo ou produzir uma cópia via `with`.

## 0.4. Conformância

Um **leitor conforme** DEVE reconstruir o `ReportDefinition` exato a partir de qualquer serialização nativa que ele mesmo poderia ter produzido (round-trip por igualdade estrutural, excluídas identidades efêmeras). Um **escritor conforme** NÃO DEVE descartar silenciosamente informação do modelo: dados sem representação no formato-alvo DEVEM ser preservados (ex.: `<CustomProperties>` no RDL) ou reportados via avisos (`ImportWarnings`/`ExportWarnings`), nunca perdidos sem sinal. Um **renderizador conforme** DEVE tratar a saída como estática (§0.2) e aplicar `Style.Format` de forma uniforme em todos os renderers (§5).

## 0.5. Organização

| § | Seção | Conteúdo |
|---|---|---|
| 1 | [Modelo de objetos: `ReportDefinition`](01-object-model.md) | O record raiz, a árvore do modelo, `PageSetup`, igualdade estrutural. |
| 2 | [Bandas e layout](02-bands-and-layout.md) | Bandas (header/footer/group/detail/subdetail), `BandKind`, e a semântica de paginação (Measure≡Render, split, CanShrink). |
| 3 | [Report items (`ReportElement`)](03-report-items.md) | A hierarquia de elementos e propriedades comuns; Label/TextBox/Line/Rectangle/Image/Tablix/Gauge/Chart/Subreport. |
| 4 | [Modelo de dados](04-data-model.md) | `DataSourceDefinition`, campos, campos calculados, relações, parâmetros e variáveis. |
| 5 | [Estilo e geometria](05-styling-and-geometry.md) | `Style`/`Font`/`Color`/`Border`, `Format`, e o sistema de unidades (`Unit`/`Rectangle`/`Thickness`). |
| 6 | [Linguagem de expressão](06-expression-language.md) | O dialeto OmniReport: `Fields.X`/`Parameters.X`/`Globals`, agregados e escopos, builtins, templates. |
| 7 | [Serialização](07-serialization.md) | A estrutura do `.repx` e do `.repjson`, o auto-wiring por convenção, e o contrato de round-trip. |
| 8 | [Interop com RDL](08-rdl-interop.md) | O mapeamento elemento→RDL, a conversão de dialeto, e o plano `<CustomProperties>` para round-trip lossless. |
