# OmniReport — Roadmap

**Baseline:** `main` @ `e7600bf` (PR #270) · **Versão publicada:** 0.1.1 · **Revisão:** 3ª — 14/10/2026
(a 2ª tinha baseline `f83ea38` + PR #215)

## Como este documento foi produzido

Esta revisão reuniu seis frentes, cada uma verificada contra o código atual e, quando possível, **executando** o
produto em vez de ler o código:

1. **Re-verificação** dos 43 itens da 2ª revisão e dos achados F01–F28 da auditoria técnica de 05/09/2026
   (`docs/auditoria-tecnica-2026-09-05.md`, `docs/correcoes-f01-f27-2026-09-08.md`,
   `docs/evolucao-pos-auditoria-2026-09-09.md`).
2. **Designer no navegador**: sample `Reporting.Samples.BlazorServer` rodando, com seleção, multisseleção,
   alinhamento, distribuição, camadas, setas, inserção, recortar/copiar/colar, desfazer/refazer, menus, paleta
   de comandos, tema, grade/snap/régua, diálogos (parâmetros, fonte de dados, configuração de página, impressão,
   editor de expressões), preview e exportação nos 9 formatos registrados.
3. **Arquitetura e organização**: grafo de dependências entre os 39 projetos, tamanho por arquivo, organização
   de pastas, solution e cobertura de teste por projeto.
4. **Fidelidade**: saídas reais dos samples (PDF inspecionado objeto a objeto, Markdown, CSV, preview em PNG).
5. **DX**: README, quickstarts, samples, CI, tooling e documentação.
6. **Paridade** com SSRS, Crystal, JasperReports, DevExpress XtraReports, Telerik, FastReport e Stimulsoft.

**Níveis de confiança:**

- 🧪 **Executado**: reproduzido rodando o produto (navegador, arquivo gerado, teste) nesta revisão.
- ✅ **Verificado**: confirmado por leitura direta do arquivo/linha citado nesta revisão.
- 🔍 **Relatado**: vem de revisão anterior ou da auditoria e não foi reconfirmado. **Confirme antes de agir.**

**Legenda de esforço:** P ≤1 dia · M 2–5 dias · G 1–3 semanas · GG >3 semanas

**Item concluído:** a linha na lista priorizada ganha **✔ FEITO (#PR)**, o detalhamento ganha uma seção
**FEITO.** com o que foi entregue e como foi verificado, e a entrega entra no *Histórico de concluídos*. A
numeração não muda — um item derivado de outro entra logo após ele, com sufixo (ex.: 3a).

> **O padrão de defeito continua o mesmo das revisões anteriores:** *o que a documentação, o comentário ou o
> teste afirma diverge do que o runtime faz*. Nesta revisão ele apareceu de novo em seis lugares — inclusive
> numa conclusão desta própria série (item 1). A regra que vale para todos os itens abaixo: **verifique o
> comportamento, não o identificador**.

---

## Resumo executivo

O núcleo está maduro para um pré-1.0: build `TreatWarningsAsErrors`, 1.950+ testes, golden files, Package
Validation contra o nuget.org, CI Linux cobrindo todos os projetos cross-platform, testes contra bancos reais,
auditoria de vulnerabilidades e os 27 achados da auditoria técnica corrigidos. O Designer funciona de verdade
nos fluxos principais — todos os comandos de edição testados no navegador fizeram o que prometem.

Os problemas que restam são de cinco tipos:

1. **Saída incorreta ou inchada sem aviso** — o PDF embute as fontes *inteiras* (1 página = 1,1 MB); `RowSpan`,
   borda de texto e cantos arredondados são aceitos pelo modelo e descartados na renderização.
2. **Arquitetura que fica cara depois do 1.0** — a abstração de exportação mora dentro do pacote PDF, então
   exportar CSV arrasta o SkiaSharp nativo; Designer, Viewer e Hosting referenciam todos os exporters.
3. **Acabamento do Designer** — paleta com 13 de ~40 comandos, menus incompletos, tema escuro que apaga o texto,
   ausência de layout responsivo e de semântica de acessibilidade.
4. **Organização do código** — arquivos de 1.100–1.700 linhas, Designer organizado por tipo e não por feature,
   solution plana, 135 arquivos gerados versionados.
5. **Features de mercado** ainda ausentes — senha/PDF-A, cache de relatório, document map no viewer, wizard,
   watermark.

---

## O que mudou desde a 2ª revisão

| Mudança | Detalhe |
|---|---|
| ✅ **Concluídos** | Itens 1–10, 12 (rede de paridade RDL), 13, 14 (parcial), 16, 18, 20, 21 (inherit), 30a, 30b; partes de 15, 17 e 19; achados F01–F27 (#260) e as seis recomendações pós-auditoria (#270). Lista completa no fim do documento. |
| ⚠️ **Reaberto** | **Item 23 (fontes no PDF).** A 2ª revisão o declarou *falso positivo* afirmando que o PDF "embute E faz subsetting". Embutir, sim; **subsetting, não** — ver item 1. |
| ➕ **Novos** | 23 itens novos, vindos do teste do Designer no navegador, da análise de arquitetura e da inspeção das saídas. |
| 🔁 **Refinados** | O antigo item 36 (RowSpan) deixou de ser "gap RDL de nicho": o campo já existe no modelo e nos serializadores e é **ignorado pelo renderer** — virou defeito (item 2). |

---

## Lista priorizada

### P0 — Saída incorreta ou enganosa (corrigir antes de qualquer feature)

| # | Item | Tipo | Esforço | Conf. | Origem |
|---|---|---|---|---|---|
| 1 | PDF embute as fontes **inteiras** — sem subsetting real (1 página = 1,1 MB) | Fidelidade/Perf | M | 🧪 | R2 #23 reaberto |
| 2 | `RowSpan` aceito, persistido e **ignorado** pelo renderer do Tablix — **✔ FEITO (#274)** | Fidelidade | M | ✅ | R2 #36 |
| 3 | `Style.Border` não desenhada em Label/TextBox/Image; grade do Tablix com cor fixa — **✔ FEITO (#275)** (grade: ver 3a) | Fidelidade | M | ✅ | R2 #21a |
| 3a | Grade do Tablix com cor e espessura fixas, sem relação com o estilo | Fidelidade | M | ✅ | item 3 |
| 4 | `CornerRadius` descartado em retângulo sem filhos — **✔ FEITO (#275)** | Fidelidade | P | ✅ | R2 #21b |
| 5 | Export RDL perde `ToggleItem` e `InitiallyHidden` (perda conhecida, registrada na rede de paridade) — **✔ FEITO (#276)** | Perda de dados | P | ✅ | R2 #12 |
| 6 | Designer: desfazer uma inserção deixa o elemento removido **selecionado e editável** — **✔ FEITO (#277)** | Bug | P | 🧪 | novo |
| 7 | Exportação no Designer: menu fixo divergente do registro, falha silenciosa, cliques perdidos — **✔ FEITO (#280)** | Bug | P | 🧪 | novo |
| 8 | Documentação afirma o que não é mais verdade (README, badges, evidências não versionadas) — **✔ FEITO (#281)** | Doc | P | ✅ | novo |

### P1 — Arquitetura e decisões antes do 1.0

| # | Item | Tipo | Esforço | Conf. | Origem |
|---|---|---|---|---|---|
| 9 | Abstração de exportação mora no pacote PDF → CSV/JSON/XML/Excel arrastam SkiaSharp — **✔ FEITO (#282)** | Arquitetura | M | ✅ | novo |
| 10 | Contrato de export: `ExportAsync` real, faixa de páginas e consumo de `PreparedReport` | API | M | ✅ | R2 #14 + #39 |
| 11 | Designer, Viewer e Hosting referenciam **todos** os exporters | Arquitetura | M | ✅ | novo |
| 12 | Nenhum `ILogger` em `src/`; falhas do Designer vão para `Console.Error` | Observabilidade | P | ✅ | novo |
| 13 | Monaco carregado do CDN em runtime — não funciona offline nem sob CSP | Segurança/DX | M | ✅ | novo |
| 14 | **Decisão:** postura de segurança do Roslyn (só fonte confiável × isolamento real) | Segurança | P/G | 🔍 | R2 #11 |
| 15 | **Decisão:** i18n — a UI já mistura português e inglês | Produto | P/G | 🧪 | R2 #30 |

### P2 — Designer: UI/UX (verificado no navegador)

| # | Item | Tipo | Esforço | Conf. | Origem |
|---|---|---|---|---|---|
| 16 | Paleta de comandos com 13 de ~40 comandos e campo de busca sem foco | UX | P | 🧪 | novo |
| 17 | Menus incompletos e enganosos ("Inserir" com 6 de ~19 tipos; "Validar" = preview; "Atalhos" = paleta) | UX | P | 🧪 | novo |
| 18 | Tema escuro escurece o papel — o texto preto do relatório some | UX/Fidelidade | P | 🧪 | novo |
| 19 | Designer não cria fontes JSON, XML, REST nem FileSystem (paridade dos 3 modos) | Paridade | M | 🧪 | novo |
| 20 | Sem layout responsivo: abaixo de ~1.280 px o painel de propriedades sai da tela | UX | M | 🧪 | novo |
| 21 | Acessibilidade: botões sem nome, toggles sem `aria-pressed`, modais sem `aria-modal`, 304 estilos inline | A11y | M | ✅ | novo |
| 22 | Defeitos visuais pequenos (placeholder `&#10;`, aba "Untitled.repx", rótulo "DATE") | UX | P | 🧪 | novo |
| 23 | Preview rasterizado a 96 DPI — borrado em tela HiDPI e com zoom | UX | P | 🧪 | novo |
| 24 | Editor de rich text (`TextRuns`) — ausente no Designer e no CodeFirst | Feature | M | ✅ | R2 #32 |
| 25 | Botão "Testar" do editor de expressões avalia com dados fictícios | UX | M | 🔍 | auditoria |

### P3 — Débito técnico, organização do código e DX

| # | Item | Tipo | Esforço | Conf. | Origem |
|---|---|---|---|---|---|
| 26 | Arquivos-deus: `ReportDesigner.razor.cs` (1.686), `designer.js` (1.611), `ReportPaginator.cs` (1.335), `ElementViewModel.cs` (1.173) | Arquitetura | G | ✅ | R2 #17 |
| 27 | `RdlImporter` (1.385) e `RdlWriter` (1.053) monolíticos | Arquitetura | M | ✅ | R2 #19 |
| 28 | Organização por tipo, não por feature (Designer, `Core/Elements`, raiz do `Layout`) | Organização | M | ✅ | novo |
| 29 | Solution plana: 39 projetos numa única pasta `/src/` | Organização | P | ✅ | novo |
| 30 | Arquivos gerados versionados: `out-novos/` (135 arquivos) e `Designer.Blazor/Docs/index-reference.html` | Higiene | P | ✅ | novo |
| 31 | Projetos sem teste direto: `Maps`, `Printing`, `Rendering`, `Printing.Android` | Teste | M | ✅ | novo |
| 32 | CI coleta cobertura sem limite e não verifica `dotnet format` | Processo | P | ✅ | novo |
| 33 | `Culture(CultureInfo.InvariantCulture)` lança `ArgumentException` | Bug de API | P | ✅ | R2 #21c |
| 34 | Paleta de gráficos com cores vizinhas quase iguais (laranja-700 × âmbar-700) | Fidelidade | P | 🧪 | novo |
| 35 | Espaçamento irregular de glifos no raster ("Qtd e") | Fidelidade | M | 🔍 | novo |
| 36 | Sem ADRs: decisões relevantes vivem só em comentários e neste roadmap | DX | P | ✅ | novo |

### P4 — Features essenciais de produto

| # | Item | Tipo | Esforço | Conf. | Origem |
|---|---|---|---|---|---|
| 37 | PDF: senha/criptografia e PDF/A (depende do item 1) | Feature | G | ✅ | R2 #22 |
| 38 | Streaming real das fontes de dados e exportação progressiva | Performance | G | ✅ | R2 #15 + F25 |
| 39 | Cache de relatório renderizado com chave (definição + parâmetros + dados + tenant) | Feature | M | ✅ | R2 #24 |
| 40 | Viewer: document map, busca e miniaturas | Feature | M | ✅ | R2 #25 |
| 41 | Wizard de criação de relatório no Designer | Feature | M | ✅ | R2 #26 |
| 42 | Oracle sem pacote de 1ª classe (a documentação promete) | Feature | P | ✅ | R2 #27 |
| 43 | `WebServiceDataSource` sem REST paginado | Feature | M | ✅ | R2 #28 |
| 44 | Sample de `Hosting.AspNetCore` inexistente | DX | P | ✅ | R2 #29 |
| 45 | Excel: autofilter e uma aba por grupo | Feature | P | ✅ | R2 #33 |

### P5 — Features recomendadas (diferenciação e alcance)

| # | Item | Tipo | Esforço | Conf. | Origem |
|---|---|---|---|---|---|
| 46 | Watermark + rotação de texto/elemento | Feature | M | ✅ | R2 #31 |
| 47 | Barcodes 2D além de QR (PDF417, DataMatrix, Aztec) | Feature | M | ✅ | R2 #34 |
| 48 | Query builder visual | Feature | G | ✅ | R2 #35 |
| 49 | `StaticMember`/`DynamicMember` no Tablix | RDL | G | 🔍 | R2 #36 |
| 50 | Site de documentação (DocFX) + `troubleshooting.md` | DX | M | ✅ | R2 #37 |
| 51 | Meta-pacote de onboarding + template `dotnet new` | DX | P | ✅ | R2 #38 |
| 52 | Agendamento e entrega de relatórios (módulo separado) | Feature | GG | ✅ | R2 #40 |
| 53 | RTL e `WritingMode` | i18n | G | ✅ | R2 #41 |
| 54 | Tagged PDF / acessibilidade | A11y | G | ✅ | R2 #42 |
| 55 | Exporters RTF / ODT / PPTX | Feature | G | ✅ | R2 #43 |
| 56 | Várias regiões de dados na raiz (N-DetailBands / List) | Feature | G | 🔍 | README |
| 57 | `ReportSections` do RDL 2016 (seções com `PageSetup` próprio) | RDL | G | 🔍 | README |
| 58 | Fontes e datasets compartilhados (`.rds`/`.rsd`) | RDL | M | 🔍 | README |

---

# Detalhamento

## P0 — Saída incorreta ou enganosa

### 1. PDF embute as fontes inteiras — sem subsetting real 🧪

**O problema.** Todo PDF carrega o programa TrueType **completo** de cada fonte usada. Um relatório de uma página
com Arial e Arial Bold sai com **1,1 MB**, dos quais ~1,1 MB são as duas fontes.

**Evidência (inspeção objeto a objeto de `samples/Reporting.Samples.CodeFirst/out/01-vendas-por-cliente.pdf`):**

| Objeto | `/Length1` (fonte descomprimida) | Arquivo de origem | Tamanho do arquivo |
|---|--:|---|--:|
| `15 0 obj` (`/FontFile2`) | 1.045.720 | `C:\Windows\Fonts\arial.ttf` | 1.045.720 |
| `11 0 obj` (`/FontFile2`) | 989.780 | `C:\Windows\Fonts\arialbd.ttf` | 989.780 |

O tamanho descomprimido é **idêntico byte a byte** ao arquivo da fonte. O nome da fonte, porém, traz o prefixo
de subset (`AAAAAA+Arial-BoldMT`, `BAAAAA+ArialMT`) — por isso a 2ª revisão concluiu, errado, que havia
subsetting. O prefixo é só uma etiqueta; o programa embutido é o arquivo inteiro.

`tests/Reporting.Output.Pdf.Tests/FontEmbeddingTests.cs` trava exatamente a garantia errada: confere que existe
`/FontFile2`, que o nome casa com `^[A-Z]{6}\+.+` e que o texto é extraível. **Nenhuma asserção mede o tamanho do
programa embutido.**

**Por que importa.** Holerites, DANFEs e faturas são gerados aos milhares e enviados por e-mail. Um arquivo de
1,1 MB onde caberiam algumas dezenas de KB multiplica armazenamento, banda e tempo de envio. E pesa sobre o
PDF/A (item 37), que exige fontes embutidas em todo arquivo arquivado.

**Ação.**
1. Confirmar a causa. Hipótese a verificar: o Skia só subseta quando o subsetter do HarfBuzz está habilitado na
   compilação do `libSkiaSharp`; o binário distribuído pode não tê-lo. Confirmar no Linux também (outro nativo).
2. Se o nativo não subsetar: subsetar antes de embutir (o projeto já depende do HarfBuzz via `SkiaSharp.HarfBuzz`)
   ou pós-processar o PDF com biblioteca dedicada.
3. Corrigir `FontEmbeddingTests` para medir o que importa: `/Length1` de cada `FontFile2` menor que uma fração do
   arquivo de origem, e um teto de tamanho para o PDF de uma página.

**Pronto quando:** o PDF de uma página do sample 01 fica abaixo de ~100 KB, o texto continua extraível e o teste
falha se o embutimento voltar a ser completo.

---

### 2. `RowSpan` aceito, persistido e ignorado pelo renderer ✅ — ✔ FEITO

**O problema.** `TablixCell` declara `ColumnSpan` e `RowSpan` (`src/Reporting.Core/Elements/TablixCell.cs:7-11`).
Os dois são lidos e gravados pelos serializadores (`RepxWriter.cs:770`, `RepxReader.cs:531`,
`RepJsonWriter.cs:609`, `RepJsonReader.cs:555`). Mas o `TablixRenderer` usa `ColumnSpan` e **nunca lê
`RowSpan`** — zero ocorrências em `src/Reporting.Layout`. O importador RDL também não o preenche
(`RdlImporter.Tablix.cs:336`: "RowSpan is implicit").

Nem o CodeFirst nem o Designer expõem a propriedade — viola a regra dos três modos de autoria.

**Por que importa.** Um `.repx`/`.repjson` com célula mesclada verticalmente carrega, salva e recarrega o valor —
e renderiza como se ele não existisse. É o pior tipo de perda: aceita sem erro e descartada na saída.

**Ação.** Implementar no `TablixRenderer` (a célula ocupa as linhas seguintes; as células cobertas não são
emitidas), expor nos três modos e cobrir com golden. Se a decisão for não suportar agora, **rejeitar** `RowSpan > 1`
na validação da definição em vez de aceitar em silêncio. Os casos de `StaticMember`/`DynamicMember` ficam no
item 49.

**Pronto quando:** um Tablix com `RowSpan = 2` gera uma célula com o dobro da altura nos golden files, nos 3 modos.

**FEITO (#274) — pela segunda alternativa da Ação, e por um motivo que a revisão não tinha visto.** Implementar o
span exigiria primeiro algo onde ele atuar: o Tablix plano tem exatamente **duas linhas-modelo** (0 = cabeçalho,
1 = detalhe repetido por registro) e a matriz lê só o canto e a primeira célula do corpo. Mesclar verticalmente
só faz sentido com cabeçalhos de várias linhas estáticas — que não existem no modelo e são o item 49. O mesmo
levantamento achou uma segunda perda silenciosa da mesma origem: **células além da linha 1** numa tabela plana
também nunca eram desenhadas.

`RenderedReport.Diagnostics` passa a trazer **`ORL025`** nos dois casos (`Tablix[nome].RowSpan` e
`Tablix[nome].TemplateRows`), percorrendo a definição e os subrelatórios inline e resolvidos
(`ReportPaginator.Diagnostics.cs`). O Designer já exibe os diagnósticos no preview. Verificado por
`TablixRowDiagnosticsTests` (4 casos, incluindo o negativo — tabela válida não gera ruído) e por sabotagem: sem a
inspeção, os 3 casos positivos falham. Quando o item 49 entrar, o `RowSpan` ganha onde atuar e o diagnóstico sai.

---

### 3. `Style.Border` não desenhada em Label/TextBox/Image ✅ — ✔ FEITO

**O problema.** `ResolveBorderPen` (`src/Reporting.Layout/Internal/BandRenderer.cs:726`) só é chamado para
`RectangleElement` (linha 326) e `EllipseElement` (linha 358). A passagem genérica de elemento emite o fundo, mas
nunca um retângulo com traço. Uma borda em Label, TextBox ou Image **não vira primitiva alguma**. A grade do
Tablix tem cor fixa (`TablixRenderer.cs:24`, `#CBD5E1`) e ignora o estilo.

Continua fixado como caracterização em `tests/Reporting.Golden.Tests/RenderGapCharacterizationTests.cs` — ou seja,
o teste existe **para garantir que o defeito continua lá** até ser corrigido.

**Por que importa.** TextBox com borda é o caso mais banal de relatório estilo SSRS. O Designer deixa configurar
a borda no painel de propriedades — e a saída a ignora.

**Ação.** Emitir o retângulo de borda na passagem genérica (as quatro faces, com estilo/espessura/cor por face)
e tornar a grade do Tablix dirigida por estilo.

**Pronto quando:** o golden `Estilos` mostra a primitiva de borda e a caracterização correspondente falha (e é
apagada).

**FEITO (#275).** Todo elemento sem contorno próprio (tudo menos Rectangle, Ellipse e Line) emite a borda **sobre o
conteúdo**, no tamanho final — o do TextBox depois de crescer/encolher e o do Tablix depois de crescer, inclusive em
cada fatia ou ladrilho de colunas quando ele pagina. Quatro lados
iguais viram um retângulo com traço; lados diferentes, uma linha por lado visível com estilo, espessura e cor
próprios. Duas correções vieram junto, da mesma raiz: o Rectangle usava **só o lado de cima** (uma borda só
inferior sumia) e a borda vinha do estilo cru, ignorando estilo nomeado e formatação condicional — agora vem do
estilo efetivo. O golden `Estilos` passou a registrar a primitiva de borda (diff lido antes de aceitar), e o teste
de caracterização virou `ElementBorderAndRadiusTests`, que trava o comportamento correto. Sabotagem: sem a emissão
genérica, 3 testes falham.

A metade "grade do Tablix dirigida por estilo" **não** entrou: a grade é desenhada pelo renderer, não pelas
células, e torná-la configurável muda o modelo (propriedade nova nos serializadores, no CodeFirst e no Designer).
Virou o item 3a.

---

### 3a. Grade do Tablix com cor e espessura fixas ✅

`TablixRenderer.cs:24` desenha a grade em `#CBD5E1` a 0,5 pt em toda tabela, sem relação com o estilo. Um relatório
que precisa de grade mais forte, mais clara ou de nenhuma grade não tem como pedir. **Ação:** propriedade de grade
no `TablixElement` (cor, espessura, ou "nenhuma"), nos três modos de autoria, com o valor atual como padrão para
não mudar relatórios existentes. Com o item 3 entregue, uma alternativa é deixar a grade de lado e usar a borda de
cada célula, como o SSRS faz.

---

### 4. `CornerRadius` descartado em retângulo sem filhos ✅ — ✔ FEITO

**O problema.** `DrawRectanglePrimitive` não tem campo de raio. O valor sobrevive só como `ClipCornerRadius`
(`src/Reporting.Layout/Primitives/LayoutPrimitive.cs:37-39`), aplicado aos **filhos** do retângulo. Um retângulo
usado como forma arredondada não tem filhos: o SVG exportado é um `<rect>` sem `rx`. Também fixado em
`RenderGapCharacterizationTests`.

**Ação.** Campo de raio em `DrawRectanglePrimitive`, honrado em Skia, GDI, PDF, SVG e no overlay HTML.

**Pronto quando:** o golden `Formas` mostra `rx` e a caracterização falha.

**FEITO (#275).** `DrawRectanglePrimitive.CornerRadius` (padrão zero) é preenchido pelo Rectangle — inclusive no
fundo de `BackColor`, que antes ficava quadrado atrás de um retângulo arredondado. Os exportadores Skia (PDF, SVG,
PNG/TIFF) desenham `roundRect`, com o raio limitado a metade do menor lado; o player genérico (GDI, ESC/POS e
hosts) traça o mesmo contorno via `DrawPath`, **sem ampliar `IRenderingContext`** — uma interface pública que
hosts implementam. Os cantos são curvas cúbicas: o `Arc` dos backends é um arco inscrito independente (o Skia abre
um contorno novo; o GDI não move o ponto corrente), e misturá-lo com `LineTo` fragmentava o preenchimento e traçava
segmentos internos — apontado na revisão e travado por testes de pixel no GDI e no Skia. JSON e XML exportam `cornerRadius`. O golden `Formas` mudou de `<rect>` para um `path`
arredondado (o backend SVG do Skia emite `roundRect` assim, não como `rx`). Verificado também em pixel no GDI: com
raio, o canto fica fora da forma. Sabotagem: sem o caminho arredondado do player, o teste do GDI falha.

---

### 5. Export RDL perde `ToggleItem` e `InitiallyHidden` ✅ — ✔ FEITO

**O problema.** `RepxWriter` e `RepJsonWriter` gravam `ToggleItemId` (`RepxWriter.cs:456`,
`RepJsonWriter.cs:395`); `RdlWriter.cs` tem **zero** ocorrências de `ToggleItem`. Importar um `.rdl` com
drill-down e salvá-lo como `.rdl` perde o vínculo e o estado inicial oculto — o mesmo defeito que o PR #217
corrigiu no sentido import.

A causa estrutural **já tem rede**: o PR #219 criou `RdlReflectionParityTests`, que percorre por reflexão as
propriedades de cada `ReportElement` e falha se alguma se perder no round-trip RDL sem estar declarada como perda.
É ela que registra esta lacuna explicitamente (`RdlReflectionParityTests.cs:57-58`, lista `CommonLossy`: *"the
WRITER still does not emit `<ToggleItem>`"*). Ou seja: a perda é conhecida e travada — falta fechá-la.

Limite da rede, a considerar: ela percorre as propriedades do elemento; valores dentro de registros aninhados
(como `TablixCell.RowSpan`, item 2) só são cobertos se o populador os alcançar.

**Ação.** Emitir `<Visibility><Hidden>` e `<ToggleItem>` no `RdlWriter` e remover `ToggleItemId`/`InitiallyHidden`
de `CommonLossy` — a rede passa a garantir que a perda não volte.

**Pronto quando:** o round-trip `.rdl` de drill-down preserva o vínculo e as duas entradas saem da lista de perdas.

**FEITO (#276).** `RdlWriter.WriteVisibility` emite `<Hidden>` e `<ToggleItem>` como inverso exato do importador.
Tirar as duas entradas de `CommonLossy` fez a rede de paridade apontar um terceiro caso que a revisão não tinha
visto: sob um `ToggleItem`, o `<Hidden>` do RDL significa "começa recolhido" — então um `Visible=false`
**permanente** num elemento com toggle voltava como visível. Não há lugar no RDL para os dois estados ao mesmo
tempo; o `Hidden` passou a carregar só o estado inicial e o oculto permanente viaja como `omni:Visible` em
`CustomProperties`, a estratégia já usada para os outros campos sem equivalente. Validado contra o XSD oficial
RDL 2016 (`RdlXsdValidationTests`) e por `RdlToggleRoundTripTests`. A revisão do PR apontou mais dois casos, também
corrigidos: com toggle, uma visibilidade **condicional** (`<Hidden>=expressão`) voltou a ser projetada — o estado
inicial, quando coexiste com ela, vai em `omni:InitiallyHidden` —, e um `ToggleItemId` que aponta para o
**bookmark** da TextBox (como o CodeFirst produz) é reescrito para o nome exportado dela, já que o RDL resolve
`<ToggleItem>` por nome; referências que já são nomes, como as importadas, ficam intactas. Sabotagem: revertendo o writer, 12
testes falham — inclusive a própria rede, que agora impede a perda de voltar.

---

### 6. Designer: desfazer inserção deixa um elemento "fantasma" 🧪 — ✔ FEITO

**O problema.** Reproduzido no navegador: inserir um Código de barras pela toolbar → **Desfazer**. O canvas volta
a ter 18 elementos, mas a barra de status continua com "Seleção: 1 elemento" e o painel de propriedades segue
exibindo o código de barras removido (X 15, Y 0, "1234567890", Code128) — editável, e agora rotulado como
pertencente à banda Detail.

**Por que importa.** Editar o fantasma altera um view model desanexado: a mudança some, ou entra no histórico
apontando para algo que não está no documento. É a mesma classe de defeito que o F23 corrigiu para outros
comandos.

**Ação.** Ao desfazer/refazer, remover da seleção os elementos que deixaram de pertencer ao documento (e
reselecionar os que voltaram). Teste bUnit: inserir → desfazer → seleção vazia e painel sem elemento.

**Pronto quando:** desfazer qualquer inserção, colagem ou duplicação deixa a seleção consistente com o canvas.

**FEITO (#277).** Em vez de corrigir comando a comando, o `DesignerState` ouve o histórico da aba ativa e, após
cada mudança, retira da seleção o que saiu do documento — inserção ou colagem desfeita, exclusão refeita, filhos
de um container removido —, mantendo selecionado o que continua lá. Assim um comando novo não precisa lembrar de
limpar a seleção. `SelectionHistoryTests` cobre os quatro casos; sem a assinatura do histórico, os quatro falham.

---

### 7. Exportação no Designer: menu fixo, falha silenciosa, cliques perdidos 🧪 — ✔ FEITO

**O problema — três defeitos no mesmo fluxo:**

1. **Menu fixo.** `Components/TopBar.razor:41-62` lista oito formatos *hardcoded*. A barra do preview, ao
   contrário, vem do `IExporterRegistry` e mostra nove (inclui XML). O comentário em
   `ReportDesigner.razor.cs:1578-1581` afirma que todo exporter registrado "becomes automatically available" —
   verdade na paleta e no preview, falso no menu Arquivo.
2. **Falha silenciosa.** Se o host não registrou um formato, o item do menu não faz nada
   (`ReportDesigner.razor.cs:1587-1592`: `desc is null` → `break`). Se a exportação lança, a exceção vai para
   `Console.Error` (linha 1229) e o usuário não vê nada.
3. **Cliques perdidos.** Clicar nos nove botões do preview em sequência (2,5 s de intervalo) gerou **sete**
   downloads: HTML e SVG sumiram sem erro. Isoladamente, os dois funcionam. Não há estado de "ocupado" nem
   desabilitação durante a exportação.

**Ação.** Gerar o menu a partir do registro; mostrar erro de exportação na UI (toast/diálogo); desabilitar os
botões e indicar progresso enquanto uma exportação está em curso.

**Pronto quando:** menu, paleta e preview listam exatamente os mesmos formatos; uma falha aparece para o usuário;
cliques durante uma exportação são enfileirados ou bloqueados de forma visível.

**FEITO (#280).** O menu Arquivo é gerado do `IExporterRegistry`, a mesma lista da barra do preview e da paleta. Uma
falha — do exportador, de formato não registrado ou do próprio preview, que também só ia para `Console.Error` —
aparece num alerta com o motivo (`role="alert"`, cores por token). A exportação passou a `ExportAsync` e, enquanto
roda, exportar e imprimir ficam desabilitados com `aria-busy`: um clique durante o processo é recusado à vista, não
perdido. Achado de passagem: exportar pelo menu quando o preview não gerava documento era um no-op silencioso — agora
o alerta do preview explica. Verificado com bUnit (`ExportFlowTests`, os 3 falham sem a correção) e no navegador: menu
com os 9 formatos + RDL; durante o PDF, 7/7 botões desabilitados; depois, download feito e botões reabilitados. O log
estruturado continua pendente no item 12.

---

### 8. Documentação afirma o que não é mais verdade ✅

| Onde | Afirma | Realidade |
|---|---|---|
| `README.md:34-37` | `GroupBand.FilterExpression`/`SortExpressions`/`KeepTogether` emitem `ORL024` | Implementados no #270; `ORL024` não existe mais em `src/` |
| `README.md:3-9` | Badges "CI passing", "1000+ tests", "core coverage ≥80%" | São imagens estáticas do shields.io — não refletem nenhum status real; a suíte tem 1.950+ testes |
| `README.md:400` | "38 bibliotecas", "23 projetos de teste" | 39 e 25 |
| `docs/auditoria-*.md`, `docs/correcoes-*.md`, `docs/evolucao-*.md` | Evidências em `artifacts/...` | `artifacts/` está no `.gitignore` (4,3 GB só nesta máquina) — quem clona não consegue conferir a evidência |
| `README.md:426` | Seção "Roadmap (diferido)" | Segunda lista de roadmap, divergente desta |

**Ação.** Corrigir os trechos; trocar os badges estáticos por badges reais do GitHub Actions (status do
`ci.yml`) e da cobertura publicada, ou removê-los; nas auditorias, distinguir evidência versionada
(`docs/evidencias/`) de evidência local, ou versionar o mínimo necessário; apontar a seção de roadmap do README
para este arquivo.

**Pronto quando:** nenhum número ou status do README é estático, e toda evidência citada como prova é acessível
a quem clona o repositório.

**FEITO (#281).** Badge de CI trocado pelo status real do GitHub Actions; os badges fixos de testes e cobertura
saíram (não havia fonte que os mantivesse verdadeiros). A seção da auditoria no README e o `CHANGELOG` deixaram de
citar o `ORL024` como atual e passaram a apontar o `ORL025`; contagens corrigidas no README e no guia do
desenvolvedor. As três auditorias ganharam um aviso de que `artifacts/` é local e não acompanha o repositório (a
alternativa, versionar gigabytes de evidência, não se paga). A lista "Roadmap (diferido)" do README virou um
ponteiro para este arquivo, e seus itens ainda pendentes entraram aqui como 56–58; o canvas "placeholder" que ela
citava já desenha uma amostra representativa (a norma do SSRS) e os mapas detalhados já são registráveis pelo host,
então não viraram itens. Achado de passagem: o `[Unreleased]` do `CHANGELOG` parava no #234 — as entregas do #229 ao
#280 foram registradas, e sem isso o próximo release publicaria notas incompletas.

---

## P1 — Arquitetura e decisões antes do 1.0

### 9. A abstração de exportação mora no pacote PDF ✅ — ✔ FEITO

**O problema.** `IReportExporter` está em `src/Reporting.Output.Pdf/IReportExporter.cs`, no namespace
`Reporting.Output.Pdf`; `ExportWarning` também (`src/Reporting.Output.Pdf/ExportWarning.cs`). Como o pacote PDF
depende de `Rendering.Skia`, **todo exporter** herda o SkiaSharp e seus binários nativos:

```
Output.Csv / Output.Json / Output.Xml / Output.Markdown / Output.Excel / Output.Html / Output.Docx
    └─► Output.Pdf ─► Rendering.Skia ─► SkiaSharp + NativeAssets
```

Quem só quer exportar CSV num container Linux mínimo carrega o Skia nativo — e o item 1 do roadmap anterior
(Linux sem fonte) mostrou o quanto esse nativo é sensível ao ambiente.

**Por que agora.** Mover um tipo público de pacote e de namespace é breaking change. Antes do 1.0 custa uma
entrada no CHANGELOG e uma supressão no Package Validation; depois, custa uma major.

**Ação.** Criar `Reporting.Output` (abstrações: `IReportExporter`, `ExportWarning`, options base, faixa de
páginas — ver item 10) dependendo só de `Layout`. Todos os exporters passam a depender dele; `Output.Pdf` vira
um exporter como os outros. Opcional: `[TypeForwardedTo]` por uma versão para suavizar a migração.

**Pronto quando:** `dotnet list package --include-transitive` do `Output.Csv` não mostra SkiaSharp.

**FEITO (#282).** Novo pacote `Reporting.Output` (namespace `Reporting.Output`), dependendo só de Core e Layout, com
`IReportExporter`, `ExportWarning`, `ExportWarningHandler` e as extensões. Os nove exportadores sem Skia passaram a
referenciar só o contrato, e o critério foi conferido: o `Output.Csv` não traz mais SkiaSharp. Não usei
`[TypeForwardedTo]`: o encaminhamento só preserva o tipo se o namespace continuar `Reporting.Output.Pdf`, o que
deixaria o contrato com o nome errado para sempre — pré-1.0, a quebra explícita custa menos. Ela está nas supressões
do Package Validation (todas decorrentes da mudança do tipo) e no CHANGELOG, com a migração. O pacote nasceu com a
trava de documentação, que pegou dois métodos sem `<summary>`; e o guia do desenvolvedor, que dizia que o contrato
ficava em `Reporting.Rendering`, foi corrigido.

---

### 10. Contrato de exportação: `ExportAsync` real, faixa de páginas e `PreparedReport` ✅

**O problema.** O item 14 da 2ª revisão foi parcialmente entregue: `IReportExporter.ExportAsync` existe
(`IReportExporter.cs:37`) com implementação padrão delegando à síncrona — mas **só 3 exporters** a implementam de
verdade (`SkiaPdfExporter`, `PngImageExporter`, `TiffImageExporter`). Os demais bloqueiam a thread no meio de um
pipeline assíncrono, e um cliente que desiste do download não libera o servidor.

Duas lacunas do mesmo contrato:

- **Faixa de páginas** (antigo item 39): exportar "páginas 3–7" exige montar um `RenderedReport` parcial à mão.
  `PrintOptions` tem faixa; os exporters, não.
- **Documento preparado** (`PreparedReport`, entregue no #270): os exporters recebem `RenderedReport` completo,
  então o Designer materializa todas as páginas para exportar (`ReportDesigner.razor.cs:1288-1294`,
  `MaterializePreview`), desfazendo o ganho de memória do preview sob demanda.

**Ação.** Junto com o item 9: `ExportAsync` nativo em todos os exporters; `PageRange` nas options base; sobrecarga
que consome páginas sob demanda (`PreparedReport`/`IAsyncEnumerable<RenderedPage>`) para os formatos que
permitem escrita progressiva (PDF, SVG por página, CSV, JSON).

**Pronto quando:** cancelar um export em curso libera a thread; exportar uma faixa não materializa o documento
inteiro.

---

### 11. Designer, Viewer e Hosting referenciam todos os exporters ✅

**O problema.** `Reporting.Designer.Blazor.csproj` referencia `CodeFirst`, `Printing`, `Serialization` e **nove**
pacotes `Output.*`; `Viewer.Blazor` referencia sete `Output.*` mais Skia e Printing; `Hosting.AspNetCore`
referencia Docx, Excel, Pdf, Skia e Printing. Um host que só quer o Designer com export PDF instala ClosedXML,
OpenXml e todo o resto.

O mecanismo de extensão certo já existe: `IExporterRegistry` descobre exporters por DI, e
`AddDefaultExporters()` registra o conjunto padrão. O acoplamento de projeto é que não acompanha.

**Ação.** O Designer e o Viewer dependem só da abstração (item 9); os exporters concretos entram pelo host ou por
um pacote de conveniência (`…Designer.Blazor.AllExporters`, ou o meta-pacote do item 51). Reavaliar por que o
Designer referencia `CodeFirst`.

**Pronto quando:** um host com Designer + PDF não restaura ClosedXML nem OpenXml.

---

### 12. Nenhum `ILogger` em `src/` ✅

**O problema.** Zero ocorrências de `ILogger` em `src/`. O Designer escreve falhas em `Console.Error` em seis
pontos: `ReportDesigner.razor.cs:936, 949, 995, 1229, 1284` e `Components/MonacoEditor.razor.cs:67`. Num host
ASP.NET Core essas mensagens não passam pelo pipeline de logging, não têm nível nem correlação e não chegam ao
Seq/Application Insights/OpenTelemetry do host.

A telemetria entregue no #270 (Activity/Meter `OmniReport`) cobre duração e resultado das fases, mas **não
substitui** log de erro com mensagem e exceção.

**Ação.** `ILogger<T>` injetado (com `NullLogger` como padrão), message templates estruturados, sem interpolação,
sem dados do relatório. Para o Designer, o mesmo erro vai ao log **e** à UI (item 7).

---

### 13. Monaco carregado do CDN em runtime ✅

**O problema.** `src/Reporting.Designer.Blazor/wwwroot/js/monaco-host.js:14` carrega o editor de
`https://cdn.jsdelivr.net/npm/monaco-editor@…`. Consequências: o editor de expressões não funciona offline nem em
rede corporativa sem acesso ao CDN; um host com Content-Security-Policy restritiva precisa liberar um domínio de
terceiro; e o código executado no navegador do usuário vem de fora do pacote publicado. A auditoria já registrou
erro de carregamento do worker de localização pelo CDN.

**Ação.** Empacotar o Monaco no RCL (ou torná-lo configurável, com o pacote local como padrão), e documentar a CSP
necessária. Fallback para `<textarea>` se o editor não carregar.

**Pronto quando:** o editor de expressões abre com a rede bloqueada.

---

### 14. Decisão: postura de segurança do Roslyn 🔍

O pacote `Expressions.Roslyn` executa C# arbitrário do relatório. O F17 resolveu a retenção de memória
(`AssemblyLoadContext` coletável), mas **não** há restrição do que o código pode fazer: não há allowlist de
namespaces nem timeout (zero ocorrências em `src/Reporting.Expressions.Roslyn`). O pacote é opt-in e o README
avisa "use apenas com fontes confiáveis".

**Decisão pendente:** (a) manter "só fontes confiáveis" — barato e honesto — registrando a decisão em ADR e com
um aviso em runtime quando o resolver for habilitado; ou (b) isolar de verdade (allowlist de assemblies, timeout,
processo separado) — caro. Em qualquer caso, testar casos hostis (laço infinito, `File`/`Process`, estouro de
pilha) para documentar o comportamento.

---

### 15. Decisão: i18n — a UI já mistura idiomas 🧪

O roadmap anterior pedia uma decisão de escopo ("há mercado fora do Brasil?"). Enquanto ela não vem, a UI
degradou para uma **mistura**: no painel de propriedades convivem "Visível se", "Aparência", "Cor do texto" e
"Layout", "Width", "Height", "Behavior", "Locked", "Bookmark", "Toggle by"; o diálogo de parâmetros mostra
"Data inicial" com o subtítulo "DATE"; a aba de documento novo chama-se "Untitled.repx" enquanto o título diz
"Sem título". Zero arquivos `.resx`.

**Ação.** Decidir. Se a resposta for "só pt-BR": uniformizar tudo em português e registrar em ADR. Se for
"internacional": `IStringLocalizer` + `.resx`, começando pelas strings já em inglês. Os itens 15 e 53 (RTL)
dependem dessa decisão.

---

## P2 — Designer: UI/UX

Verificação feita no navegador com o sample `Reporting.Samples.BlazorServer`. **Funcionaram como prometido:**
seleção e multisseleção (Shift+clique), alinhar (6 direções), distribuir, mesma largura/altura/tamanho, mover com
setas, inserir pela toolbar (5 tipos), recortar/copiar/colar/excluir, desfazer/refazer de todas essas operações,
grade/snap/régua, tema, diálogos de parâmetros, fonte de dados (8 abas), configuração de página e impressão,
editor de expressões com Monaco, preview e exportação nos 9 formatos.

### 16. Paleta de comandos incompleta e sem foco 🧪

A paleta lista **13** comandos fixos ("13 resultados"); existem ~40 comandos tratados no despacho
(`ReportDesigner.razor.cs`, ~60 `case`). Ficam de fora alinhamento, distribuição, camadas, exportações, zoom,
grade, snap, tema, configuração de página. Além disso, o campo de busca usa o atributo `autofocus`
(`Components/CommandPalette.razor:9`), que o Blazor não aplica a elementos inseridos dinamicamente: ao abrir com
Ctrl+K, o texto digitado **não vai para a busca**.

**Ação.** Gerar a lista a partir de um registro único de comandos (o mesmo que alimenta menus e atalhos);
`ElementReference.FocusAsync()` em `OnAfterRenderAsync`.

---

### 17. Menus incompletos e enganosos 🧪

- **Inserir** oferece 6 tipos (TextBox, Label, Linha, Retângulo, Imagem, Código de barras); a Toolbox oferece
  ~19 (Elipse, QR, Tabela, Chart, Gauge, Sparkline, Map, DataBar, Indicator, Subreport, Code, Nº Página…).
- **Relatório › Validar expressões** e o botão "Validar relatório" só abrem o preview
  (`ReportDesigner.razor.cs:1620`: `case "report.validate": await OnPreview();`).
- **Ajuda › Atalhos de teclado** abre a paleta de comandos (linha 1633) — embora exista
  `src/Reporting.Designer.Blazor/Docs/KEYBOARD-SHORTCUTS.md`.
- Não há menu **Formatar/Organizar**: alinhamento e camadas só existem na toolbar.
- Há dois `case` duplicados para inserir o mesmo tipo (`insert.textbox`/`insert.text`, `insert.rectangle`/
  `insert.rect`, `insert.image`/`insert.img`).

**Ação.** Menus gerados do mesmo registro de comandos do item 16; validação real (compilar todas as expressões
e listar erros com localização, sem executar); diálogo de atalhos de verdade.

---

### 18. Tema escuro escurece o papel 🧪

Com o tema escuro, a área da página fica escura, mas os elementos mantêm a cor do relatório (preto por padrão).
Os cabeçalhos "Produto", "Qtde" e "Preço Unitário" do sample 01 ficam quase invisíveis. Além de ilegível, deixa
de ser WYSIWYG: o papel impresso é branco.

**Ação.** O tema escuro deve escurecer o *chrome* do Designer, não o papel — a página continua com o fundo
definido no relatório (branco por padrão), como fazem SSRS Report Builder, Crystal e o Word.

---

### 19. Designer não cria fontes JSON, XML, REST nem FileSystem 🧪

O editor de fonte de dados oferece cinco provedores: In-memory, SQLite, PostgreSQL, SQL Server e MySQL. Os
conectores `Json`, `Xml`, `WebService` e `FileSystem` existem, são documentados e estão nos samples — mas só podem
ser autorados por código. Viola a regra de paridade dos três modos de autoria.

**Ação.** Provedores no editor com os campos de cada conector (URL/arquivo, caminho de registros, headers por
segredo nomeado, paginação quando o item 43 entrar), reaproveitando as abas de Campos e Preview.

---

### 20. Sem layout responsivo 🧪

Num painel de ~630 px úteis (pane de 800 px com a navegação do sample), o painel de propriedades sai da tela e
a toolbar é cortada, sem rolagem nem colapso. Os painéis laterais não são recolhíveis.

**Ação.** Painéis laterais recolhíveis, toolbar com overflow ("…"), e um aviso explícito de largura mínima para
autoria abaixo de ~1.024 px (a visualização pode continuar). Validar em 1.024 / 1.280 / 1.440.

---

### 21. Acessibilidade ✅

Medido nos `.razor` do Designer: **25** `aria-label` contra 171 `title`; 2 `role="dialog"` e **0** `aria-modal`
para mais de dez diálogos; **0** `aria-pressed` — os toggles Grade/Snap/Régua indicam estado só por classe CSS;
2 botões visíveis sem nome acessível (zoom − / + do preview); **304** estilos inline em 28 componentes, contra a
convenção do próprio projeto (tokens + CSS isolado).

**Ação.** Nome acessível em todo botão de ícone; `aria-pressed` nos toggles; diálogos com `role="dialog"`,
`aria-modal`, foco preso e retorno de foco; mover estilos inline para CSS isolado com tokens. Verificar
navegação só por teclado e contraste (WCAG 2.2 AA) — não verificados nesta revisão.

---

### 22. Defeitos visuais pequenos 🧪

- Placeholder dos "Valores permitidos" exibe `1|Ativo&#10;0|Inativo` literal — entidade codificada duas vezes
  (`Components/ParametersList.razor:57`).
- A aba do documento continua "Untitled.repx" após carregar "Vendas por Cliente" (`ViewModels/DesignerState.cs:17`).
- Subtítulo "DATE" (inglês) sob os rótulos de parâmetro em português.
- O campo "Valores permitidos" aparece também para parâmetros do tipo Date, onde não faz sentido.

---

### 23. Preview rasterizado a 96 DPI 🧪

O preview é um PNG de 794 × 1.123 px (A4 a 96 DPI), independente de `devicePixelRatio` e do zoom. Em tela HiDPI
ou com zoom > 100 % o texto fica borrado. **Ação:** rasterizar em `96 × devicePixelRatio × zoom` (respeitando o
orçamento de pixels do F03) ou servir a página como SVG.

---

### 24. Editor de rich text (`TextRuns`) ✅

O modelo suporta runs com formatação mista e os serializadores os preservam, mas nem o Designer nem o CodeFirst
(zero ocorrências de `TextRuns` em `src/Reporting.CodeFirst`) permitem criá-los. A auditoria também registrou que
estilos e ações por run não são preservados na renderização concatenada — confirmar antes de expor na UI.

---

### 25. "Testar" do editor de expressões usa dados fictícios 🔍

Relatado pela auditoria (`ExpressionEditorDialog.razor`): a avaliação é real, mas os valores são de amostra
(`Number = 42`, `Money = 1234,56`, `Date = Now`). **Ação:** avaliar contra a primeira linha real do preview da
fonte quando disponível, mantendo a amostra como fallback e indicando qual foi usada.

---

## P3 — Débito técnico, organização do código e DX

### 26. Arquivos-deus ✅

| Arquivo | Linhas | Responsabilidades misturadas |
|---|--:|---|
| `Designer.Blazor/ReportDesigner.razor.cs` | 1.686 | despacho de ~60 comandos, preview, export, impressão, abrir/salvar, clipboard, teclado |
| `Designer.Blazor/wwwroot/js/designer.js` | 1.611 | drag, resize, guias, régua, seleção por área, atalhos |
| `Layout/ReportPaginator.cs` | 1.335 | já dividido em 11 parciais, e o principal ainda tem 1.335 |
| `Designer.Blazor/ViewModels/ElementViewModel.cs` | 1.173 | estado de UI + mapeamento de ~18 tipos de elemento |

**Ação.** No Designer: extrair um **registro de comandos** (resolve os itens 16 e 17 junto), e serviços de
preview/export/arquivo; dividir `designer.js` em módulos ES por interação. No `ElementViewModel`: continuar a
divisão por faceta já iniciada (`ViewModels/Facets/`). Sem big-bang — a suíte do Designer (357 testes) cobre.

---

### 27. `RdlImporter`/`RdlWriter` monolíticos ✅

1.385 e 1.053 linhas, misturando parsing genérico com regras por tipo de elemento. Já existe
`RdlImporter.Tablix.cs` e `RdlWriter.Tablix.cs` como modelo. **Ação:** continuar o split por tipo (Chart, Gauge,
Map, CustomReportItem), espelhando o particionamento do registry de Repx/RepJson. Combina com o item 5.

---

### 28. Organização por tipo, não por feature ✅

- **Designer:** `Components/` (84 arquivos), `ViewModels/` (65), `Services/` (17). Uma feature — "fonte de dados",
  "parâmetros", "preview", "impressão" — fica espalhada pelas três pastas. A convenção do projeto é feature-first.
  Proposta: `Canvas/`, `PropertyGrid/`, `DataSources/`, `Parameters/`, `Expressions/`, `Preview/`, `Printing/`,
  `Shell/` (menus, toolbar, paleta, abas), cada uma com seus componentes, VMs e serviços.
- **`Core/Elements/`:** 41 arquivos planos. Proposta: `Text/`, `Shapes/`, `Data/` (Tablix/Table), `Visuals/`
  (Chart, Gauge, Map, KPI), `Codes/` (Barcode/QR), `Containers/`, `Actions/`.
- **Raiz do `Layout`:** 22 arquivos, 11 deles parciais do `ReportPaginator`. Proposta: `Pagination/` para o
  paginador e suas parciais, `Prepared/` para documento preparado e streaming, mantendo na raiz só os contratos
  públicos (`IReportPaginator`, `PaginationRequest`, `RenderedReport`…).

Mover arquivos **sem** mudar namespace preserva a API pública (o Package Validation confirma).

---

### 29. Solution plana ✅

`OmniReport.slnx` tem quatro pastas (`/samples/`, `/src/`, `/benchmarks/`, `/tests/`) e os 39 projetos de `src`
numa só. **Ação:** pastas de solução por camada, espelhando o grafo de dependências:

```
/Core/        Core, Expressions, Expressions.Roslyn, Layout, CodeFirst, Serialization, Barcode, Maps
/Data/        DataSources, DataSources.* (9)
/Rendering/   Rendering, Rendering.Skia, Rendering.Gdi
/Output/      Output (item 9), Output.* (10)
/Printing/    Printing, Printing.*
/UI/          Designer.Blazor, Designer.Blazor.DataConnect, Viewer.Blazor
/Hosting/     Hosting.AspNetCore
```

Os testes espelham as mesmas pastas. Opcionalmente, refletir a mesma divisão no disco (`src/Core/…`) — mais caro
(paths de CI, scripts, docs), avaliar depois do 1.0.

---

### 30. Arquivos gerados versionados ✅

- `out-novos/`: **135 arquivos** de saída (PNG, PDF, XLSX, CSV…) versionados desde o commit inicial, sem nenhuma
  referência em código, docs ou scripts.
- `src/Reporting.Designer.Blazor/Docs/index-reference.html`: protótipo HTML que carrega Bootstrap do jsDelivr e
  `lucide@latest` do unpkg — versão não fixada, dentro da pasta de um projeto empacotável.

**Ação.** Remover `out-novos/` e adicioná-lo ao `.gitignore`; mover o protótipo para `docs/` (ou removê-lo) e
conferir que `Docs/` não entra no `.nupkg`.

---

### 31. Projetos sem teste direto ✅

`Maps`, `Printing`, `Rendering` e `Printing.Android` não são referenciados por nenhum projeto de teste.
`Rendering` (as abstrações de contexto, estilos e `ContinuousPageBounds`) é exercitado indiretamente; `Maps` teve
0 % de cobertura na auditoria. **Ação:** testes diretos para `Maps` (resolução de shapes, registro) e para o
contrato de `Printing` (seleção de páginas, cópias); `Printing.Android` depende de dispositivo — manter o build
real no CI e documentar.

---

### 32. Gates de qualidade ausentes no CI ✅

O `ci.yml` coleta cobertura (`--collect:"XPlat Code Coverage"`) e publica o artefato, mas **não há limite**: a
cobertura pode cair a zero sem quebrar nada. E não há `dotnet format --verify-no-changes`, embora o
`.editorconfig` e o `EnforceCodeStyleInBuild` existam. **Ação:** limite de cobertura por pacote do núcleo
(começando no valor atual, para travar regressão) e verificação de formato.

---

### 33. `Culture(CultureInfo.InvariantCulture)` lança ✅

`src/Reporting.CodeFirst/ReportBuilderRoot.cs:136` repassa `culture.Name` para `Language(string)`, que aplica
`ThrowIfNullOrWhiteSpace` — e o nome da cultura invariante é `""`. Guardar `""` não resolve: o paginador trata
string vazia como "nenhuma cultura". **Ação:** sentinela explícito reconhecido pelos dois lados, ou recusar com
mensagem clara.

---

### 34. Paleta de gráficos com cores vizinhas quase iguais 🧪

`ChartRenderer.cs:28-36`: a paleta categórica põe `#C2410C` (laranja-700) e `#B45309` (âmbar-700) nas posições 1
e 4. No sample "Dashboard", as fatias "Jan" e "Abr" da pizza são praticamente indistinguíveis, inclusive na
legenda. **Ação:** paleta validada para distinção perceptual e para daltonismo; teste que garanta distância
mínima entre cores consecutivas.

---

### 35. Espaçamento irregular de glifos no raster 🔍

No preview do sample 01, o cabeçalho "Qtde" aparece como "Qtd e". Hipótese: posicionamento por glifo com
avanços do medidor de texto diferentes dos da fonte efetivamente usada. **Confirmar** com um golden de raster ou
inspeção das posições no PDF antes de corrigir.

---

### 36. Sem ADRs ✅

Não existe `docs/adr/`. Decisões estruturais — RDL como projeção do modelo nativo, Package Validation em vez de
PublicApiAnalyzers, FluentAssertions fixado na 6.x por licença, saída sempre estática, Roslyn opt-in — estão em
comentários, PRs e neste roadmap. **Ação:** ADRs curtos para as decisões já tomadas e para as pendentes (itens 14
e 15).

---

## P4 — Features essenciais de produto

### 37. PDF: senha/criptografia e PDF/A ✅

`PdfExportOptions` só expõe metadados — zero ocorrências de senha, criptografia ou PDF/A em
`src/Reporting.Output.Pdf`. O produto se posiciona em documentos fiscais brasileiros (NFC-e, DANFE): arquivamento
pede PDF/A, e distribuição de holerite/fatura pede senha. Paridade: Crystal, SSRS, Jasper, XtraReports e
Stimulsoft têm senha; Jasper, XtraReports e FastReport têm PDF/A. **Depende do item 1**: PDF/A com fonte inteira
embutida é válido, mas inviável em volume. Priorizar senha (menor esforço), depois PDF/A-3b com validador externo
(veraPDF) no CI.

---

### 38. Streaming real das fontes e exportação progressiva ✅

O #270 entregou documento preparado em disco e emissão progressiva de páginas, mas **as fontes de dados continuam
materializadas** (ordenação, agregados e relações exigem buffer) e os exporters recebem o documento inteiro (item
10). **Ação:** especificar quais relatórios dispensam o buffer (sem ordenação global, sem `Page.Total`, sem
agregado de relatório) e paginar em streaming nesses casos; exporters progressivos via item 10.

---

### 39. Cache de relatório renderizado ✅

Zero ocorrências de `HybridCache`/`IMemoryCache` em `src/`. O `PreparedReport` (#270) é a base natural: cachear o
documento preparado por chave (definição + parâmetros + versão dos dados + tenant), com validade explícita.
Paridade: FastReport tem cache em arquivo de páginas preparadas; SSRS, cache e snapshots.

---

### 40. Viewer: document map, busca e miniaturas ✅

`ReportViewer.razor` (83 linhas) tem navegação, zoom, export e impressão; zero ocorrências de document map, busca
ou miniaturas. O modelo já suporta `Bookmark`, `DocumentMapLabel` e `Action.BookmarkLink`, e o importador RDL os
lê — falta consumir. Document map primeiro; busca exige camada de texto sobre a imagem (o exporter HTML já tem um
overlay que serve de base).

---

### 41. Wizard de criação de relatório ✅

Zero ocorrências de "Wizard" em `src/`. Todo relatório começa em branco. SSRS, Crystal e DevExpress guiam o
usuário (fonte → campos → agrupamento → layout → estilo). Reaproveitar `DataSourceEditorDialog`,
`SchemaExplorerTree` e `ParametersList`.

---

### 42. Oracle sem pacote de 1ª classe ✅

`docs/user-guide.md` e `docs/presentation.html` citam Oracle como suportado; `src/Reporting.DataSources.Oracle/`
não existe. Os quatro conectores SQL são o template (35–58 linhas cada), e o contrato de integração do #237 se
aplica sem alteração. Ou criar o pacote, ou corrigir a documentação.

---

### 43. `WebServiceDataSource` sem REST paginado ✅

Uma única requisição; zero ocorrências de paginação em `src/Reporting.DataSources.WebService`. Estratégias
configuráveis: cursor via campo do payload, offset/limit, header `Link`. Integrar ao limite de linhas
(`PaginationLimits.MaxSourceRows`) para não drenar APIs sem fim.

---

### 44. Sample de `Hosting.AspNetCore` inexistente ✅

`AddReporting` não aparece em nenhum arquivo de `samples/` — o caminho que o README apresenta como recomendado
para produção nunca é exercitado. **Ação:** sample mínimo de API (endpoint que gera PDF) + teste com
`WebApplicationFactory`.

---

### 45. Excel: autofilter e uma aba por grupo ✅

Freeze de cabeçalho e linhas zebradas já existem; autofilter e "uma aba por grupo" não (zero ocorrências em
`src/Reporting.Output.Excel`). Paridade: SSRS, Crystal e Jasper fazem autofilter; SSRS e Jasper quebram aba por
grupo.

---

## P5 — Features recomendadas

### 46. Watermark + rotação ✅

Zero ocorrências de `Watermark` em `src/` e de `Rotation` em `Reporting.Core`. Duas peças: rotação por elemento
(habilita rótulos verticais em colunas estreitas e a própria marca d'água) e watermark de texto/imagem em
`PageSetup` (opacidade, ângulo, atrás/à frente). Nos três modos de autoria.

### 47. Barcodes 2D além de QR ✅

PDF417 tem uso real em documentos brasileiros (CNH, boletos), o que o torna o mais defensável; depois DataMatrix
e Aztec.

### 48. Query builder visual ✅

Hoje: Monaco com SQL + explorador de schema real. Falta o construtor visual com detecção de JOIN do Graphical
Query Designer (SSRS) e do Crystal.

### 49. `StaticMember`/`DynamicMember` no Tablix 🔍

Último gap estrutural conhecido do Tablix (`docs/rdl-compatibility-gaps.md`). O caso comum (coluna fixa "Total")
já é coberto por `ColumnSubtotals`. Definir a semântica antes de implementar. É também o que dá ao `RowSpan`
onde atuar: hoje ele gera o diagnóstico `ORL025` (item 2), que deve sair quando as linhas estáticas existirem.

### 50. Site de documentação + troubleshooting ✅

`docs/` tem ~30 documentos em Markdown solto. Com os XML docs travados no núcleo (#236/#247), um site DocFX com
referência de API navegável passa a valer. Incluir `troubleshooting.md` (fontes no Linux, CSP do Monaco, SkiaSharp
nativo, limites de paginação).

### 51. Meta-pacote de onboarding + template `dotnet new` ✅

O quickstart exige quatro `dotnet add package`. Um meta-pacote `AndersonN.Omni.Report` para o caminho comum e um
template `dotnet new omnireport-blazor` (Designer + Viewer + `AddReporting` prontos) reduzem o tempo até o primeiro
relatório.

### 52. Agendamento e entrega 🔍

Pacote separado, nunca no Core: fila durável, idempotência, retry, credenciais por tenant, auditoria. É o que
separa "biblioteca" de "plataforma" (SSRS subscriptions, Jasper Server). Avaliar se pertence a este repositório ou
ao produto que o consome.

### 53. RTL e `WritingMode` 🔍

`docs/rdl-spec-compliance.md` registra "sempre LTR". Só faz sentido junto da decisão do item 15; a parte de texto
vertical tem interseção com a rotação do item 46.

### 54. Tagged PDF / acessibilidade 🔍

O HTML tem overlay acessível; o PDF não tem árvore de estrutura. Relevante para setor público e licitações.
Validar com veraPDF (PDF/UA).

### 55. Exporters RTF / ODT / PPTX 🔍

`docs/comparison.md` reconhece a lacuna. Baixa prioridade: DOCX cobre o caso "editar no Word".

---

### 56. Várias regiões de dados na raiz (N-DetailBands / List) 🔍

Hoje o relatório tem uma banda Detail ligada a uma fonte; RDL permite várias regiões de dados (`List`, `Tablix`)
lado a lado no corpo. Toca o núcleo da paginação. Registrado antes só no README.

### 57. `ReportSections` do RDL 2016 🔍

Várias seções, cada uma com `PageSetup` próprio (capa em retrato, anexo em paisagem). O export já escreve a
estrutura `ReportSections` com uma única seção, para validar no XSD.

### 58. Fontes e datasets compartilhados (`.rds`/`.rsd`) 🔍

Referências a fontes de dados e datasets externos ao `.rdl`, comuns em servidores SSRS. Hoje o importador só lê o
que está embutido no relatório.

---

## Sequenciamento sugerido

**Sprint 1 — Saída correta (P0).** Itens 6, 7, 8 (P, impacto imediato e visível), depois 1 (o maior ganho
mensurável: ~95 % do tamanho de todo PDF), 5 (teste de reflexão primeiro — previne a próxima perda), 2, 3 e 4.
Ao fim, nada que o modelo aceita é descartado em silêncio na saída.

**Sprint 2 — Fronteiras do 1.0 (P1).** Itens 9, 10 e 11 juntos — é a mesma refatoração (abstração de exportação,
contrato assíncrono com faixa de páginas, dependências por DI) e o único bloco com prazo real: depois do 1.0 vira
major. Junto, 12 e 13. **Antes da sprint**, tomar as decisões 14 e 15 (custam uma conversa, não código).

**Sprint 3 — Designer utilizável por quem não é desenvolvedor (P2).** Começar pelo registro de comandos (o recorte
do Designer no item 26), que resolve 16 e 17 de uma vez; depois 18, 22 e 23 (todos P); depois 19, 20 e 21.

**Sprint 4 — Organização (P3).** Itens 29, 30, 32 e 36 (P, mecânicos), depois 28 e 27 aproveitando o mesmo
movimento de arquivos, e 31. O restante do item 26 (`designer.js`, `ElementViewModel`) em paralelo, por faceta.

**Sprint 5+ — Produto (P4).** 37 (senha, depois PDF/A — já com o item 1 resolvido), 39 e 40, 41, depois 42, 43,
44 e 45 (todos pequenos, bons para preencher folga).

**Ganhos rápidos fora de ordem:** 22, 33, 34, 42 e 44 são de esforço P e independentes de tudo.

---

## Não verificado nesta revisão

| Verificação | Por quê | O que falta |
|---|---|---|
| Navegação por teclado e contraste WCAG | Exige leitor de tela e medição por componente | Auditoria com axe/Lighthouse e teste manual de foco |
| Designer em 768 px | A emulação de viewport do navegador de teste não renderizou o Designer de forma confiável | Repetir em navegador real nas larguras 768 / 1.024 / 1.280 / 1.440 |
| Subsetting de fonte no Linux | A inspeção do PDF foi feita no Windows | Repetir a inspeção do item 1 com o nativo do Linux |
| Espaçamento de glifos (item 35) | Observado só no preview rasterizado | Golden de raster ou posições de glifo no PDF |
| SQL Server, PostgreSQL e MySQL reais | Sem Docker nesta máquina | Coberto no CI pelo `db-integration.yml` |
| Dispositivo Android e impressora física | Exigem hardware | Ver `docs/evolucao-pos-auditoria-2026-09-09.md` |

---

## Histórico de concluídos

| Item (numeração da revisão em que nasceu) | Entrega | PR |
|---|---|---|
| R2 #1 — `Printing.Android` publicado como stub | `IsPackable=false` fora do build Android real | #231 |
| R2 #2 — release sem teste | `release.yml` roda `dotnet test` antes do `pack` | #231 |
| R2 #3 — README com imagens quebradas no NuGet | URLs absolutas | #231 |
| R2 #4 — conectores SQL sem teste | Um contrato contra os três motores, em banco real; achou defeito de cancelamento no `AdoNetDataSource` | #237 |
| R2 #5 — CI Linux com 15/39 projetos | Lista derivada automaticamente de todos os projetos cross-platform | #232 |
| R2 #6 — sem gate de breaking change | Package Validation contra a 0.1.1 publicada, nos 39 pacotes | #235 |
| R2 #7 — XML docs em 1 de 39 pacotes | Core, Expressions, DataSources, Layout e Rendering documentados e travados (CS1591) | #236, #247 |
| R2 #8 — sem Dependabot/CodeQL/`SECURITY.md` | Dependabot agrupado, CodeQL, auditoria de vulnerabilidades, `SECURITY.md` | #232, #250 |
| R2 #9 — CHANGELOG desatualizado | `[Unreleased]` reconstruído; release falha sem seção da versão | #234 |
| R2 #10 — `Verify.Xunit` sem uso | Golden files de display list e SVG | #233 |
| R2 #12 — travessia duplicada nos serializadores | Rede de paridade por reflexão no caminho RDL (perdas conhecidas declaradas; resto no item 5) | #219 |
| R2 #13 — gradiente ausente no GDI | Linear e radial em `GdiRenderingContext` | #221 |
| R2 #14 — export síncrono | `ExportAsync` na interface (resto no item 10) | #227 |
| R2 #15 — leitura repetida das fontes | Cada fonte lida uma vez por paginação (streaming no item 38) | #225 |
| R2 #16 — Excel descarta conteúdo sem avisar | `ExcelExportOptions.OnWarning` | #228 |
| R2 #17 — `ElementViewModel` god object | Mapeamento de domínio extraído para facetas (parte 1; resto no item 26) | #226 |
| R2 #18 — `catch` nus no `Layout` | Filtrados por tipo | #220 |
| R2 #19 — `RdlImporter`/`RdlWriter` monolíticos | Bloco Tablix extraído em parciais (resto no item 27) | #230 |
| R2 #20 — sem benchmarks | `Reporting.Benchmarks` (paginação, fontes de dados) | #229, #243 |
| R2 #21 — perda do "inherit" de `ForeColor`/`Font` | O VM lembra que o modelo dizia "herdar" e devolve `null` | #222 |
| R2 #23 — garantia de fontes no PDF | Teste de embutimento — **reaberto** como item 1 (não mede subsetting) | #248 |
| R2 #30a / #30b — SkiaSharp 4 e NCalc 7 | Migrações com teste da garantia semântica antes | #253, #254, #269 |
| Item 2 — `RowSpan` e linhas-modelo extras descartados sem aviso | Diagnóstico `ORL025` (o span depende do item 49) | #274 |
| F01–F27 da auditoria técnica | Ver `docs/correcoes-f01-f27-2026-09-08.md` | #260 |
| Itens 3 e 4 — borda em qualquer elemento e raio de retângulo sem filhos | Borda sobre o conteúdo no tamanho final; `CornerRadius` na primitiva | #275 |
| Seis recomendações pós-auditoria + F28 | Preview sob demanda, limites, F24/F25, cache limitado, isolamento do designer, telemetria, um tipo por arquivo | #270 |
| Item 5 — export RDL sem `ToggleItem`/`InitiallyHidden` | `<Visibility>` completa + `omni:Visible`; fora da lista de perdas | #276 |
| Advisories do `System.Security.Cryptography.Pkcs` | Pin na 10.0.13 | #271 |
| Item 6 — elemento-fantasma selecionado após desfazer | Seleção podada a cada mudança do histórico | #277 |
| Item 7 — exportação no Designer sem registro, sem erro visível e com cliques perdidos | Menu do registro, alerta de falha, estado ocupado | #280 |
| Item 8 — documentação desatualizada | README, badges, CHANGELOG e aviso de evidências locais | #281 |
| Item 9 — contrato de exportação no pacote PDF | Pacote `Reporting.Output`; exportadores sem SkiaSharp | #282 |

**Lições que valem para os próximos itens:**

- *Ausência de identificador não prova ausência de comportamento* — três falsos positivos da 2ª revisão.
- *Presença de marcador não prova o comportamento* — o prefixo `AAAAAA+` não prova subsetting (item 1).
- *Uma major que "compila e passa" é o cenário mais perigoso* — NCalc 7 passou nos 165 testes com a aritmética em
  `double`; SkiaSharp 4 passou no Windows com zero pixel de texto no Linux.
- *Um teste que só compara valor não pega mudança de tipo; um teste que só procura um token não pega o conteúdo
  errado.* Escreva o teste da garantia semântica antes de mexer.
