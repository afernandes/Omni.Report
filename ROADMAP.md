# OmniReport — Roadmap

**Baseline:** `main` @ `f83ea38` + PR #215 · **Versão publicada:** 0.1.1 · **Revisão:** 2ª (a 1ª tinha baseline PR #214)

## Como este documento foi produzido

Varredura em cinco frentes — débito estrutural, cobertura de testes, gaps de produto, Designer, infra/release —
cruzando código-fonte, `docs/` e histórico git. Esta revisão **re-verificou** os itens da anterior contra o
código atual e acrescentou os gaps de paridade com outras engines (SSRS, Crystal, JasperReports, DevExpress
XtraReports, Telerik, FastReport, Stimulsoft).

**Níveis de confiança** — respeite-os antes de agir:

- ✅ **Verificado**: confirmado por leitura direta do arquivo/linha citado **nesta revisão**.
- 🔍 **Relatado**: veio da varredura anterior com evidência `arquivo:linha`, mas não foi reconfirmado agora.
  **Confirme antes de abrir o PR.**

> **Duas armadilhas já mapeadas — não "corrija" nenhuma das duas:**
> 1. "`RepeatColumnHeaders`/`MinColumnWidth` não editáveis no Designer" — **falso**: entregues no PR #211 via
>    anotação `[PropertyGrid]` no modelo (por isso não aparecem hardcoded em `PropertyGrid.razor`).
> 2. "`catch (Exception)` genéricos em `DesignerDataConnect`" — **falso positivo verificado**: os 5 catches
>    (`DesignerDataConnect.cs:59,102,161,191,226`) envolvem operações user-facing (testar conexão, descobrir
>    schema, preview) que **devem** converter qualquer falha em mensagem para o usuário. Estreitá-los faria o
>    Designer estourar exceção em erro não-antecipado. O item 18 trata de outros pontos, não destes.

### Estado geral do projeto

