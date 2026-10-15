# Evolução após a auditoria — 09/09/2026

> **Evidências locais.** Os caminhos `artifacts/...` citados neste documento ficam na máquina onde a rodada foi
> executada: a pasta está no `.gitignore` e não acompanha o repositório. As evidências versionadas estão em
> `docs/evidencias/`; para reproduzir as demais, use os comandos da seção de reprodução.

Base: `88f7c0cca5cca927dd29fd859c0b5c93888c2dbb`, merge do PR #260. Implementação na branch `codex/remaining-audit-recommendations`. Esta entrega implementa as seis recomendações posteriores a F01–F27. Incorporada à main pelo PR #270.

## Entregas e evidências

| Recomendação | Comportamento implementado | Evidência principal |
|---|---|---|
| Preview por demanda e limites | Documento preparado; PNG produzido somente quando solicitado; cache de até três páginas; teto de 16 milhões de pixels por página; limites de trabalho configuráveis | [PreviewPageCache](../src/Reporting.Designer.Blazor/Services/PreviewPageCache.cs), [ReportDesigner](../src/Reporting.Designer.Blazor/ReportDesigner.razor.cs), [testes do cache](../tests/Reporting.Designer.Blazor.Tests/PreviewPageCacheTests.cs), [limites](../tests/Reporting.Layout.Tests/PaginationLimitsTests.cs) |
| Grupos — F24 | Filtro por instância, ordenação estável dos grupos e KeepTogether baseado na altura do conteúdo completo | [planejamento](../src/Reporting.Layout/ReportPaginator.GroupPlanning.cs), [KeepTogether](../src/Reporting.Layout/ReportPaginator.KeepTogether.cs), [testes de grupos](../tests/Reporting.Layout.Tests/GroupPlanningTests.cs), [testes de paginação de grupos](../tests/Reporting.Layout.Tests/GroupKeepTogetherTests.cs) |
| Documento preparado — F25 | Consulta aleatória de páginas em arquivo temporário limitado; emissão progressiva com contrapressão; descarte em sucesso, falha e interrupção do consumidor | [PreparedReport](../src/Reporting.Layout/PreparedReport.cs), [PrepareAsync](../src/Reporting.Layout/ReportPaginator.Prepared.cs), [StreamPagesAsync](../src/Reporting.Layout/ReportPaginator.Streaming.cs), [regressões](../tests/Reporting.Layout.Tests/PreparedReportTests.cs) |
| Cache de propriedades | FIFO de até 1.024 entradas, incluindo caminhos inválidos; caminhos acima de 512 caracteres não entram no cache | [PropertyPathBinder](../src/Reporting.Layout/Internal/PropertyPathBinder.cs), [BoundedCache compartilhado](../src/Reporting.Core/Common/BoundedCache.cs), [ensaio de 4.000 caminhos inválidos](../tests/Reporting.Layout.Tests/PropertyPathCacheTests.cs) |
| Múltiplos designers e histórico | Estado JS por raiz, seletores locais, descarte dos listeners/observers/timers; bloquear/ocultar com undo/redo; menu contextual cabe na janela | [designer.js](../src/Reporting.Designer.Blazor/wwwroot/js/designer.js), [ElementSnapshotCommand](../src/Reporting.Designer.Blazor/ViewModels/ElementSnapshotCommand.cs), [testes de isolamento](../tests/Reporting.Designer.Blazor.Tests/DesignerIsolationTests.cs), [evidência do navegador](evidencias/evolucao-pos-auditoria-2026-09-09/browser-verification.json) |
| Convenções — F28 e observabilidade | Separação de tipos e code-behind Razor; Activity/Meter para dados, layout, renderização e exportação | [inventário](evidencias/evolucao-pos-auditoria-2026-09-09/conventions-inventory.json), [telemetria](../src/Reporting.Core/Diagnostics/ReportTelemetry.cs), [testes](../tests/Reporting.Layout.Tests/ReportTelemetryTests.cs), [resultados de exportação](../tests/Reporting.Output.Tests/ExportTelemetryTests.cs) |

## Contratos de execução e memória

As APIs existentes de paginação continuam disponíveis. `PaginationRequest.Limits` é opcional: ausência de limites conserva o comportamento anterior da biblioteca. Quando um limite é excedido, `PaginationLimitExceededException` identifica `Budget` e `Limit`.

