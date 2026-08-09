# Benchmarks

Harness BenchmarkDotNet em `benchmarks/Reporting.Benchmarks`. Serve a dois propósitos: **barreira contra
regressão** de performance e **base factual** para decisões de arquitetura que antes eram tomadas por
intuição.

## Como rodar

```bash
dotnet run -c Release --project benchmarks/Reporting.Benchmarks -- --filter "*Pagination*"
```

Sem `--filter` abre o seletor interativo; `--list flat` lista tudo. Resultados vão para
`BenchmarkDotNet.Artifacts/results` (markdown + CSV), que é git-ignored. O projeto **nunca é empacotado** —
`Directory.Build.props` força `IsPackable=false` para `*.Benchmarks`, verificado com `dotnet pack`.

Os jobs usam `warmupCount: 1, iterationCount: 3` — sinal suficiente para comparar, execução em minutos e não
em horas. Para um estudo sério (investigar uma regressão específica), suba as iterações.

> **Ler "Allocated" corretamente:** é o total alocado por operação (*churn*), **não** o pico de memória
> retida. Um número alto significa pressão de GC, não necessariamente working set alto.

## Linha de base

Máquina de desenvolvimento (Windows, .NET 10). Números **relativos** são o que importa; os absolutos variam
com o hardware.

### Paginação — como escala com o número de linhas

| Linhas | Tempo | Alocado | Alocado/linha |
|---:|---:|---:|---:|
| 1.000 | 7,3 ms | 9,6 MB | ~9,6 KB |
| 10.000 | 158 ms | 96,7 MB | ~9,7 KB |
| 100.000 | 1,00 s | 963 MB | ~9,6 KB |

**A alocação é perfeitamente linear** — ~9,6 KB por linha, constante nas três escalas. Não há complexidade
escondida O(n²) no caminho de paginação, o que é a boa notícia. A má é o coeficiente: um relatório de 100 mil
linhas movimenta perto de 1 GB, com coletas Gen2 (LOH sob pressão).

**O que isso diz sobre o streaming (ROADMAP item 15):** a materialização da entrada é só uma parte do total.
O grosso é a **saída** — cada linha vira primitivos de layout que o `RenderedReport` guarda até o export. Ou
seja, transformar a leitura em streaming reduz o pico, mas **não** derruba os ~9,6 KB/linha, porque as páginas
renderizadas continuam em memória. Um ganho real de memória exigiria também streamar o *lado da saída*
(emitir páginas conforme são fechadas, em vez de acumular `RenderedReport`) — escopo bem maior do que o item
15 descrevia. Medir antes evitou refatorar o motor pela metade e concluir que não adiantou.

### Segundo passe (`Page.Total`)

| Cenário | Tempo | Alocado |
|---|---:|---:|
| Passe único | 55,5 ms | 42,8 MB |
| Com `Page.Total` | 115,0 ms | 82,2 MB |
| **Custo** | **2,08×** | **1,92×** |

"Página N de M" **dobra** o custo do layout: o total de páginas só é conhecido depois do primeiro passe, então
o relatório inteiro é paginado duas vezes. É um preço justo pela conveniência, mas agora é um número — quem
tem relatório gigante e não precisa do "de M" sabe exatamente o que economiza.

### Expressões

| Caminho | Tempo | Alocado |
|---|---:|---:|
| Cache quente (mesma expressão) | 891 ns | 1,95 KB |
| Cache frio (expressão nova) | 4.527 ns | 2,46 KB |

O cache de parse do `ExpressionCompiler` vale **~5,1×**. É por isso que o `ReportPaginator` compartilha um
único compilador entre execuções mesmo tendo virado por-execução no resto (`ReportPaginator.cs:63`): o cache é
`ConcurrentDictionary`, então continua seguro e continua pagando.

### Export (5.000 linhas, já paginado)

| Exporter | Tempo | Alocado |
|---|---:|---:|
| PDF (Skia) | 100 ms | 11,7 MB |
| Excel (ClosedXML) | 348 ms | 48,0 MB |

O Excel é **3,5× mais lento e aloca 4,1×** mais que o PDF — apesar de produzir *menos* conteúdo (só texto; ver
a [matriz de fidelidade](rdl-coverage.md#matriz-de-fidelidade--o-que-cada-exporter-preserva)). O custo está em
construir o modelo de objetos do OpenXML via ClosedXML, não no nosso lado.

## O que ainda não é medido

- **Import/export RDL** — o `RdlImporter`/`RdlWriter` são os arquivos mais tocados do repo e não têm medição.
- **Pico de memória retida** (working set), que é o que realmente limita um host — `[MemoryDiagnoser]` mede
  churn. Precisaria de um diagnosticador de heap ou medição fora do processo.
- **Concorrência** — throughput com N paginações simultâneas, agora que o paginador é thread-safe.
