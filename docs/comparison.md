# Comparação: OmniReport × RDL oficial × outras engines

> Documento comparativo **honesto** — descreve forças **e** limitações do OmniReport. Os números de
> conformidade RDL vêm de [`rdl-spec-compliance.md`](rdl-spec-compliance.md) (estado pós-#146).

## Sumário

- [Posicionamento em uma linha](#posicionamento-em-uma-linha)
- [Comparação com o RDL oficial da Microsoft](#comparação-com-o-rdl-oficial-da-microsoft)
- [Comparação com outras engines de relatórios](#comparação-com-outras-engines-de-relatórios)
- [Tabela rápida de decisão](#tabela-rápida-de-decisão)
- [Quando escolher (e quando não escolher) o OmniReport](#quando-escolher-e-quando-não-escolher-o-omnireport)

---

## Posicionamento em uma linha

**OmniReport** é uma engine de relatórios **.NET-nativa, open-source (MIT)**, com **três modos de autoria
intercambiáveis** — code-first fluente, modelo imutável low-level e Designer Blazor WYSIWYG — que exporta
para **9 formatos** (PDF/PNG/SVG/HTML vetoriais e XLSX/DOCX/CSV/Markdown/JSON tabulares), imprime em spooler
Windows e térmica ESC/POS, e **importa `.rdl` do SSRS** (~80% de conformidade no importador, ~85% global).

---

## Comparação com o RDL oficial da Microsoft

O RDL (Report Definition Language) é o XML que define relatórios no SSRS / Power BI Report Server. O OmniReport
o trata como **cidadão de 1ª classe**, mas é honesto: **não é um clone 100% conforme** — `100%` é a *meta*
declarada, não o estado atual.

### Conformidade por dimensão (estado atual)

| Dimensão | Conformidade | Comentário |
|---|:--:|---|
| Round-trip interno (`.repx`/`.repjson`) | **~98%** | Praticamente lossless; auto-wiring por convenção |
| Render | **~95%** | Estilo, multi-coluna, Rectangle-container + clip, paginação completa; faltam ticks de gauge e RowSpan |
| Code-first | **~91%** | API cobre quase tudo; faltam spans no Tablix builder fluente |
| Model | **~85%** | Hidden/Nullable, DataSetName, ColSpan/RowSpan, Sizing; faltam TablixHeader/Body nativos, N-DetailBands |
| Designer | **~85%** | Toolbox + edição aninhada; faltam editores ricos e canvas WYSIWYG real |
| Import (`RdlImporter`) | **~80%** | DataSets + Tablix + viz + CustomReportItem + estilo + cultura |
| **Global (ponderada por uso real)** | **~85%** | Migração SSRS→.NET viável |

### O que o OmniReport cobre bem do RDL

- **Page setup** completo (papel, margens, orientação, multi-coluna/snake).
- **Parâmetros** (DataType, DefaultValue, AvailableValues estáticos e por query, multi-valor, Nullable, Hidden).
- **ReportItems simples**: Textbox (com `TextRuns` multi-estilo + ação por-run), Rectangle como **container**,
  Image (Stretch/Fit/Fill/Native), Line, Subreport.
- **DataSets** (Fields, CalculatedFields, Filters, SortExpressions, QueryParameters), Relations master-detail.
- **Tablix**: tabela plana → bandas que **paginam nativamente e repetem cabeçalho**, **+ matrix/pivô** com
  grupos aninhados, subtotais e ColSpan.
- **Data-viz**: Chart, Gauge, DataBar, Sparkline, Indicator — e **CustomReportItem** (wrapper 2008) é mapeado
  para o elemento de 1ª classe correspondente no import.
- **Estilo / visibilidade estática / Action / Bookmark / PageBreaks / cultura (`<Language>`) / metadados**.
- **Formatação condicional** importada de `<Style>` com expressões (negativo-em-vermelho, zebra, semáforo).
- **Expressões VB-SSRS** convertidas automaticamente: `Fields!`/`Parameters!`/`Globals!`, `Sum`/`Avg`/`Count`/
  `Min`/`Max`/`RunningValue`/`RunningTotal`/`First`/`Last`/`CountDistinct`/**`Var`/`StDev`**, `Lookup`/`LookupSet`/
  **`MultiLookup`**, `RowNumber`/`Previous`, `IIf`/`Switch`, e o vocabulário VB de texto/data/formatação.

### O que ainda difere (lossy — **com aviso explícito**, nunca descarte silencioso)

O importador registra cada perda em `Metadata["ImportWarnings"]`:

- **Map** — impedância espacial (RDL usa shapefiles/geometria; OmniReport usa lat/long + GeoJSON).
- **Shapes de Tablix exóticos** — `TablixHeader`/`Body` nativos, RowSpan implícito vertical, repeat-headers de
  matrix, híbrido tabela+matrix.
- **`MarkupType=HTML`** em TextRun — achatado para texto.
- **Drill-down / toggle interativo** — **fora de escopo por decisão de produto**: a saída do OmniReport é
  sempre um **relatório estático**. Visibilidade **estática** (`Visible`/`VisibleExpression`) funciona.
- **Funções de baixíssimo uso ainda ausentes**: `InScope`, `Level` (recursivo), coleção `DataSets!`.

> **Resumo justo:** o OmniReport oferece um caminho de **migração SSRS→.NET viável e honesto sobre suas
> perdas**, não um clone drop-in 100% conforme. Para a maioria dos relatórios operacionais reais, importa e
> edita; para relatórios que dependem de interatividade em runtime ou de geometria espacial RDL, há limites
> documentados.

---

## Comparação com outras engines de relatórios

Comparação **factual e justa** entre OmniReport e as principais engines .NET (SSRS, DevExpress XtraReports,
Telerik Reporting, FastReport .NET, Stimulsoft) e a referência Java (JasperReports). Os incumbentes comerciais
têm **anos de maturidade** que o OmniReport (mais novo) ainda não alcançou em alguns eixos — isto está dito
abaixo sem maquiagem.

### Modos de autoria

- **OmniReport** — três modos com **paridade real** sobre o mesmo `ReportDefinition`: code-first fluente
  (`ReportBuilder`), low-level (records imutáveis) e Designer Blazor WYSIWYG. Um relatório feito em um modo é
  editável e exporta **idêntico** nos outros.
- **SSRS/RDL** — designer visual (Report Builder / VS) é o caminho primário; o RDL é XML editável à mão, mas
  **não há API fluente** de construção.
- **DevExpress / Telerik / FastReport / Stimulsoft** — designer visual maduro (desktop + web) é o forte; também
  permitem construção por código, mas o designer é o centro de gravidade.
- **JasperReports** — JRXML + Jaspersoft Studio (Eclipse); API existe mas é verbosa.

### .NET-nativo / cross-platform

- **OmniReport** — 100% .NET/C#, cross-platform via SkiaSharp; roda em Blazor Server/WASM e MAUI Hybrid. Só os
  drivers GDI+/Windows-spooler são Windows-only por design.
- **DevExpress / Telerik / FastReport / Stimulsoft** — nativos .NET, maduros; forte histórico em .NET Framework
  e suporte crescente a .NET moderno (alguns componentes legados ainda dependem de `System.Drawing`/Windows).
- **SSRS** — .NET, porém acoplado ao SQL Server / serviço de relatórios.
- **JasperReports** — **Java**; consumo em .NET exige ponte/serviço.

### Formatos de saída

- **OmniReport** — 9 exportadores (PDF/PNG/SVG/HTML + XLSX/DOCX/CSV/Markdown/JSON) + drivers Windows-spooler e
  **ESC/POS térmico**. Nichos raros: **ESC/POS**, **Markdown GFM** e **JSON determinístico** (snapshot/RAG).
- **Comerciais (.NET)** — cobrem PDF/XLSX/DOCX/HTML/imagem/CSV e geralmente **mais** (RTF, PPTX, ODT em alguns).
- **SSRS** — PDF/Excel/Word/CSV/XML/imagem.
- **JasperReports** — amplíssimo (PDF/XLSX/DOCX/HTML/CSV/RTF/ODT/PPTX). Markdown e ESC/POS térmico não são
  padrão na maioria.

### Compatibilidade RDL

- **OmniReport** — importa `.rdl` SSRS real (~80%) com avisos explícitos; round-trip próprio (~98%).
- **SSRS** — **define** o RDL (referência canônica, 100% por definição).
- **DevExpress / Telerik** — importadores/conversores de RDL/RDLC com fidelidade parcial conhecida.
- **FastReport / Stimulsoft** — formatos próprios; algum import RDL/MRT variável.
- **JasperReports** — JRXML próprio, sem foco em RDL.

### Open-source / Licença

- **OmniReport** — **MIT** (permissiva, sem royalties, sem licença por dev/servidor/deploy, código auditável).
- **DevExpress / Telerik / FastReport .NET / Stimulsoft** — **comerciais**, licença paga (por desenvolvedor
  e/ou runtime/servidor).
- **SSRS** — incluído/atrelado ao licenciamento do SQL Server.
- **JasperReports** — edição community open-source (LGPL/AGPL) + comercial Jaspersoft; o concorrente mais
  próximo em abertura, porém **Java**.

### Designer visual

- **OmniReport** — Designer Blazor WYSIWYG: canvas drag-drop, outline tree, toolbox, PropertyGrid orientado a
  metadados, browser de data source com schema, editor de expressões (Monaco), undo/redo, alinhamento
  multi-seleção, edição aninhada de Rectangle. **Maduro mas mais novo**; pendências: preview canvas 100%-fiel
  (hoje placeholder no canvas — o **render real** já existe no output), editores ricos de TextRuns/spans.
- **Comerciais + SSRS** — designers **extremamente maduros** (anos de mercado), desktop e web, com preview
  fiel, wizards e WYSIWYG refinado. **Em maturidade pura de designer, os incumbentes ainda lideram.**

### Expressões / scripting

- **OmniReport** — linguagem via **NCalc** + conversor VB-SSRS automático: coleções, agregados (incl. `Var`/
  `StDev`/`CountDistinct`), `Lookup`/`LookupSet`/`MultiLookup`, posicionais, formatação. Scripting C# arbitrário
  via **`CodeElement` + pacote opt-in `Reporting.Expressions.Roslyn`** (executa C# — só fontes confiáveis).
- **SSRS** — expressões VB.NET completas + blocos de código e assemblies custom (superconjunto).
- **DevExpress / Telerik / FastReport / Stimulsoft** — expressões + scripting C#/VB embutido.
- **JasperReports** — expressões Java/Groovy.

### Extensibilidade

- **OmniReport** — interfaces limpas (`IReportDataSource`, `IReportExporter` + registry, `IReportPrinter`) e
  modelo imutável com **serialização auto-wired por convenção** (adicionar um `ReportElement` novo é
  round-trip-lossless **sem editar serializers**). Open-source: forkável e auditável.
- **Comerciais** — extensibilidade rica e documentada, porém em produto **fechado e pago**.
- **SSRS** — extensões de dados/render e CustomReportItems, com acoplamento à plataforma.
- **JasperReports** — muito extensível e open-source, mas Java.

---

## Tabela rápida de decisão

| Critério | OmniReport | SSRS | DevExpress / Telerik / FastReport / Stimulsoft | JasperReports |
|---|:--:|:--:|:--:|:--:|
| Licença | **MIT (grátis)** | SQL Server | Comercial (paga) | LGPL/AGPL + comercial |
| .NET-nativo | **✅** | ✅ | ✅ | ❌ (Java) |
| Cross-platform | **✅** (Skia) | parcial | parcial/✅ | ✅ (Java) |
| 3 modos de autoria c/ paridade | **✅** | ❌ | parcial | ❌ |
| Designer WYSIWYG | ✅ (mais novo) | ✅ | ✅✅ (líder) | ✅ |
| Importa `.rdl` | ✅ ~80% | **define** | parcial | ❌ |
| ESC/POS térmico | **✅** | ❌ | raro | ❌ |
| Saída Markdown/JSON | **✅** | ❌ | raro | parcial |
| Código auditável / forkável | **✅** | ❌ | ❌ | ✅ |

---

## Quando escolher (e quando não escolher) o OmniReport

**Escolha o OmniReport quando:**

- Você quer uma engine **.NET moderna, cross-platform e sem custo de licença** (MIT).
- Precisa **autorar por código E visualmente** com paridade (ex.: gerar relatórios por API e deixar o usuário
  final ajustar no designer).
- Tem cenários de **PDV/cupom térmico (ESC/POS)**, **NFC-e/DANFE**, ou saídas **Markdown/JSON** (snapshot/RAG).
- Quer **migrar relatórios SSRS** para um stack .NET próprio e aceitar perdas documentadas.
- Valoriza **código auditável/forkável** e extensibilidade sem licença fechada.

**Prefira um incumbente comercial / SSRS quando:**

- Precisa de **drill-down/toggle interativo em runtime** (o OmniReport gera saída **estática**).
- Depende de **100% de fidelidade RDL** ou de recursos de Tablix/Map muito específicos do SSRS.
- Precisa do **designer mais maduro do mercado** com anos de polimento e wizards.
- Quer **suporte comercial** com SLA.