Os contadores representam **trabalho acumulado da execução**, incluindo subrelatórios, passagem de contagem e medição de KeepTogether. Portanto, `MaxPages = 100` não significa necessariamente permitir um documento final de 100 páginas. O provider pode entregar uma linha adicional para detectar que o orçamento de leitura foi excedido. Cancelamento é cooperativo; estes limites não interrompem à força código do host ou um provider que ignore o token.

O designer adota estes padrões, substituíveis pelo parâmetro `PreviewLimits`:

| Recurso | Limite padrão |
|---|---:|
| Linhas lidas das fontes | 100.000 |
| Linhas de iteração construídas | 200.000 |
| Páginas de trabalho | 2.000 |
| Primitivas acumuladas | 1.000.000 |
| Arquivo temporário por documento preparado | 256 MiB |
| PNGs retidos pelo cache de preview | 3 |
| Raster de uma página de preview, a 96 DPI | 16.000.000 pixels |

O cache não faz prefetch: reutiliza até três imagens já solicitadas e remove uma entrada antes de rasterizar a próxima. O contexto Skia de cada página é descartado imediatamente após a codificação. Quem enumerar o cache e guardar os arrays passa a reter essas referências por sua própria conta.

`PrepareAsync` pagina diretamente para um arquivo temporário de nome aleatório, aberto com exclusividade e apagado no descarte. Em Unix, a criação solicita leitura/escrita apenas para o proprietário; em Windows, aplicam-se as ACLs da pasta escolhida pelo host. A consulta `GetPage` usa índice baseado em zero. O índice de offsets fica em memória; o conteúdo de uma página é lido e desserializado quando solicitado. A serialização usa tipos de primitivas e escalares explicitamente permitidos, preservando `decimal`; o arquivo é interno e temporário, sem contrato de formato persistente.

O limite de armazenamento limita bytes no disco. Uma página é serializada em memória antes dessa verificação. Ele não é um limite rígido de heap. As seis famílias atuais de primitivas têm teste de ida e volta, incluindo texto, imagem e polígono.

`StreamPagesAsync` entrega páginas completas, com capacidade de uma página na fila entre produtor e consumidor. Interromper a enumeração cancela e aguarda o produtor. Pode haver também a página atual do consumidor e a página em produção. A API exige host com threads; runtimes de navegador são rejeitados explicitamente para evitar bloquear um consumidor de uma única thread. Para esses hosts, permanecem `PaginateAsync` e `PrepareAsync`; não foi validada a responsividade de WebAssembly nesta rodada.

**As fontes de dados ainda são materializadas.** A emissão de páginas começa depois dessa leitura; relatórios com total de páginas também executam uma passagem inicial de contagem, cujas páginas são liberadas à medida que são concluídas. Ordenação, agregações e subrelatórios continuam exigindo buffers de dados. Exportação e impressão pelo designer materializam um `RenderedReport` ao serem solicitadas, porque os contratos atuais desses consumidores recebem o documento completo.

Exemplo de configuração, considerando `definition` e `registry` fornecidos pelo host:

```csharp
var request = new PaginationRequest
{
    Definition = definition,
    DataSources = registry,
    Limits = new PaginationLimits
    {
        MaxSourceRows = 100_000,
        MaxIterationRows = 200_000,
        MaxPages = 2_000,
        MaxPrimitives = 1_000_000
    }
};

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var document = await new ReportPaginator().PrepareAsync(
    request,
    new PreparedReportOptions { MaxStorageBytes = 256L * 1024 * 1024 },
    cancellation.Token);

if (document.PageCount > 0)
{
    RenderedPage firstPage = document.GetPage(0, cancellation.Token);
    // O consumidor renderiza ou exporta esta página.
}
```

Para consumo sequencial em um host com threads:

```csharp
await foreach (var page in new ReportPaginator().StreamPagesAsync(request, cancellation.Token))
{
    await consumePage(page, cancellation.Token);
}
```

## Semântica dos grupos

O planejamento ocorre após os filtros/ordenações da fonte primária e do detalhe. Cada sequência contígua de chaves iguais constitui uma instância de grupo; instâncias não contíguas com a mesma chave continuam distintas.

O filtro do grupo vê sua instância completa, inclusive agregações de grupo. O planejamento calcula as chaves de ordenação, processa os filhos e mantém a ordem original em empates. A ordenação de grupos não reorganiza as linhas dentro deles. Identificadores internos preservam instâncias distintas mesmo quando a ordenação aproxima grupos com chaves iguais.

