# Golden files

Suíte de regressão visual. Cada teste roda um relatório do catálogo
([`GoldenReports.cs`](GoldenReports.cs)) por todo o pipeline e compara o resultado com um arquivo
`Goldens/*.verified.txt` versionado. Se algo mudou, o teste falha e mostra o diff.

## O que é pinado, em duas camadas

| Camada | Arquivo | O que pega |
|---|---|---|
| **Display list** — a lista de instruções de desenho que o paginador produz | `Goldens/<Nome>.verified.txt` | posição, tamanho, fonte, cor, alinhamento, borda, ordem, quebra de página, `Page.Total` |
| **Emissão SVG** — o que o backend vetorial de fato desenhou | `Goldens/<Nome>.svg.verified.txt` | preenchimento, traço, gradiente, `<defs>` — o que some *depois* do layout estar correto |

As duas juntas cobrem o buraco que os testes de contagem de pixels de tinta deixam: eles provam que
*algo* foi desenhado mais ou menos onde se esperava, mas um baseline deslocado, um gradiente que
degrada para a cor inicial ou um alinhamento invertido mantêm a contagem idêntica.

## Como atualizar um golden

1. Rode a suíte. Cada golden que divergir gera um `Goldens/<Nome>.received.txt` ao lado do
   `.verified.txt` (o `.received.*` é ignorado pelo git).
2. **Leia o diff.** Este é o passo que dá valor à suíte — aceitar um golden sem ler transforma o
   arquivo num carimbo.
3. Se a mudança é a que você pretendia, substitua o `.verified.txt` pelo `.received.txt`.

Nenhuma ferramenta de diff é aberta automaticamente (`DiffRunner.Disabled`, em
[`ModuleInitializer.cs`](ModuleInitializer.cs)): numa máquina de dev abriria uma janela, e no CI a
tentativa só falharia.

## Por que isso é estável em Windows e Linux

O CI roda a suíte nos dois. Um golden que dependa de fonte seria verde na máquina do autor e
vermelho para sempre no runner Linux, então três coisas foram escolhidas de propósito:

- **A geometria vem do display list**, que é aritmética inteira em mils, com o
  `AverageWidthTextMeasurer` (média por caractere, sem arquivo de fonte, sem shaping, sem cultura).
- **Os relatórios fixam uma cultura** (`en-US`). Sem isso, `Format("N2")` escreve `1.450,90` numa
  máquina pt-BR e `1,450.90` no CI.
- **O SVG é resumido antes de comparar** ([`SvgShape.cs`](SvgShape.cs)). O Skia escreve texto como
  avanços por glifo (`x="42.551998, 54.106686, …"`) tirados da fonte resolvida — no Linux não há
  Arial e o fontconfig substitui por algo com métrica diferente. Formas mantêm todos os atributos;
  de `<text>` sobram fonte, peso e o conteúdo. A geometria do texto não se perde: ela já está
  pinada, do lado do modelo, no golden de display list.

Ids de elemento são reescritos para ordinais (`e1`, `e2`, …) porque `ReportElement.Id` é um
`Guid.NewGuid()` — o ordinal preserva o que importa (que a primitiva *tem* origem, e que duas
primitivas compartilham a mesma) sem mudar a cada execução.

## Lacunas conhecidas

[`RenderGapCharacterizationTests.cs`](RenderGapCharacterizationTests.cs) fixa dois defeitos que esta
suíte expôs na primeira execução — `Style.Border` em elemento de texto e `CornerRadius` em retângulo
sem filhos, ambos descartados antes de virar primitiva. Os goldens registram a saída degradada como
a verdade de hoje; quando o defeito for corrigido, o teste de caracterização falha alto, é apagado, e
os goldens afetados são reaceitos.
