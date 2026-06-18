# Contribuindo com OmniReport

Obrigado pelo interesse! Este documento descreve o fluxo de contribuição.

## Setup

Requer **.NET 10 SDK** (≥ `10.0.100`). Para módulos opcionais:

- **GDI / Spooler Windows**: precisa de Windows (TFM `net10.0-windows`).
- **MAUI Hybrid sample**: `dotnet workload install maui maui-windows`. Para Android:
  `dotnet workload install android`, depois `dotnet android sdk install --accept-licenses`.

```powershell
git clone https://github.com/<user>/OmniReport.git
cd OmniReport
dotnet build OmniReport.slnx -c Release      # 0 warnings
dotnet test  OmniReport.slnx -c Release      # 375+ testes
```

## Branch model

- `main` — sempre verde, sempre publicável.
- `feature/<slug>` — branches curtas para uma feature ou bug-fix.
- `release/<version>` — somente para preparar releases (raro; o normal é cortar tag direto de `main`).

Pull requests sempre alvo `main`. Squash-merge por padrão. Tag de release segue
[SemVer](https://semver.org).

## Conventional Commits

Mensagens de commit seguem [Conventional Commits 1.0](https://www.conventionalcommits.org/pt-br/v1.0.0/).
Tipos aceitos:

| Tipo | Quando usar |
|---|---|
| `feat` | nova funcionalidade visível ao usuário |
| `fix` | correção de bug |
| `docs` | apenas documentação |
| `style` | formatação, whitespace, sem mudança de comportamento |
| `refactor` | reorganização interna sem mudar API ou comportamento |
| `perf` | melhoria de performance |
| `test` | adicionar ou ajustar testes |
| `build` | mudanças em build, deps, scripts |
| `ci` | mudanças em CI/CD |
| `chore` | tarefas de manutenção |

**Escopo opcional** entre parênteses: `feat(layout): ...`, `fix(escpos): ...`.

**Breaking change**: adicionar `!` antes dos dois pontos OU rodapé `BREAKING CHANGE: <descrição>`.

Exemplos:

```
feat(designer): add CommandPalette overlay (Ctrl+K)

fix(layout): close groups before emitting next row's primitives

refactor(skia): extract SkiaPrimitiveRenderer for reuse in PDF exporter

feat(viewer)!: change OnExported to async EventCallback

BREAKING CHANGE: OnExported now takes EventCallback<ReportExportEventArgs>
instead of Action<ReportExportEventArgs>.
```

## Padrões de código

- C# 13 / .NET 10. `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=13`.
- `TreatWarningsAsErrors=true` em projetos de produção (`src/Reporting.*`). Testes
  podem relaxar.
- File-scoped namespaces, expression-bodied members quando óbvio.
- Records imutáveis para domínio; classes mutáveis apenas em ViewModels do designer.
- Identificadores em **inglês**. Comentários XML doc em **inglês**. Strings de usuário
  e exemplos em **pt-BR** (engine usada no Brasil em primeiro lugar).
- Logging via `Microsoft.Extensions.Logging`. Configuração via `IOptions<T>` quando
  fizer sentido.

## Testes

- xUnit + FluentAssertions são padrão. bUnit para componentes Razor.
  PdfPig para inspeção de PDF, ClosedXML para readback de XLSX, Verify para snapshots
  (quando vier).
- Cobertura ≥ 80% em projetos `src/Reporting.*` core (Core, Expressions,
  Layout, Rendering, CodeFirst, Serialization, Output.*, Printing.EscPos,
  Rendering.Gdi). Viewer/Designer ≥ 60%.
- Cada PR adiciona testes para o que muda. Não há "PR só de código".

## Processo de PR

1. Abra issue para discutir mudanças não-triviais ANTES de codar.
2. Crie branch `feature/<slug>`.
3. Implemente: código + testes + atualização de docs/CHANGELOG quando aplicável.
4. Rode local: `dotnet build` e `dotnet test` ambos limpos.
5. Conventional Commit nas mensagens.
6. Abra PR para `main` com descrição: o que muda, por quê, links para issues.
7. CI roda automaticamente (build-windows + build-linux + pack). Tudo verde =
  pode revisar.
8. Reviewer aprova → squash-merge → PR fica fechado, commit fica em `main`.

## Plataformas e TFMs

| Projeto | TFM | Nota |
|---|---|---|
| `Reporting.Core`, `Reporting.Expressions`, `Reporting.DataSources`, `Reporting.Layout`, `Reporting.Rendering`, `Reporting.CodeFirst`, `Reporting.Serialization`, `Reporting.Printing` | `net10.0` | Cross-platform puro |
| `Reporting.Rendering.Skia`, `Reporting.Output.Pdf`, `Reporting.Output.Excel`, `Reporting.Printing.EscPos` | `net10.0` | Dependem de SkiaSharp/ClosedXML/SkiaSharp respectivamente — cross-plat |
| `Reporting.Rendering.Gdi`, `Reporting.Printing.WindowsSpooler` | `net10.0-windows` | Windows-only via System.Drawing.Printing |
| `Reporting.Printing.Android` | `net10.0` (stub) ou `net10.0-android` (real) | Gated em `OMNIREPORT_BUILD_ANDROID=true` |
| `Reporting.Viewer.Blazor`, `Reporting.Designer.Blazor` | `net10.0` (Razor Class Library) | Hosteable em Blazor Server, Blazor Web App, MAUI Hybrid |
| `Reporting.Hosting.AspNetCore` | `net10.0` | Wires DI |
| `Reporting.Samples.MauiHybrid` | `net10.0-windows10.0.19041.0` + `net10.0-android` (cond.) | Workload MAUI obrigatório |

## Code of Conduct

Seja gentil. Comentários focam no código, não na pessoa. Diversidade de opiniões e
perspectivas é bem-vinda — debates são saudáveis, ataques pessoais não.

## License

Ao contribuir, você concorda em licenciar sua contribuição sob a MIT (mesma do projeto).