Filtros e ordenações do pai são avaliados antes dos filtros dos filhos. Durante esse planejamento, o escopo de relatório corresponde às linhas disponíveis após os filtros anteriores ao agrupamento. Após o planejamento, os totais de saída são preparados novamente com as linhas retidas. Essa ordem evita uma dependência circular entre o filtro de grupo e seu próprio total final.

KeepTogether mede o fragmento completo com o mesmo renderer/measurer, incluindo grupos filhos. Quando o grupo cabe em uma coluna nova e não cabe no espaço restante, avança antes de emiti-lo. Se excede a área disponível de uma coluna, permite divisão e mantém o progresso normal. Quebras explícitas continuam tendo precedência. O planejamento usa uma cópia do estado das expressões para preservar variáveis e históricos da execução principal, e interrompe a medição ao ultrapassar a altura útil.

A medição adicional consome orçamento e processamento. Expressões ou medidores do host com efeitos colaterais ou resultados não determinísticos podem divergir entre medição e emissão. Os testes cobrem filtro por agregado, estabilidade de ordenação, preservação de instâncias, grupos aninhados, cabeçalhos repetidos, totais e grupos maiores que uma página. O diagnóstico ORL024 foi removido para estas propriedades agora implementadas.

## Isolamento e organização

O módulo JS mantém uma instância por elemento raiz em `WeakMap`. Listeners de início de interação pertencem à raiz; continuação de arraste pode acompanhar o documento até o gesto terminar. Listeners e observers dos elementos removidos são encerrados, assim como os recursos da raiz descartada. Coordenação de menus deixou de ser estática. Seletores de canvas e abertura de arquivo usam atributos locais, e parâmetros de formulários recebem IDs únicos.

Bloquear e ocultar passam pelo histórico. O snapshot inclui o estado de bloqueio do view model, que não pertence ao elemento serializado do relatório. Um teste no navegador também revelou menu parcialmente fora da janela de 720 px: o menu agora ajusta sua posição e permite rolagem quando necessário.

No navegador foram observados: seleção e tema independentes, bloqueio por clique, undo/redo, arraste impedido durante bloqueio e permitido após undo, 20 ciclos de montagem/descarte preservando o segundo designer, arraste na instância restante e preview de uma página com imagem carregada em 794 × 1.123 pixels. Os erros de reconexão gerados ao reiniciar o host de diagnóstico foram separados dos cenários finais. Não houve erro no log do servidor durante a execução final.

O inventário sintático cobre **39 projetos, 480 arquivos C# e 37 arquivos Razor**, excluindo `bin` e `obj`. Não encontrou arquivos com duas implementações independentes de tipos nem blocos `@code`/`@functions` nos Razor. Há 52 declarações parciais que envolvem tipos aninhados: são necessárias para conservar seus nomes e acesso existentes; não representam uma segunda implementação no arquivo. A separação foi estrutural e preservou namespaces e contratos existentes. A compilação Android também conferiu o ramo condicional real, além do stub compilado na solução comum.

## Observabilidade

O nome da fonte de Activity e do Meter é `OmniReport`. O host opta por coletá-los com listeners .NET ou a integração de observabilidade de sua preferência.

| Instrumento | Conteúdo |
|---|---|
| Activities `report.data`, `report.layout`, `report.render`, `report.export` | Duração e resultado de cada operação instrumentada |
| `omnireport.phase.duration` | Histograma em segundos, com `report.phase` e `report.outcome` |
| `omnireport.rows.read` | Linhas efetivamente consumidas dos providers |
| `omnireport.pages.produced` | Páginas de trabalho, incluindo contagem e subrelatórios |

Os resultados são `success`, `error` ou `cancelled`. A fase de renderização cobre a reprodução de páginas e a rasterização de imagens; os exportadores instrumentam sua operação completa, incluindo seus caminhos de desenho internos. A exportação PNG por página também publica a fase de exportação. Nomes de relatórios, consultas, campos, parâmetros e valores não são adicionados como tags.

## Correção adicional encontrada na validação do PDF

A comparação síncrono/assíncrono revelou uma falha funcional intermitente. Antes da correção, um PDF de dez páginas retornou **3.090 caracteres nulos** na extração independente; o outro retornou os textos esperados. A diferença estava no objeto `ToUnicode`, cujos 15 glifos usados apontavam para Unicode zero. Os dois PDFs foram preservados em `artifacts/evolucao-pos-auditoria-2026-09-09/PdfComparison/before-sync.pdf` e `before-async.pdf`. Evidências: [extração anterior](evidencias/evolucao-pos-auditoria-2026-09-09/pdf-before-extraction.json) e [mapa anterior](evidencias/evolucao-pos-auditoria-2026-09-09/pdf-before-ToUnicode.txt).