A engenharia é madura para um pré-1.0: build determinístico, SourceLink, `.snupkg`, Trusted Publishing via OIDC,
`TreatWarningsAsErrors=true`, ~1.200 testes, zero `TODO`/`FIXME`/`NotImplementedException` em `src/`, zero
`#pragma warning disable`, 39 projetos publicáveis. A compatibilidade RDL fechou >90% dos gaps rastreados, e a
paginação do Tablix está completa em 2D (linha #197/#207 + coluna #209).

Por isso os problemas críticos **não são de falta de processo** — são de *divergência entre o que a documentação
ou o comentário afirma e o que o MSBuild ou o runtime realmente faz*. Passam despercebidos justamente porque a
área parece coberta.

**Legenda de esforço:** P ≤1 dia · M 2–5 dias · G 1–3 semanas · GG >3 semanas

### O que mudou desde a 1ª revisão

| Mudança | Detalhe |
|---|---|
| ✅ **P0 #1 resolvido** | `ReportPaginator` thread-safety: `PaginateAsync` roda cada execução numa **instância privada** (`ReportPaginator.cs:63`), compartilhando só o `ExpressionCompiler` (cache concorrente). Guardado por `PaginatorConcurrencyTests`. Saiu da lista. |
| ✅ **Sintoma do #7 corrigido** | Os `CS1574` do `Sample11` foram corrigidos (PR #215) — mas a **causa raiz** (CS1591 só travado no CodeFirst) continua no item 7. |
| ➕ **4 itens novos** | Gaps de paridade com outras engines não mapeados antes: **23** (embedding de fontes), **31** (watermark + rotação), **33** (Excel freeze/autofilter), **39** (export de intervalo de páginas). |
| ➖ **2 não-itens** | `AllowMultiple` (multi-value params) **já existe** em `ReportParameter.cs:12`; **undo/redo já existe** no Designer (`Commands.cs:11,23-25`). Ambos saíram da consideração. |

---

## Lista priorizada

### P0 — Bloqueadores de 1.0 (integridade de entrega)

| # | Item | Tipo | Esforço | Conf. |
|---|---|---|---|---|
| 1 | `Printing.Android` publicado no NuGet como stub que só lança `PlatformNotSupportedException` | Bug de release | P | ✅ |
| 2 | `release.yml` publica no NuGet.org sem rodar nenhum teste | Processo | P | ✅ |
| 3 | README embutido nos 39 pacotes com imagens quebradas no NuGet.org | DX/imagem | P | ✅ |

### P1 — Confiabilidade verificável (o que hoje é afirmado, não testado)

| # | Item | Tipo | Esforço | Conf. |
|---|---|---|---|---|
| 4 | Zero teste nos 3 conectores SQL de produção (SqlServer/PostgreSql/MySql) | Teste | M | 🔍 |
| 5 | CI Linux cobre 15/39 projetos — metade da superfície "cross-platform" nunca é verificada | CI | P | 🔍 |
| 6 | Sem gate de breaking change de API pública | Processo | M | ✅ |
| 7 | XML docs obrigatórios em 1 de 39 pacotes → IntelliSense vazio | DX | M | ✅ |
| 8 | Sem Dependabot, CodeQL, scan de vulnerabilidade nem `SECURITY.md` | Segurança | P | ✅ |
| 9 | `CHANGELOG.md` ~91 PRs desatualizado | Doc | P | ✅ |
| 10 | `Verify.Xunit` referenciado sem uso → zero regressão visual real | Teste | M | ✅ |
| 11 | `Expressions.Roslyn` executa C# arbitrário sem sandbox, com 5 testes | Segurança | G | 🔍 |

### P2 — Débito técnico estrutural

| # | Item | Tipo | Esforço | Conf. |
|---|---|---|---|---|
| 12 | Travessia do modelo duplicada em 6 serializadores | Arquitetura | G | ✅ |
| 13 | Backend GDI não implementa gradiente → some silenciosamente na impressão | Paridade | P | ✅ |
| 14 | `IReportExporter.Export` síncrono, sem `CancellationToken` | API | M | ✅ |
| 15 | `MaterializeAsync` sem streaming → memória proporcional ao dataset inteiro | Performance | G | 🔍 |
| 16 | Excel/DOCX descartam gráficos, imagens e estilo sem avisar | Paridade | M | ✅ |
| 17 | `ElementViewModel`: God object de ~1.400 linhas e ~76 propriedades | Arquitetura | G | 🔍 |
| 18 | `catch` nus em 5 pontos de `Layout` mascaram bugs reais | Robustez | P | ✅ |
| 19 | `RdlImporter`/`RdlWriter` monolíticos (1.713 e 1.376 linhas) | Arquitetura | M | 🔍 |
| 20 | Sem benchmarks — nenhuma barreira contra regressão de performance | Performance | M | ✅ |
| 21 | Perda da semântica "inherit" de `ForeColor`/`Font` no Designer | Bug sutil | P | 🔍 |

### P3 — Features essenciais de produto

| # | Item | Tipo | Esforço | Conf. |
|---|---|---|---|---|
| 22 | PDF: senha/criptografia e PDF/A (arquivamento fiscal) | Feature | G | ✅ |
| 23 | **Embedding/subsetting de fontes no PDF** — pré-requisito do 22 | Feature | M | ✅ |
| 24 | Cache de relatório renderizado | Feature | M | ✅ |
| 25 | Viewer sem document map, busca ou miniaturas | Feature | M | 🔍 |
| 26 | Designer sem wizard de criação de relatório | Feature | M | ✅ |
| 27 | Oracle sem pacote de 1ª classe (prometido em doc, ausente em `src/`) | Feature | P | ✅ |
| 28 | `WebServiceDataSource` sem suporte a REST paginado | Feature | M | 🔍 |
| 29 | Sample de `Hosting.AspNetCore` inexistente | DX | P | ✅ |
| 30 | UI do Designer 100% hardcoded em pt-BR, sem i18n | Feature | G | ✅ |

### P4 — Features recomendadas (diferenciação e alcance)

| # | Item | Tipo | Esforço | Conf. |
|---|---|---|---|---|
| 31 | **Watermark + rotação de texto/elemento** | Feature | M | ✅ |
| 32 | Editor de rich text (`TextRuns`) no Designer | Feature | M | 🔍 |
| 33 | **Excel: autofilter e uma aba por grupo** (freeze já existe — ver nota) | Feature | P | 🟡 |
| 34 | Barcodes 2D além de QR (DataMatrix, PDF417, Aztec) | Feature | M | 🔍 |
| 35 | Query builder visual (hoje só SQL em texto) | Feature | G | 🔍 |
| 36 | RowSpan no corpo do Tablix + `StaticMember`/`DynamicMember` | RDL | G | 🔍 |
| 37 | Site de documentação (DocFX) + `troubleshooting.md` | DX | M | 🔍 |
| 38 | Meta-pacote de onboarding | DX | P | 🔍 |
| 39 | **Export de intervalo de páginas** | Feature | P | ✅ |
| 40 | Agendamento e entrega por e-mail de relatórios | Feature | GG | 🔍 |
| 41 | RTL e `WritingMode` (árabe/hebraico, texto vertical) | i18n | G | 🔍 |
| 42 | Tagged PDF / acessibilidade | Acessib. | G | 🔍 |
| 43 | Exporters RTF / ODT / PPTX | Feature | G | 🔍 |

---

# Detalhamento

## P0 — Bloqueadores de 1.0

### 1. `Printing.Android` publicado como stub inoperante ✅

**O problema.** O pacote `AndersonN.Omni.Report.Printing.Android` é publicado no NuGet.org contendo o *stub* —
uma DLL cujo único comportamento é lançar `PlatformNotSupportedException`.

**Evidência (re-verificada):**
- `src/Reporting.Printing.Android/Reporting.Printing.Android.csproj` — **`IsPackable` não aparece no arquivo**
  (grep sem nenhum match).
- `Directory.Build.props:51-52` — `IsPackable=false` só se aplica a projetos `.Tests`.
- `Directory.Build.props:43,47` — o projeto casa com `StartsWith('Reporting.')`, então **recebe `PackageId`** e
  é empacotável por padrão do SDK.
- Sem `OMNIREPORT_BUILD_ANDROID=true` (não setado em nenhum workflow), compila como `net10.0` com
  `OMNIREPORT_ANDROID_STUB`, e `AndroidPrintFrameworkPrinter.cs:24,29` lança `PlatformNotSupportedException`.
- O comentário do CI em `ci.yml:155-157` afirma que o stub "fica de fora via `<IsPackable>false</IsPackable>`" —
  **a propriedade não existe**.
- É anunciado ao usuário em `README.md:169`.

**Por que é crítico.** `dotnet add package …Printing.Android` entrega hoje um pacote que não faz nada além de
lançar exceção. É a pior classe de defeito de release: publicado, anunciado e inoperante.

**Ação.** Adicionar ao `.csproj`:
```xml
<IsPackable Condition="'$(OMNIREPORT_BUILD_ANDROID)' != 'true'">false</IsPackable>
```
Se o pacote Android for realmente desejado, publicá-lo num job separado com o workload `android` instalado.
Corrigir o comentário mentiroso em `ci.yml:155-157`.

**Pronto quando:** `dotnet pack OmniReport.slnx` não gera o `.nupkg` do Android sem a variável; o comentário do
CI reflete o mecanismo real. Considerar despublicar/deprecar a versão já publicada.

---

### 2. `release.yml` publica sem rodar testes ✅

**O problema.** O job de release faz `restore` → `build` → `pack` → `push` sem nenhum `dotnet test`.

**Evidência (re-verificada):** `.github/workflows/release.yml` — grep por `dotnet test` **não retorna nada**;
os únicos passos são `dotnet pack` (`:68`) e `dotnet nuget push` (`:99`, `:117`).

**Por que importa.** Um `workflow_dispatch` numa tag cujos testes nunca rodaram (ou rodaram vermelhos) publica
pacotes quebrados **irreversivelmente** — o NuGet.org não permite reupload da mesma versão, só deprecação. O
item 1 é exatamente a categoria de defeito que um gate de release pegaria.

**Ação.** Inserir `dotnet test` antes do `pack`, ou exigir que a tag aponte para um commit com check verde
(`gh api` verificando o status do commit).

**Pronto quando:** um release com teste vermelho falha antes do `push`.

---

### 3. README com imagens quebradas nas 39 páginas do NuGet ✅

**O problema.** Todo pacote embute o `README.md` da raiz, que referencia imagens por caminho relativo — mas a
pasta `assets/` nunca entra no `.nupkg`.

**Evidência:**
- `Directory.Build.props:62-63` — embute `README.md` em todo pacote empacotável.
- `README.md:30-32,39-40` — `<img src="assets/designer-light.png">`, `sample-dashboard.png`, `sample-map.png`,
  `sample-tablix.png`, `sample-nfce.png`.
- `assets/` existe com os PNGs, mas **nenhum `.csproj`/`.props` referencia a pasta**.
- O bloco ```mermaid``` do README também não é renderizado pelo NuGet.org.

**Por que importa.** A aba "Readme" de todos os 39 pacotes mostra imagens quebradas — é a primeira impressão de
quem avalia a biblioteca. Agrava-se pelo README ser monolítico: quem abre o pacote `Barcode` recebe um documento
sobre o produto inteiro.

**Ação.** Trocar os caminhos por URLs absolutas (`https://raw.githubusercontent.com/<org>/<repo>/main/assets/…`)
— corrige o NuGet.org sem prejudicar o GitHub. Opcionalmente, gerar um README curto por pacote via target
MSBuild. Adicionar `PackageIcon` (hoje ausente em todos os pacotes).

**Pronto quando:** inspeção do `.nupkg` mostra README com URLs resolvíveis; ícone presente.

---

## P1 — Confiabilidade verificável

### 4. Zero teste nos 3 conectores SQL de produção 🔍

`Reporting.DataSources.SqlServer` (160 LOC), `.PostgreSql` (180) e `.MySql` (157) não são referenciados por
nenhum `.csproj` de teste. São exatamente o tipo de shim "óbvio demais para errar" onde falhas de mapeamento de
tipo e connection string custam caro — e o SQL Server é provavelmente o conector mais usado em ambiente
corporativo. Comparar com `.Sqlite`, que ao menos tem 4 testes.

**Ação.** Testes de integração com Testcontainers (SQL Server, PostgreSQL, MySQL) cobrindo abertura de conexão,
mapeamento de tipos (datas, decimais, nulos, `uniqueidentifier`), streaming e cancelamento. Rodar num job de CI
separado (marcado, não bloqueante em PR se lento).

**FEITO.** `tests/Reporting.DataSources.Integration.Tests` — **um contrato só, executado contra os três motores**.
Eles são o mesmo shim três vezes (cada um entrega uma fábrica de conexão ao `AdoNetDataSource`), então uma cópia
de teste por motor iria divergir, e um comportamento que só um deles acerta é justamente o bug que interessa.

Cobre exatamente o que um shim fino erra: nulo chegando como `DBNull` em vez de `null` (não é nulo — toda
checagem a jusante falha calada e o valor renderiza como `System.DBNull`), `decimal` virando `double`
(`1450.90` → `1450.8999999999999` num relatório financeiro), `DateTime` truncado para meia-noite, booleano do
MySQL (que não tem o tipo — `TINYINT(1)` é a convenção), parâmetro por nome, leitura preguiçosa e cancelamento.

Roda no workflow `db-integration.yml`: em PR que toca conector ou o `AdoNet` compartilhado, em push na `main`,
semanalmente e sob demanda. Imagens **pinadas por tag** — com `latest` a suíte mudaria de comportamento sem
nenhum commit.

Dois detalhes que evitam falso-verde, ambos deliberados:

1. **O gate vive no `DockerFactAttribute`, não no workflow.** Em CI os testes só rodam com
   `OMNIREPORT_DB_TESTS` setada. Sem isso, o job Linux do `ci.yml` — que deriva a lista de projetos
   automaticamente — passaria a puxar três imagens de banco em todo PR. Manter a decisão no atributo torna o
   comportamento independente de como cada job escolhe seus projetos.
2. **O workflow falha se algum teste for pulado.** Um job cuja única razão de existir é rodar contra banco real
   não pode ficar verde tendo pulado tudo; perder o Docker no runner viraria um verde silencioso, que é o
   defeito que este roadmap mais encontrou.

**A primeira execução real já encontrou um defeito de produção.** `AdoNetDataSource` delegava o cancelamento
inteiramente ao token passado para `DbDataReader.ReadAsync`. Quando o driver já tem as linhas em buffer local
esse método retorna **de forma síncrona sem consultar o token** — ou seja, o cancelamento era honrado por
acidente de driver: o SqlClient consulta, o Npgsql e o MySqlConnector não. Um render cancelado continuava
drenando o resultado inteiro no PostgreSQL e no MySQL. Foi exatamente rodar o **mesmo teste nos três motores**
que tornou isso visível — passava em um, falhava em dois. Corrigido com `ThrowIfCancellationRequested()` por
linha (`AdoNetDataSource.cs:143`).

Placar da primeira execução contra bancos reais: 25 de 27 verdes, as 2 falhas sendo esse defeito nos dois
motores afetados.

---

### 5. CI Linux verifica só 15 de 39 projetos 🔍

O job `build-linux` (`ci.yml:78-132`) restaura/compila 15 projetos e testa 12 de 23. **Nunca são compilados no
Linux**, apesar de terem TFM `net10.0` puro: todos os `DataSources.*` (AdoNet, Sqlite, PostgreSql, SqlServer,
MySql, Json, Xml, WebService, FileSystem), `Expressions.Roslyn`, `Maps`, `Designer.Blazor.DataConnect` e os
exporters `Svg`, `Html`, `Csv`, `Json`, `Xml`, `Markdown`, `Docx`, `Image`.

Essa é justamente a categoria de maior risco cross-platform (drivers nativos, `ClosedXML`, `OpenXml`, I/O de
arquivo, `HttpClient`). A promessa "roda em container Linux" é, para metade da superfície do produto, **não
testada**.

**Nota positiva:** o isolamento Windows-only está bem-feito — `Rendering.Gdi` e `Printing.WindowsSpooler` usam
TFM `net10.0-windows`, então um consumidor Linux nem consegue restaurá-los. O risco não é GDI vazar; é o resto
não ser verificado.

**Ação.** Ampliar o job Linux para a solução inteira, excluindo apenas TFMs `-windows`/`-android`.

---

### 6. Sem gate de breaking change de API pública ✅

Não há `Microsoft.CodeAnalysis.PublicApiAnalyzers` — **nenhum `PublicAPI.Shipped.txt` existe no repositório**
(glob sem match) — nem `ApiCompat` ou equivalente. Com 39 pacotes interdependentes, uma mudança silenciosa em
`Reporting.Core` pode quebrar consumidores dos outros 38 sem aviso. O sinal de "isto é breaking" depende hoje de
disciplina humana no Conventional Commit (`CONTRIBUTING.md:49`).

**Ação.** Adotar `PublicApiAnalyzers` ao menos em `Core`, `CodeFirst`, `Layout` e `Rendering` **antes do 1.0** —
depois do 1.0 o custo de adoção sobe muito.

**FEITO — por outro mecanismo, e mais amplo.** Adotado o **Package Validation** nativo do SDK
(`EnablePackageValidation` + baseline `0.1.1`) em vez do `PublicApiAnalyzers`. Três razões, nesta ordem:

1. **Cobre os 39 pacotes**, não os 4 propostos — a Ação original limitava o escopo exatamente porque o custo
   por projeto do PublicApiAnalyzers é alto.
2. **Não exige `PublicAPI.Shipped.txt` por projeto** (dezenas de milhares de linhas geradas para manter à mão,
   que também podem ficar desatualizadas em silêncio — o defeito que este roadmap mais encontrou).
3. **Compara contra o artefato realmente publicado no nuget.org**, não contra um arquivo no repo. A pergunta
   que importa é "isto quebra quem já consome o pacote?", e essa é literalmente a comparação feita.

Os pacotes 0.1.0/0.1.1 estão publicados, então o baseline existe. Resultado medido: **só 8 dos 39 pacotes têm
quebra desde a 0.1.1** — Core, CodeFirst, Layout, Expressions, Rendering, Rendering.Skia, Rendering.Gdi e
Designer.Blazor — e as supressões somam ~14 KB. Os outros 31 estão estáveis. `Output.Docx`, `Output.Image` e
`Output.Xml` ficam sem baseline por não existirem na 0.1.1; entram quando a 0.2.0 sair.

Verificado por sabotagem: remover `Unit.ToCm()` faz o `pack` falhar com
`CP0002: O membro 'double Reporting.Geometry.Unit.ToCm()' existe em [Linha de base] ... mas não em ...`.

Achado de tabela: o `<Version>` default do repo era `0.1.0-alpha` — **atrás** da 0.1.1 já publicada. Passava
despercebido porque o `release.yml` sobrescreve com a tag, então só quem rodasse `pack` local geraria um pacote
mais velho que o do nuget.org. O `CP0003` do Package Validation foi o que tornou isso visível. Corrigido para
`0.2.0-alpha`.

Procedimento para aceitar uma quebra intencional documentado em `CONTRIBUTING.md`.

---

### 7. XML docs obrigatórios em 1 de 39 pacotes ✅

`GenerateDocumentationFile=true` é global (`Directory.Build.props:9`), mas `CS1591` é suprimido globalmente no
`NoWarn`. Apenas `Reporting.CodeFirst` reverte a supressão (PR #208). Resultado: `Core`, `Layout`,
`Serialization`, todos os `Output.*` e `DataSources.*` podem ter 0% de doc pública sem que o build reclame — e o
IntelliSense do consumidor fica vazio.

**Dimensão medida:** ~444 membros públicos sem `<summary>` só em `Reporting.Core`; ~1.000+ somando Core, Layout
e CodeFirst antes do #208.

Isso também explicava os `CS1574` dos samples: sem warning-as-error para docs fora do CodeFirst, um `cref`
quebrado nunca falha o build. O PR #215 corrigiu os dois crefs do `Sample11`, mas **só o sintoma** — a próxima
doc quebrada volta a passar silenciosa.

**Ação.** Estender o padrão do CodeFirst progressivamente, começando pelos pacotes core. Há trilho pronto: os
PRs #205 e #208 documentaram 32 tipos de Core/Layout e travaram CS1591 no CodeFirst — é continuar por
`Reporting.Core` → `Layout` → `Rendering`.

**FEITO — os cinco pacotes do núcleo documentados e travados.**

**Correção das dimensões declaradas acima**, em duas rodadas. Os "~444 em Core" contavam o warning duplicado que
o MSBuild emite por membro. E o "128 do Layout" somava a cadeia de dependências: compilar o `Layout` compila
`Expressions` e `DataSources` junto, e os avisos deles apareciam no mesmo build. Medido por localização única e
atribuído ao projeto dono, com `--no-incremental` (sem isso o build fica up-to-date e reporta **zero**, que foi
como esses números se distorceram em primeiro lugar):

| Pacote | Membros públicos sem doc | Situação |
|---|--:|---|
| `Reporting.Core` | 220 | documentado e travado |
| `Reporting.Expressions` | 38 | documentado e travado |
| `Reporting.DataSources` | 37 | documentado e travado |
| `Reporting.Layout` | 29 | documentado e travado |
| `Reporting.Rendering` | 24 | documentado e travado |

Somado ao `CodeFirst` (#208), a trava cobre agora **todo o núcleo que um consumidor toca diretamente**. Fechar só
o `Layout` teria travado um pacote e deixado suas duas dependências — igualmente públicas — degradando.

A trava é a mesma do `CodeFirst` (#208): `NoWarn.Replace(';CS1591','')` no csproj do projeto, que somado ao
`TreatWarningsAsErrors` das bibliotecas empacotáveis faz o build **falhar** enquanto houver membro público sem
`<summary>` — e continuar falhando se um novo chegar sem ela. É a parte que importa: sem a trava, a documentação
volta a se degradar no próximo PR.

Como efeito colateral previsto no próprio item, a trava também passa a pegar `cref` quebrado (CS1574) nesses dois
projetos — um foi encontrado e corrigido enquanto a documentação era escrita.

---

### 8. Sem Dependabot, CodeQL nem `SECURITY.md` ✅

`.github/workflows/` tem só `ci.yml` e `release.yml`. **Verificado por glob: não existe `dependabot.yml` nem
`SECURITY.md`**; não há CodeQL nem `dotnet list package --vulnerable`. Já há evidência de correção **reativa** de
CVE (`Directory.Packages.props:60-65`, pin de `SQLitePCLRaw` por `GHSA-2m69-gcr7-jv3q`) — exatamente o que o
Dependabot automatizaria.

**Ação.** `dependabot.yml` (NuGet + Actions), job semanal de auditoria de vulnerabilidade, `SECURITY.md` com
canal de divulgação responsável.

---

### 9. `CHANGELOG.md` ~91 PRs desatualizado ✅

A última entrada é **0.1.1 (2026-06-18)** e `[Unreleased]` diz literalmente **"Nada ainda."**
(`CHANGELOG.md:8-10`, re-verificado) — mas o histórico vai até o PR **#215**, com features substanciais
(gradientes, named styles, cascading parameters, paginação 2D do Tablix, CF por-célula, exporters TIFF/XML) que
os docs de conformidade RDL já descrevem como entregues.

**Ação.** Reconstruir o `[Unreleased]` a partir do log (os commits seguem Conventional Commits, então é
semi-automatizável com `git-cliff`). Adicionar ao checklist de release, ou automatizar de vez — hoje há **duas**
fontes de notas não sincronizadas (o CHANGELOG manual e o `generate_release_notes` do `release.yml:126-132`).

**FEITO.** `[Unreleased]` reconstruído a partir dos 225 PRs desde a v0.1.1, curado por área em vez de uma linha
por commit — um changelog de 225 bullets não é lido. As 136 referências de PR foram conferidas por script contra
`git log v0.1.1..main`: nenhuma inexistente.

A causa estrutural também foi fechada, senão o arquivo envelhece de novo: o `release.yml` passa a extrair a
seção da versão publicada e usá-la como `body_path` do GitHub Release, com o `generate_release_notes`
acrescentando a lista de PRs **abaixo** — as duas fontes somam em vez de competir. E, principalmente, **sem
seção para a versão o release falha**. Esquecer o CHANGELOG deixa de ser omissão invisível e vira bloqueio de
entrega. Extrator testado nos quatro casos, incluindo a colisão de prefixo (`0.1` não casa com `0.1.1`).

---

### 10. `Verify.Xunit` referenciado sem uso → zero regressão visual ✅

`tests/Reporting.Rendering.Tests/Reporting.Rendering.Tests.csproj:20` referencia `Verify.Xunit`, mas
**re-verificado: o único hit de "Verify" no projeto inteiro é a própria linha do `.csproj`** — nenhum `.cs` usa
a API e não existe nenhum `*.verified.*`.

O que existe são testes de contagem de pixels de tinta por região — validam propriedades geométricas, mas **não
comparam contra imagem de referência**. Nada protege contra regressão visual sutil (mudança de anti-aliasing,
gradiente ligeiramente errado, deslocamento de baseline) em PDF/PNG/SVG.

**Ação.** Ou ativar o Verify com golden files para um conjunto representativo de relatórios, ou remover a
dependência morta. A primeira é claramente melhor para um motor de renderização — e tem sinergia com o item 13
(o gradiente ausente no GDI seria pego por um golden file).

**FEITO.** `tests/Reporting.Golden.Tests` — 4 relatórios pinados em duas camadas: o *display list* do paginador
(geometria, fonte, cor, alinhamento, quebra de página, `Page.Total`) e a emissão SVG (preenchimento, traço,
gradiente). A referência morta em `Reporting.Rendering.Tests` foi removida. Verificado por sabotagem: descartar
o gradiente em `StyleResolver.BackgroundBrush` derruba os dois goldens de `Formas` — `fill=#FF8800→#FFFFFF/TopBottom`
vira `fill=#FF8800` e o `<linearGradient>` some do SVG. Estabilidade cross-platform é por construção, não por
sorte: mils inteiros + `AverageWidthTextMeasurer` + cultura fixa + resumo do SVG que descarta os avanços por
glifo (que o Skia tira da fonte resolvida e mudam no Linux). Ver `tests/Reporting.Golden.Tests/README.md`.

---

### 11. `Expressions.Roslyn` sem sandbox, com 5 testes 🔍

O pacote executa C# arbitrário definido no relatório. A varredura não encontrou nenhuma restrição de segurança
(`AppDomain`, `SecurityException`, allowlist de assemblies) em `src/Reporting.Expressions.Roslyn`, e há apenas 5
testes, todos de caminho feliz.

**Por que importa.** Um `.rdl`/`.repx` vindo de fonte não confiável — cenário natural num produto que **importa
arquivos SSRS** — vira execução arbitrária de código no servidor.

> **Atenuante desde a 1ª revisão:** o vazamento de `CodeFunctionResolver` entre requisições concorrentes (antigo
> P0 #1) foi corrigido, então o risco não escala mais de "relatório malicioso" para "comprometimento
> cross-tenant" por essa via. O risco de execução arbitrária **permanece**.

**Ação.** Decisão de produto primeiro: (a) documentar explicitamente que `Code`/Roslyn só deve ser habilitado
para relatórios confiáveis e mantê-lo opt-in — barato e honesto; ou (b) investir em isolamento real (allowlist
de namespaces, `AssemblyLoadContext` restrito, timeout de execução) — caro. Independente da escolha, ampliar os
testes para casos hostis (loop infinito, acesso a `File`/`Process`, stack overflow).

---

## P2 — Débito técnico estrutural

### 12. Travessia do modelo duplicada em 6 serializadores ✅

`RepxWriter`/`RepxReader`, `RepJsonWriter`/`RepJsonReader`, `RdlWriter`/`RdlImporter` implementam **cada um** a
travessia completa de `ReportDefinition → Bands → Elements → Style/Border/Font`, com métodos 1:1 pelo nome.

Este é o maior risco sistêmico do repositório: **todo campo novo no modelo precisa ser replicado em até 6
lugares**, e esquecer um produz perda silenciosa de dados. Não é hipotético — está **confirmado três vezes**, e
a terceira ainda está aberta:

- O PR **#212** corrigiu o export RDL, que descartava `MinColumnWidth`/`RepeatColumnHeaders`/`KeepTogether`
  (entregues em #197/#209). Ler → salvar `.rdl` → reler **perdia** o comportamento de paginação.
- O PR **#217** corrigiu o *importador* RDL, que descartava `ToggleItemId`/`InitiallyHidden`: importar um `.rdl`
  com `<Visibility><ToggleItem>` transformava um relatório com drill-down num relatório estático.
- ⚠️ **Ainda aberto:** o `RdlWriter` **também** não emite `<ToggleItem>` — só o import foi fechado no #217. O
  round-trip `.rdl` de drill-down continua com perda no sentido export.

Os três foram achados por revisão, não por teste — não há nada no build que impeça o quarto caso.

**Contexto importante:** já existe trilho para resolver — o `ElementSerializationRegistry` (auto-wiring por
convenção, PRs #74/#75/#76) reduziu os 4 switches de Repx/RepJson. O RDL ficou de fora por ser projeção com
semântica própria (e os 3 campos do #212 tiveram de ser escritos à mão como `omni:` CustomProperties).

**Ação.** Duas frentes, na ordem:
1. **Teste de completude por reflexão** (esforço P, valor altíssimo): enumerar as propriedades públicas de cada
   `ReportElement` e falhar se alguma não for coberta por nenhum serializador. Transforma "esqueci um dos 6" de
   bug de produção em erro de build. **Faça isto primeiro** — captura a classe inteira de bug por uma fração do
   custo da refatoração.
2. Estender a cobertura do registry ao caminho RDL onde a semântica for direta.

---

### 13. Backend GDI não implementa gradiente ✅

`SkiaPrimitiveRenderer.cs:301-331` implementa gradiente linear (4 direções) e radial. O equivalente GDI usa só
cor sólida — **re-verificado: a palavra "Gradient" tem ZERO ocorrências em todo o projeto
`src/Reporting.Rendering.Gdi`**.

Um relatório com `BackgroundGradient` renderiza corretamente em PDF/SVG/imagem e cai silenciosamente para cor
sólida **na impressão via spooler do Windows**, sem nenhum aviso. Como gradientes foram entregues em #179/#180 e
o próprio doc de gaps registra "GDI = follow-up", é um follow-up conhecido e não fechado.

**Ação.** Implementar via `LinearGradientBrush`/`PathGradientBrush` do GDI+ (suporte nativo, esforço baixo).
Alternativa mínima: emitir warning de degradação.

---

### 14. `IReportExporter.Export` síncrono sem `CancellationToken` ✅

`src/Reporting.Output.Pdf/IReportExporter.cs:20` — `void Export(RenderedReport report, Stream output)`
(re-verificado), implementado por 10+ exporters. Todo o resto do pipeline (`PaginateAsync`, `ReadAsync`) é async
com token, mas a etapa final — potencialmente a mais cara em CPU e I/O — bloqueia a thread e não pode ser
cancelada. Num host ASP.NET Core isso é I/O síncrono no meio de um pipeline assíncrono; um cliente que desiste
do download não libera o servidor.

**Ação.** Adicionar `ExportAsync(RenderedReport, Stream, CancellationToken)` à interface com implementação
default delegando à síncrona (evita quebrar implementadores externos), migrando os exporters internos
progressivamente. Fazer **antes do 1.0** — depois vira breaking change.

---

### 15. `MaterializeAsync` sem streaming 🔍

`ReportPaginator.cs:71` acumula cada fonte de dados inteira em `List<…>` antes de paginar. É necessário para
sub-detail bands e para o 2-pass de `Page.Total`, mas significa que **todo** relatório materializa o dataset
completo em memória — mesmo os que não precisam de nenhum dos dois.

Há tensão com a documentação: `docs/data-sources.md:104-109` promete streaming via `IAsyncEnumerable` sem
materializar tudo. O streaming existe na *leitura*; o paginador o desfaz.

**Ação.** Detectar o caso simples (sem sub-detail, sem `Page.Total`, sem agregado global) e paginar em streaming
real. Nos demais, manter a materialização e **documentar honestamente** a característica de memória.

> **MEDIDO — o escopo deste item mudou.** Com o item 20 concluído, os benchmarks mostram alocação
> **perfeitamente linear**: ~9,6 KB por linha (9,6 MB @ 1k · 96,7 MB @ 10k · 963 MB @ 100k), sem O(n²)
> escondido. Mas o grosso **não é a entrada materializada** e sim a **saída** — cada linha vira primitivos que
> o `RenderedReport` retém até o export. Streamar só a leitura reduz o pico e **não** derruba os 9,6 KB/linha.
> Um ganho real exigiria streamar também a saída (emitir páginas conforme fecham, em vez de acumular o
> `RenderedReport` inteiro), o que é escopo maior do que este item descrevia — provavelmente um item próprio.
> Ver [docs/benchmarks.md](docs/benchmarks.md).
>
> **Já entregue (PR #225):** cada fonte passou a ser lida **uma vez** por paginação. Antes o primário era lido
> duas vezes (quatro em master-detail), ou seja, a mesma query SQL executada em dobro.

---

### 16. Excel/DOCX descartam conteúdo sem avisar ✅

`ExcelExporter` percorre apenas `DrawTextPrimitive` (re-verificado: `ExcelExporter.cs:13,52` —
`LayoutPrimitiveGrid.Build(report)` sobre texto): gráficos, imagens, barcodes, gauges, mapas e KPIs
**desaparecem** do `.xlsx`, e as cores de célula são hardcoded em vez de refletir o `Style` do relatório. DOCX
rasteriza gráficos (limitação arquitetural aceita e documentada em `docs/rdl-coverage.md:69-71`).

Que Excel seja orientado a dados é legítimo — o problema é a perda ser **silenciosa**: nem `ExcelExportOptions`
documenta, nem nenhum warning é emitido.

**Ação.** Emitir warnings de degradação na exportação (o motor já tem canal de warnings no import RDL —
reutilizar o padrão) e documentar a matriz de fidelidade por exporter. Opcionalmente, rasterizar gráficos como
imagem embutida no Excel, como o DOCX já faz.

---

### 17. `ElementViewModel`: God object 🔍

~1.400 linhas, ~76 propriedades observáveis, cobrindo simultaneamente os ~18 tipos de elemento, com
`ToElement()`/`FromElement()` que precisam conhecer todos. Mistura estado de apresentação com mapeamento de
domínio, e é o ponto onde bugs sutis de round-trip nascem (ver item 21).

**Ação.** Refatoração incremental por faceta (TextFacet, ChartFacet, TablixFacet) em vez de big-bang — a suíte
de ~299 testes do Designer, forte em round-trip, dá cobertura para refatorar com segurança. Baixa urgência,
alto valor de longo prazo.

---

### 18. `catch` nus em 5 pontos de `Layout` ✅

Não há `catch {}` totalmente vazio (bom sinal), e vários pontos **já foram estreitados** no PR #199
(`ReportPaginator.cs:254,472` filtram `FormatException or OverflowException or InvalidCastException`). Restam 5
pontos nus, re-verificados:

- `Internal/TablixRenderer.cs:708`, `:724`, `:765` — `catch` sem filtro em `EvalDouble`/`FormatNumber`,
  retornando `0` silenciosamente.
- `ReportPaginator.cs:691` — `catch { value = null; }`, **com** comentário justificando ("resilient by design:
  a bad calculated-field expression nulls the field"). Este é deliberado; no máximo merece filtro de tipo.
- `ReportPaginator.cs:1152` — `catch { value = null; }` **sem** comentário nem filtro.

O padrão correto já domina o repo (`KpiRenderer`, `ChartRenderer`, `MapRenderer`, `PropertyPathBinder.cs:229`,
`BandRenderer.cs:536` filtram por tipo). O risco: um `NullReferenceException` por bug real vira "célula com 0",
indistinguível de dado inválido.

> **Não confundir com os catches de `DesignerDataConnect`** — aqueles são corretos por design (ver aviso no topo).

**Ação.** Restringir aos tipos esperados (`FormatException`, `InvalidCastException`, `OverflowException`,
`ExpressionEvaluationException`), alinhando ao padrão dominante. Esforço baixo, ganho real de diagnosticabilidade.

---

### 19. `RdlImporter`/`RdlWriter` monolíticos 🔍

1.713 e 1.376 linhas respectivamente, cada um misturando parsing genérico de XML com regras específicas por tipo
de elemento (Tablix, Chart, Gauge, CustomReportItem, Map). A responsabilidade é coesa ("importar/exportar RDL"),
mas o tamanho torna a navegação e a revisão caras — e é o arquivo mais tocado do repositório nos PRs recentes
(#212 mexeu nos dois).

**Ação.** Split por tipo de elemento (`RdlTablixImporter`, `RdlChartImporter`…), espelhando o particionamento que
Repx/RepJson já têm via registry. Combina naturalmente com o item 12.

---

### 20. Sem benchmarks ✅

**Re-verificado: nenhum projeto de benchmark existe** (glob por `*Benchmark*/*.csproj` sem match), nenhuma
referência a BenchmarkDotNet. Para um motor de layout/paginação que roda sob carga em servidor, não há barreira
automática contra regressão de performance. `LargeReportPaginationTests` cobre integridade de dados a 600 linhas
— é teste de correção, não de performance, e 600 linhas não expõe complexidade O(n²).

**Ação.** Projeto de benchmark cobrindo paginação (1k/10k/100k linhas), avaliação de expressões e export
PDF/Excel. Rodar sob demanda; publicar tendência. Pré-requisito para atacar o item 15 com dado em vez de
intuição.

**Consequência colhida.** Com os benchmarks no lugar, a asserção de wall-clock que morava em
`SamplesIntegrationTests.Sample01_with_10k_rows_finishes_under_2_seconds` foi removida. Ela rodava **só fora do
CI** — dispensava o runner compartilhado e cobrava o orçamento de 2 s justamente da máquina do dev, que costuma
estar no meio de um build; verificado que falhava na `main` limpa neste workstation enquanto o CI passava verde.
Um `Stopwatch` em volta de uma execução fria também não controla JIT nem warmup. O teste manteve a asserção de
correção (10k linhas ⇒ múltiplas páginas) e o orçamento de tempo passou a viver em `PaginationBenchmarks`.

---

### 21. Perda da semântica "inherit" no Designer 🔍

`ElementViewModel.cs:1293,1300` — `ForeColor = element.Style.ForeColor ?? Color.Black` e
`FontFamily = element.Style.Font?.Family ?? "Arial"` materializam valores explícitos onde o modelo tinha `null`
(= "herdar do estilo nomeado/tema"). Um elemento sem cor explícita, ao ser aberto e salvo no Designer, passa a
gravar `Black`/`Arial` como literal.

Hoje o impacto visual é nulo (os defaults coincidem), mas quebra a herança de **named styles** — feature
entregue em #181-183/#188-189. Um relatório editado no Designer deixa de responder à troca de tema.

**Ação.** Representar "inherit" como estado distinto no VM (nullable + placeholder na UI), não como valor
materializado. Conhecido e pré-existente; a chegada dos named styles elevou sua relevância.

---

### 21a. `Style.Border` só é desenhada em Rectangle e Ellipse ✅

Achado pela suíte de golden files (item 10) na primeira execução, e fixado em
`tests/Reporting.Golden.Tests/RenderGapCharacterizationTests.cs`.

`PenStyle.FromBorderSide` tem **um único chamador** em todo o `src/`: `ResolveBorderPen`
(`Reporting.Layout/Internal/BandRenderer.cs:698`), invocado apenas nos casos `RectangleElement` (linha 320) e
`EllipseElement` (linha 352). A passagem genérica de elemento emite um `DrawRectanglePrimitive` de **fundo**
quando há preenchimento (linha 248), mas nunca um com traço.

Consequência: `.Border(...)` num `Label`/`TextBox`/`Image` **não vira primitiva alguma** — nenhum backend pode
desenhá-la. Um textbox com borda é o caso mais banal de relatório estilo SSRS. O Tablix também ignora
`Style.Border`: desenha uma grade de cor fixa (`TablixRenderer.cs:24`, `#CBD5E1` a 0.5pt), não configurável.

**Ação.** Emitir o retângulo de borda na passagem genérica (as 4 faces, não só `Top` como o fallback atual) e
tornar a grade do Tablix dirigida por estilo. **Pronto quando** o golden `Estilos` mostrar a primitiva de borda
e a caracterização correspondente falhar (então é apagada).

---

### 21b. `CornerRadius` é descartado em retângulo sem filhos ✅

Mesmo achado, mesma caracterização. `DrawRectanglePrimitive` não tem campo de raio. O valor sobrevive só como
`ClipCornerRadius`, que o `BandRenderer` carimba nos **filhos** do retângulo para recortar overflow
(`BandRenderer.cs:342`). Um retângulo usado como forma arredondada não tem filhos, então o raio não chega a
lugar nenhum — o SVG exportado é um `<rect>` sem `rx`, como registra `Goldens/Formas.svg.verified.txt`. Ser
honrado para containers é justamente por que a lacuna passou despercebida.

**Ação.** Campo de raio no `DrawRectanglePrimitive` + honrar em Skia, GDI, PDF, SVG e no overlay HTML.

---

### 21c. `ReportBuilderRoot.Culture(CultureInfo.InvariantCulture)` lança 🔍

`Reporting.CodeFirst/ReportBuilder.cs:145` repassa `culture.Name` para `Language(string)`, que aplica
`ArgumentException.ThrowIfNullOrWhiteSpace` — e o nome da cultura invariante é `""`. Uma chamada pública,
documentada e perfeitamente razoável (é a escolha mais reprodutível para um relatório) lança
`ArgumentException` com uma mensagem que cita um parâmetro que quem chamou nunca informou.

Guardar `""` **não** resolve: `ReportPaginator.TryGetCulture` (linha 231) trata string vazia como "nenhuma
cultura informada" e cai na cultura ambiente — silenciosamente o oposto do pedido. Suporte real exige um
sentinela entendido pelos dois lados.

**Ação.** Sentinela explícito (`Metadata["Language"] = "Invariant"`) reconhecido em `TryGetCulture`, ou —
se a decisão for não suportar — lançar em `Culture()` com mensagem que diga isso. Contornado nos goldens
fixando `en-US`.

---

## P3 — Features essenciais de produto

### 22. PDF: senha/criptografia e PDF/A ✅

`PdfExportOptions` expõe **apenas** metadados — re-verificado, o record tem `Title`, `Author`, `Subject`,
`Keywords`, `Producer`, `Creator`, `CreationDate`, `CompressImages` e nada mais. **Sem** senha, criptografia,
assinatura digital, PDF/A ou formulários.

**Por que é essencial e não opcional:** o README posiciona o produto fortemente em NFC-e/DANFE e documentos
fiscais brasileiros. Arquivamento fiscal costuma exigir **PDF/A**, e distribuição de holerites/faturas costuma
exigir **senha**. É a lacuna com maior descasamento entre o mercado-alvo declarado e a capacidade entregue.

**Paridade:** Crystal, SSRS, Jasper, XtraReports e Stimulsoft têm senha/criptografia; Jasper e XtraReports têm
PDF/A; XtraReports e Stimulsoft têm assinatura digital.

**Ação.** Priorizar senha/criptografia (mais pedido, menor esforço) e depois PDF/A-3b. **O item 23 (embedding de
fontes) é pré-requisito técnico do PDF/A** — não dá para conformar sem fontes embutidas. Avaliar se o backend
Skia atende ou se é preciso pós-processamento com uma lib dedicada.

---

### 23. Embedding/subsetting de fontes no PDF ❌ — **FALSO POSITIVO, não "corrija"**

> **O PDF já embute E faz subsetting de fontes.** Verificado inspecionando um export real, não o código:
>
> ```
> /FontFile2: 2                              ← programas TrueType EMBUTIDOS
> /Type0: 2                                  ← fontes compostas (CID), o caminho Unicode do Skia
> glifo 'R' fonte='AAAAAA+Arial-BoldMT'      ← o prefixo AAAAAA+ é o marcador padrão de SUBSET
> ```
>
> O backend PDF do Skia faz as duas coisas por padrão. O item nasceu de inferir ausência de comportamento a
> partir de ausência de identificador (`EmbedFont`/`FontSubset` não aparecem no `src/`) — o mesmo erro dos
> outros três falsos positivos deste roadmap. Expor `EmbedFonts` seria adicionar um botão para algo que já
> funciona e cujo único valor sensato é "ligado" (o PDF/A exige sempre).
>
> **O que faltava de verdade — e foi entregue:** nada verificava isso. Como é um *default* do Skia e não uma
> decisão deste código, uma atualização da biblioteca ou outro caminho de criação do `SKDocument` poderia
> desligar em silêncio, e a falha é invisível na máquina que gerou o arquivo — o PDF só quebra num leitor sem
> a fonte. `FontEmbeddingTests` trava as três garantias: programa embutido para **cada** fonte usada, nome com
> prefixo de subset, e texto ainda extraível depois do remapeamento de glifos.

**O texto original do item, preservado para contexto:**

**O problema (INCORRETO).** Não há nenhum controle de embutimento de fonte: `EmbedFont`/`FontSubset` têm
zero ocorrências em `src/`. O PDF depende de a fonte existir na máquina que abre o arquivo.

**Por que importa.** Um PDF gerado com "Arial" ou uma fonte corporativa abre com métricas diferentes — ou
substituição total — em máquina Linux, celular ou leitor sem a fonte. Para documento fiscal ou contrato, isso é
descaracterização do documento. É também **pré-requisito obrigatório do PDF/A** (item 22): a norma exige todas as
fontes embutidas.

**Paridade:** todas as engines comerciais embutem fontes por padrão; o subsetting (embutir só os glifos usados)
é o que mantém o arquivo pequeno.

**Ação (SUPERADA).** Expor `EmbedFonts` (e idealmente `SubsetFonts`) em `PdfExportOptions`. Restou apenas a
segunda metade — "guardar com teste que verifique a presença do dicionário `/FontFile2`" —, que é o que foi
feito.

**Pronto quando:** um PDF gerado abre com métricas idênticas numa máquina sem a fonte instalada. **Já era o
caso**; agora há teste que impede isso de regredir.

**Consequência para o item 22 (PDF/A):** o pré-requisito de fontes embutidas está satisfeito e travado, então o
PDF/A parte de uma base menor do que o item supunha.

---

### 24. Cache de relatório renderizado ✅

**Re-verificado: nenhuma referência a `HybridCache` ou `IMemoryCache` em `src/`.** Cada `PaginateAsync()`
recomputa do zero, inclusive para relatórios idênticos com os mesmos parâmetros. Em cenário web com relatórios
pesados e concorrência, é o ganho de performance mais direto disponível.

**Ação.** Cache de `RenderedReport` com chave (definição + parâmetros + versão dos dados), sobre `HybridCache`
do .NET 10.

> **Pré-condição agora satisfeita:** este item dependia do antigo P0 #1 (estado compartilhado no paginador), que
> foi resolvido. Cachear sobre um paginador com vazamento entre requests multiplicaria o problema; agora é
> seguro atacar.

---

### 25. Viewer sem document map, busca ou miniaturas 🔍

`ReportViewer.razor` tem 83 linhas: navegação de página, zoom, export PDF/XLSX, imprimir. **Não tem** busca,
document map/bookmarks nem miniaturas. Renderiza páginas como imagens estáticas (`<img>`), o que também impede
seleção de texto.

O detalhe frustrante: o modelo **já suporta** `Bookmark`, `DocumentMapLabel` e `Action.BookmarkLink`, e o
importador RDL os lê. O back-end está pronto; falta consumir no viewer.

**Paridade:** document map/árvore de navegação é padrão em SSRS, Crystal, Jasper e XtraReports; miniaturas e
busca de texto também.

**Ação.** Document map primeiro (maior valor, dado o suporte pronto no modelo), depois miniaturas. Busca exige
repensar o render por imagem — avaliar camada de texto sobreposta, como o overlay de a11y que o exporter HTML já
faz.

---

### 26. Designer sem wizard de criação ✅

**Re-verificado: zero ocorrências de "Wizard" em `src/`.** Todo relatório novo começa 100% em branco. SSRS,
Crystal e DevExpress guiam o usuário (fonte → campos → layout → estilo) — é a maior barreira de adoção para
usuário não-técnico, justamente o público de um designer visual.

**Ação.** Wizard de 4 passos reaproveitando peças que já existem e são sólidas (`DataSourceEditorDialog`,
`SchemaExplorerTree`, `ParametersList`). Esforço moderado, impacto alto em onboarding.

---

### 27. Oracle sem pacote de 1ª classe ✅

`docs/user-guide.md:162` e `presentation.html:228` citam Oracle como suportado, mas **re-verificado por glob:
`src/Reporting.DataSources.Oracle/` não existe**. Funciona só via `AdoNetDataSource` genérico com
`DbProviderFactory` fornecido pelo host, sem a conveniência que SqlServer/PostgreSql/MySql/Sqlite têm.

É discrepância entre documentação e entrega.

**Ação.** Criar o pacote (baixo esforço, os 4 irmãos são o template) ou corrigir a documentação. Preferir criar —
Oracle é relevante no mercado corporativo brasileiro.

---

### 28. `WebServiceDataSource` sem REST paginado 🔍

`WebServiceDataSource.cs:9-11,49-83` faz **uma única** requisição HTTP. APIs paginadas (cursor, `next`,
offset/limit) exigem wrapper customizado pelo host. Como REST paginado é hoje o padrão dominante, é uma
limitação que atinge a maioria dos casos reais.

**Ação.** Estratégias de paginação configuráveis (cursor via campo do payload, offset/limit, header `Link`).

---

### 29. Sample de `Hosting.AspNetCore` inexistente ✅

**Re-verificado: `AddReporting` não aparece em nenhum arquivo de `samples/`** — só em `src/`, `tests/`, `docs/`
e no `README.md:297-312`, que o anuncia como quickstart principal. O sample `BlazorServer` monta os serviços por
DI manual, contornando o pacote que o README recomenda.

Ou seja: **o caminho canônico de produção nunca é exercitado**, nem em sample nem em CI. Isso conectava
diretamente ao antigo P0 #1 — um sample real teria tornado o bug de concorrência muito mais visível.

**Ação.** Sample mínimo de API com `AddReporting`, endpoint de geração de PDF, e teste de integração com
`WebApplicationFactory`.

---

### 30. UI do Designer sem i18n ✅

**Re-verificado por glob: zero arquivos `.resx` no repositório**; zero `IStringLocalizer`. Todas as strings do
Designer são hardcoded em português nos `.razor` ("Alinhar à esquerda", "Recortar", "Agrupar por…"). Não há como
oferecer a UI em inglês ou espanhol sem fork dos componentes — SSRS, Crystal e DevExpress são localizáveis
nativamente.

**Ação.** Extrair para `.resx` com `IStringLocalizer`. Esforço mecânico mas amplo (31 componentes). **Decisão de
produto:** só vale se houver ambição de mercado fora do Brasil — se não houver, **registre isso explicitamente**
como decisão de escopo, como o projeto já faz bem em outros pontos.

---

### 30a. Migração para SkiaSharp 4 ✅ — **FEITO**

Adiado deliberadamente da varredura de dependências (que atualizou 19 pacotes). O SkiaSharp 4 depreciou a API
mutável de `SKPath` em favor de `SKPathBuilder`, o que dá **10 erros CS0618 em 3 arquivos** —
`SkiaPathBuilder.cs` (12 ocorrências), `SkiaPrimitiveRenderer.cs` (6) e `SkiaRenderingContext.cs` (2). Como as
bibliotecas empacotáveis usam `TreatWarningsAsErrors`, obsolescência vira erro de build.

Em linhas é contido, mas é o **núcleo de renderização**, e a major traz um Skia nativo novo que pode deslocar
rasterização e métricas. Numa varredura com 19 outros pacotes, qualquer regressão ficaria inatribuível.

**A favor de fazer agora:** os golden files (#233) cobrem exatamente isso — a camada de emissão SVG passa pelo
Skia, então uma mudança de comportamento aparece no diff em vez de passar batido.

**Ação.** PR próprio: migrar os 3 arquivos para `SKPathBuilder`, rodar os goldens e **ler o diff** antes de
aceitar qualquer mudança neles.

**FEITO.** Foram 10 obsolescências, mais do que o `SKPath` previsto — além dos 6 do path builder,
`SKTypeface.GetGlyph` (cobertura de glifo migrou para `SKFont`), `SKCanvas.DrawText` (agora exige `SKTextAlign`)
e duas sobrecargas de `DrawImage` (agora exigem `SKSamplingOptions`). Em todas escolhi o valor que **preserva o
comportamento**, não o "melhor": `SKTextAlign.Left` porque o cursor já resolve alinhamento antes da chamada, e
`SKSamplingOptions.Default` porque filtragem linear melhoraria imagem reduzida mas mudaria pixel — isso seria
decisão de qualidade, não de migração, e escaparia à revisão deste PR.

**A prova de que nada mudou:** os goldens passaram **sem que nenhum `.verified` fosse tocado**. O diff inicial
foi de 2 arquivos SVG e continha só ruído de float — `187.128` vs `187.12801`, `99.216` vs `99.216003` — na casa
de 10⁻⁵ ponto, cerca de 3 nanômetros.

E ele expôs um defeito no próprio normalizador do golden: dados de path colam o comando ao número seguinte, então
`187.12801L368.496` chega como **um único token**, e a versão anterior só descascava letra no *início*. Esse token
passava sem arredondamento — era o único do arquivo que escapava. Corrigido para varrer números em vez de dividir
por espaço, com `SvgShapeTests` travando os quatro casos, inclusive o de que um movimento real ainda aparece.

O `SKPathBuilder` é `IDisposable` e o `SKPath` agora sai por `Detach()`, então o adaptador passou a ser
descartável — antes não havia o que liberar, porque ele expunha o `SKPath` direto.

---

### 30b. Migração para NCalc 7 ✅ — **NOVO**

Também adiado da varredura. O NCalc 7 mudou a assinatura de `LogicalExpressionFactory.Create` (agora
`(string, LogicalExpressionParserOptions?, CultureInfo?)`) e a resolução de overload do construtor de
`Expression`, dando 4 erros em `ExpressionCompiler.cs:40,47`.

São 2 call sites, mas é o **motor de expressões**: 165 testes e todo o vocabulário SSRS (agregados, posicionais,
lookups, formatação) dependem dele, e uma major pode mudar semântica de avaliação — coerção de tipo, cultura,
comportamento de operador — e não só assinatura. É a segunda vez que o NCalc muda API de forma incompatível
(ver [[ncalc6-and-audit]]).

**Ação.** PR próprio, com a suíte de expressões inteira como rede. Verificar especialmente se
`ExpressionOptions.DecimalAsDefault` e `IgnoreCaseAtBuiltInFunctions` mantêm o mesmo efeito — são o que impede
aritmética virar concatenação e o que faz `SUM` casar com `Sum`.

---

## P4 — Features recomendadas

### 31. Watermark + rotação de texto/elemento ✅ — **NOVO**

**O problema.** Não há marca d'água — **re-verificado: `Watermark` tem zero ocorrências em `src/`**. Pior: não é
possível nem aproximar com um TextBox inclinado, porque **o modelo não tem rotação** (`Rotation`/`Angle` também
com zero ocorrências em `Reporting.Core`).

**Por que importa.** "RASCUNHO", "CONFIDENCIAL", "CÓPIA NÃO CONTROLADA" e logo de fundo são pedidos corriqueiros
em relatório corporativo — e são o caso de uso que mais frequentemente força o usuário a pós-processar o PDF
fora da ferramenta.

**Paridade:** Crystal, Jasper, XtraReports, FastReport e Stimulsoft têm watermark de texto e de imagem, com
rotação e opacidade. É das poucas features "de tabela" que faltam.

**Ação.** Duas peças, úteis separadamente:
1. **Rotação** (`Rotation` em graus no `ReportElement`) — habilita watermark e também rótulos verticais em
   colunas estreitas, comuns em tabelas densas. Toca modelo + 4 serializadores (ver item 12) + renderers.
2. **Watermark** como propriedade de `PageSetup` (texto ou imagem, opacidade, ângulo, "atrás/à frente do
   conteúdo"), renderizada por página.

Entregar nos 3 modos (code-first, low-level, Designer), como manda a convenção do projeto.

---

### 32. Editor de rich text (`TextRuns`) 🔍

O modelo suporta runs com formatação mista e o round-trip os preserva (`ElementViewModel.cs:1321`, comentário
explícito *"no editor yet"*), mas não há UI para criá-los. Negrito em uma única palavra só é possível importando
de fora. Todas as engines concorrentes têm editor de rich text no designer.

---

### 33. Excel: autofilter e uma aba por grupo 🟡 — **PARCIALMENTE FALSO POSITIVO, corrigido**

> **Correção da 2ª revisão.** Este item afirmava que faltava *freeze panes*, baseado num grep por
> `FreezePane`/`AutoFilter`. **O freeze já existe** — sob outro nome: `ExcelExportOptions.FreezeHeader`
> (default **true**), aplicado em `ExcelExporter.cs:57` via `ws.SheetView.FreezeRows(1)`. O grep por nome de
> API do ClosedXML não achou o nome da *opção*. Terceiro falso-positivo desta varredura — confirme sempre o
> comportamento, não só o identificador.

O que **realmente** falta: **autofilter** e **uma aba por grupo**. `AlternateRowColors` (zebra) também já existe.

**Por que importa.** Quem exporta para Excel quase sempre vai *analisar* os dados; o autofilter é um toque de
API no ClosedXML (já é dependência). "Uma aba por grupo" é o passo seguinte, um pouco maior.

**Paridade:** SSRS, Crystal e Jasper fazem autofilter; SSRS e Jasper suportam quebra de aba por grupo.

**Ação.** `ExcelExportOptions` com `AutoFilter` e `SheetPerGroup`.

---

### 34. Barcodes 2D além de QR 🔍

DataMatrix, PDF417 e Aztec. **PDF417 tem uso real em documentos oficiais brasileiros** (CNH, boletos), o que o
torna o mais defensável dos três.

---

### 35. Query builder visual 🔍

Hoje é Monaco com SQL em texto + explorador de schema real (introspecção de SQLite/Postgres/SqlServer/MySQL, com
inserção de tabela/coluna). Funciona bem para quem sabe SQL; falta o construtor visual com detecção de JOIN que
usuários do Graphical Query Designer (SSRS) ou do Crystal esperam.

---

### 36. RowSpan no Tablix + `StaticMember`/`DynamicMember` 🔍

Gaps RDL remanescentes (#7 e #8 em `docs/rdl-compatibility-gaps.md`). O próprio doc os classifica como nicho e
recomenda definir a semântica antes de implementar. O caso comum de `StaticMember` (coluna fixa "Total") já é
coberto por `ColumnSubtotals`. Baixa prioridade — mas é o último gap estrutural conhecido do Tablix.

---

### 37. Site de documentação + troubleshooting 🔍

`docs/` tem ~3.000 linhas bem organizadas, mas é Markdown solto no GitHub: sem site publicado, sem referência de
API navegável, sem `troubleshooting.md`. **Depende do item 7** (XML docs) para a referência de API valer a pena.

---

### 38. Meta-pacote de onboarding 🔍

A granularidade de 39 pacotes é *tecnicamente correta* (isola Npgsql, ClosedXML, SkiaSharp de quem não precisa),
mas o quickstart exige 4 `dotnet add package`. Um meta-pacote `AndersonN.Omni.Report` agregando o caminho comum
reduz atrito sem desfazer a modularidade.

---

### 39. Export de intervalo de páginas ✅ — **NOVO**

Os exporters recebem o `RenderedReport` inteiro (`void Export(RenderedReport, Stream)`), sem nenhum parâmetro de
faixa. Exportar "só as páginas 3–7" exige o chamador montar um `RenderedReport` parcial na mão. `PrintOptions`
já tem faixa de páginas para **impressão** — a assimetria é evidente.

**Paridade:** faixa de páginas é padrão em todos os viewers/exporters comerciais.

**Ação.** `PageRange` nas options de export (ou sobrecarga `Export(report, stream, range)`). Encaixa
naturalmente na migração do item 14 (`ExportAsync`) — mesma assinatura, mesma revisão, sem breaking change extra.

---

### 40. Agendamento e entrega por e-mail 🔍

Ausente por completo (sem scheduler, fila ou envio). É o que separa "biblioteca de relatórios" de "plataforma de
relatórios" tipo SSRS/Jasper Server. Esforço alto; avaliar se é o posicionamento desejado ou se cabe melhor ao
produto que consome a lib.

---

### 41. RTL e `WritingMode` 🔍

`docs/rdl-spec-compliance.md:185,219` registra 🔴 "sempre LTR". Bloqueia mercados árabe/hebraico e texto
vertical. Só faz sentido junto do item 30 — e a parte de "texto vertical" tem interseção com a rotação do item 31.

---

### 42. Tagged PDF / acessibilidade 🔍

O exporter HTML já tem overlay de a11y; o PDF não tem estrutura de tags. Relevante para requisitos de
acessibilidade do setor público (e para licitações).

---

### 43. Exporters RTF / ODT / PPTX 🔍

`docs/comparison.md:110` já reconhece a lacuna frente a concorrentes comerciais. Baixa prioridade: são formatos
de nicho decrescente, e DOCX já cobre o caso "editar no Word".

---

## Sequenciamento sugerido

**Sprint 1 — Destravar o 1.0 (P0 completo).** Itens 1–3. Todos de esforço P, todos com impacto desproporcional na
percepção do produto: hoje um pacote publicado não funciona, o pipeline de release não tem gate e a vitrine do
NuGet está com imagens quebradas. É o melhor retorno por dia de trabalho do roadmap inteiro.

**Sprint 2 — Fechar o cerco de qualidade.** Itens 5, 8, 9, 29 (todos P), depois 4 e 6. Ao final, a promessa
"cross-platform, testado, sem breaking change silencioso" passa a ser verificada, não afirmada.

**Sprint 3 — Débito com maior retorno.** Comece pelo **teste de completude por reflexão do item 12** — é esforço
P e elimina uma classe inteira de bug que já se materializou duas vezes (#212 e o `ToggleItemId`). Depois 13, 18,
21 (todos P) e 10, 14 (M). O 14 é o único com prazo real: depois do 1.0 vira breaking change.

**Sprint 4+ — Produto.** Itens 22 + 23 juntos (senha/PDF-A dependem do embedding de fontes) e 26 primeiro — são
o maior descasamento entre público-alvo declarado e capacidade entregue. Depois 24 (agora desbloqueado), 25, 27,
28.

**Ganhos rápidos fora de ordem.** Os itens 33 (freeze/autofilter no Excel) e 39 (faixa de páginas) são ambos de
esforço P, com valor perceptível imediato, e encaixam em revisões que já estão previstas (16 e 14
respectivamente). Bons candidatos para preencher folga de sprint.

**Antes de tudo — duas decisões de produto que não custam nada e mudam a prioridade de vários itens:**
1. **Item 11** — postura de segurança do Roslyn: documentar como "só para fontes confiáveis" (barato) ou investir
   em isolamento real (caro)?
2. **Item 30** — há ambição de mercado fora do Brasil? Se não, registre a decisão e os itens 30 e 41 saem do
   roadmap em vez de ficarem pendurados.