O [player de texto](../src/Reporting.Rendering.Skia/SkiaPrimitiveRenderer.UnicodeText.cs) passou a fornecer UTF-8 e offsets de clusters junto dos glifos, preservando o posicionamento simples anterior. A implementação de [SKTextBlob.Create na versão 4.151.1](https://github.com/mono/SkiaSharp/blob/v4.151.1/binding/SkiaSharp/SKTextBlob.cs) usada pelo caminho anterior aloca um run posicionado de glifos, sem o texto associado. Não foi isolada a causa interna da biblioteca nativa que fazia o mapeamento de fallback variar entre execuções.

Depois da correção, ambos os arquivos recuperaram os 3.090 caracteres, sem caracteres nulos. O mapa ainda pode usar representações equivalentes — entradas individuais ou intervalos — produzindo bytes diferentes. Por isso, a comparação PDF foi transferida para um teste com PdfPig que confere texto esperado, fontes, posições e dimensões de todas as páginas; PNG e TIFF mantêm igualdade binária. Um segundo teste valida 80 PDFs em oito execuções concorrentes, incluindo acentos e símbolos. [Regressões](../tests/Reporting.Output.Pdf.Tests/ConcurrentTextExportTests.cs), [extração após correção](evidencias/evolucao-pos-auditoria-2026-09-09/pdf-explicit-extraction.json) e [mapa explícito](evidencias/evolucao-pos-auditoria-2026-09-09/pdf-explicit-ToUnicode.txt).

## Medições realizadas

As medições abaixo foram repetidas após essa mudança no player.

Ensaio local em Windows `10.0.26200`, runtime `.NET 10.0.12`, 96 DPI. São 90 observações: 1/10/100 páginas × concorrência 1/5 × cinco modos × três repetições. Cada página mede 210 × 100 mm e contém 30 textos com valores decimais; cada sessão recebe parâmetro distinto. O harness verifica quantidade efetiva de páginas, valores e limite do cache. Há aquecimento de um relatório por modo.

`preview-all` reproduz o fluxo anterior de rasterizar/codificar todas as páginas antes de mostrar a primeira. `preview-window` prepara o documento, mostra a primeira imagem e depois percorre todas as páginas, retendo no máximo três PNGs. Essa comparação inclui preparação, rasterização e codificação. Não é uma comparação entre duas versões completas do produto.

Resultados de **100 páginas × cinco sessões**, mediana de três execuções. “Primeira página” é a mediana das médias das cinco sessões; tempo total corresponde ao lote:

| Modo | Primeira página (ms) | Lote completo (ms) | Alocação acumulada (MiB) | Pico privado amostrado (MiB) | Pico gerenciado amostrado (MiB) |
|---|---:|---:|---:|---:|---:|
| Paginação em memória | 47,48 | 49,11 | 63,81 | 44,50 | 10,62 |
| Documento preparado | 57,14 | 59,31 | 85,92 | 47,61 | 7,32 |
| Páginas progressivas | 8,13 | 26,14 | 63,98 | 46,73 | 4,30 |
| Preview de todas as páginas | 1.598,25 | 1.645,03 | 82,77 | 66,10 | 19,39 |
| Preview por demanda, percorrendo todas | 78,23 | 1.705,71 | 133,66 | 59,00 | 15,13 |

O ganho observado no preview foi a disponibilidade inicial: 1.598,25 ms para 78,23 ms neste cenário. O cache passou de 100 para no máximo três PNGs por sessão. Percorrer todas as páginas alocou mais memória acumulada e levou cerca de 4% mais tempo no lote. O documento preparado usou aproximadamente 2,26 MiB de armazenamento por relatório deste cenário.

Após GC forçado, a mediana de heap gerenciado do processo foi 2,27 MiB no fluxo de preview completo e 2,28 MiB no fluxo por demanda; a memória privada ficou em aproximadamente 38,03 e 37,98 MiB. Esses valores incluem caches compartilhados, o próprio harness e resultados acumulados das rodadas. Não demonstram vazamento nem ausência dele.

Limites de interpretação:

- São medições exploratórias, com três repetições e aquecimento curto, em máquina compartilhada. Não constituem benchmark estatístico de capacidade nem justificam extrapolar linearmente entre 1, 10 e 100 páginas.
- Picos foram amostrados a cada 20 ms; casos de duração próxima ou inferior ao intervalo podem ter picos não capturados. Não interpretar a tabela de paginação simples como prova de redução de pico.
- Alocação acumulada inclui o sampler; LOH corresponde à última coleta observada, e não ao pico instantâneo. GC Gen0/1/2, working set, GDI/USER e dados por sessão estão no arquivo bruto.
- Os mesmos processos e caches são reutilizados entre os casos. Memória privada após GC pode permanecer reservada pelo runtime ou alocador nativo.
- Estes números cobrem textos e paginação simples. Grupos/subrelatórios e imagens possuem validação funcional, mas não benchmark representativo novo nesta rodada.

Dados: [90 medições brutas](evidencias/evolucao-pos-auditoria-2026-09-09/measurements.json) e [consolidação](evidencias/evolucao-pos-auditoria-2026-09-09/measurement-summary.json). [Harness reproduzível](../tools/diagnostics/evolucao-pos-auditoria/Measurements/Program.cs).

## Validação e reprodução

- Build Release da solução: zero avisos e zero erros.
- Suítes locais: **1.950 aprovados, zero falhas e 30 ignorados**, em 25 suítes. São 27 casos adicionais em relação aos 1.923 aprovados da base desta rodada.
- Os 30 ignorados dependem de bancos reais via Docker indisponível nesta máquina. Isso não invalida a evidência anterior de CI do PR #260, mas ela não foi contada como execução atual.
- Build Android real com `OMNIREPORT_BUILD_ANDROID=true`: zero avisos e zero erros. Não houve execução em dispositivo nem impressão física.
- Navegador: host de diagnóstico Blazor Server e eventos reais, conforme registro acima. Não houve execução WebAssembly.
- Nenhuma atualização de golden ou pacote foi necessária. Não houve CI, publicação ou merge destas alterações nesta rodada.

Resumo por suíte: [test-summary.json](evidencias/evolucao-pos-auditoria-2026-09-09/test-summary.json). Logs, TRX, host de navegador e ferramentas auxiliares ficam em `artifacts/evolucao-pos-auditoria-2026-09-09`.

Na raiz do repositório, em PowerShell:

```powershell
dotnet build OmniReport.slnx -c Release -m:4
dotnet test OmniReport.slnx -c Release --no-build -m:4 --logger trx --results-directory artifacts/evolucao-pos-auditoria-2026-09-09/TestResultsRepro
dotnet build src/Reporting.Printing.Android -c Release -p:OMNIREPORT_BUILD_ANDROID=true
dotnet run --project tools/diagnostics/evolucao-pos-auditoria/Measurements -c Release -- artifacts/evolucao-pos-auditoria-2026-09-09/measurements-repro.json
```

O harness usa GDI/USER de Windows. Execute fora das builds e testes para reduzir interferência. O diretório de saída deve existir. Para o navegador, execute o projeto local `artifacts/evolucao-pos-auditoria-2026-09-09/DesignerHost` e repita os passos registrados em `browser-verification.json`.

## Verificações ainda abertas

| Verificação | Situação e requisito para concluir |
|---|---|
| Retenção prolongada de cache e designer | O limite de entradas e o descarte funcional foram verificados. Ainda faltam snapshots de heap/raízes, ensaio prolongado e comparação após o prazo real de retenção de circuitos do host. |
| Produção sob carga variada | Repetir com relatórios reais, imagens, grupos/subrelatórios, mais aquecimento, ordem de casos controlada e amostragem/ETW apropriados. Não transformar o ganho de primeira página em promessa de throughput. |
| Streaming integral das fontes | Não implementado: ordenação, agregados e relações ainda materializam dados. Uma próxima API de provider/paginação incremental precisa especificar quais relatórios dispensam esses buffers. |
| Exportação integralmente progressiva pelo designer | Os exportadores atuais recebem documento completo. Exige evolução dos contratos de exportação/impressão para consumir páginas preparadas diretamente. |
| Linux e permissões Unix em execução | A configuração restritiva do arquivo existe no código; falta executar a nova rodada de testes e conferir permissões e descarte no ambiente Linux. |
| WebAssembly | A API progressiva rejeita browser runtimes explicitamente. Preview preparado e responsividade de cancelamento nesse host não foram validados. |
| Dispositivo Android e impressão física | Compilação confirmada; execução e hardware continuam exigindo ambiente e equipamento específicos. |

As seis recomendações receberam implementação e regressões. A validação de retenção prolongada e as plataformas não executadas permanecem explicitamente abertas; crescimento ou redução de working set não foi usado como diagnóstico de vazamento.
