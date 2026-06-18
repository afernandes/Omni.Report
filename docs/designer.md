# Designer Blazor

O designer visual é distribuído como **Razor Class Library** (`Reporting.Designer.Blazor`)
e pode ser embedded em qualquer Blazor Web App ou MAUI Blazor Hybrid. O visual segue o
design system **Print Studio** (paleta ferrugem `#C2410C`, IBM Plex Sans/Mono, Bootstrap
5.3.3, ícones Lucide).

## Setup

### Blazor Web App

`Program.cs` — o designer é uma Razor Class Library; registre o pipeline de preview/impressão/
exportação. O `<ReportDesigner />` em si **não** exige um `AddOmniReportDesigner()` (esse método
não existe):

```csharp
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddOmniReportViewer();              // pipeline de preview (Skia)
builder.Services.AddOmniReportDesignerPrinting();    // impressão: browser universal + nativo opt-in
builder.Services.AddOmniReportDesignerDataConnect(); // editor de fonte de dados live-DB (opcional)
builder.Services.AddDefaultExporters();              // PDF/XLSX/HTML/SVG/CSV/JSON/Markdown no preview
```

`App.razor` (ou `_Host.cshtml`) — carregue as **cinco** folhas de estilo (nesta ordem) no
`<head>` e o módulo JS antes de `</body>` (o JS habilita drag/resize, marquee, smart-guides
e réguas):

```razor
<link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/tokens.css" />
<link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/base.css" />
<link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/layout.css" />
<link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/components.css" />
<link rel="stylesheet" href="_content/Reporting.Designer.Blazor/css/overlays.css" />
<!-- ...antes de </body>: -->
<script src="_content/Reporting.Designer.Blazor/js/designer.js"></script>
```

Página `/designer` — o componente recebe um `DesignerState` em `InitialState` e devolve o
`.repx` salvo via `OnSaved`:

```razor
@page "/designer"
@rendermode InteractiveServer
@using Reporting.Designer.Blazor
@using Reporting.Designer.Blazor.ViewModels

<ReportDesigner InitialState="_state" OnSaved="HandleSaved" />

@code {
    private readonly DesignerState _state = new();

    private async Task HandleSaved(byte[] repxBytes)
        => await File.WriteAllBytesAsync("user-report.repx", repxBytes);
}
```

> Para abrir um relatório existente, troque o conteúdo do estado com
> `_state.ReplaceActiveReport(ReportDefinitionViewModel.FromDefinition(def))` ou
> `_state.Load(repxBytes)`.

### MAUI Blazor Hybrid

Já vem habilitado no sample `Reporting.Samples.MauiHybrid` — UI desktop no Windows,
viewer touch no Android.

## Estrutura

O designer segue o esqueleto especificado no design package (`Docs/COMPONENTS.md`):

```
ReportDesigner
├── TopBar              ← logo, tabs, command palette trigger (⌘K)
├── Toolbar             ← formatação, alinhamento, undo/redo
├── LeftPanel
│   ├── ElementToolbox  ← drag-source de elementos
│   └── DataSourcesTree ← campos arrastáveis
├── CanvasRegion
│   ├── RulerHorizontal/Vertical
│   ├── PageCanvas
│   │   └── BandStrip × N
│   ├── SelectionLayer
│   ├── SmartGuidesLayer
│   └── MarqueeLayer
├── RightPanel
│   └── PropertyGrid    ← editores categorizados por tipo de propriedade
└── StatusBar           ← zoom, posição, página atual
```

## Atalhos de teclado

A matriz completa fica em `src/Reporting.Designer.Blazor/Docs/KEYBOARD-SHORTCUTS.md`.
Resumo:

| Categoria | Atalho | Ação |
|---|---|---|
| Globais | `Ctrl+S` | Salvar `.repx` |
| | `Ctrl+O` | Abrir |
| | `Ctrl+Z` / `Ctrl+Y` | Undo / Redo |
| | `F5` | Toggle Preview |
| | `Ctrl+K` | Command Palette |
| Seleção | `Ctrl+A` | Selecionar tudo |
| | `Shift+Click` | Adicionar à seleção |
| | `Esc` | Limpar seleção |
| Manipulação | `Delete` | Remover |
| | `Arrows` | Mover 1 mil |
| | `Shift+Arrows` | Mover 10 mils |
| Alinhamento | `Ctrl+L` | Alinhar esquerda |
| | `Ctrl+R` | Alinhar direita |
| | `Ctrl+E` | Centralizar horizontal |
| Property Grid | `F8` | Abrir Expression Editor |

## ViewModels

A camada de UI usa ViewModels mutáveis (`ReportDefinitionViewModel`,
`BandViewModel`, `ElementViewModel`) com `INotifyPropertyChanged` interno.
Conversão de/para `ReportDefinition` (records imutáveis) acontece em load/save.

```csharp
var vm = ReportDefinitionViewModel.FromDefinition(report); // records imutáveis → VM mutável
vm.Name = "Vendas (rev. 2)";                               // edição via propriedades observáveis
var updated = vm.Build();                                  // VM → ReportDefinition imutável
```

`PageSetup` é um record imutável — para alterá-lo, troque o valor inteiro
(`vm.PageSetup = vm.PageSetup with { Margins = ... }`), não um sub-campo.

## Undo/Redo

`CommandHistory` operando sobre `IDesignerCommand`:

```csharp
public interface IDesignerCommand
{
    void Execute(DesignerState state);
    void Undo(DesignerState state);
}
```

Implementações já prontas: `AddElement`, `MoveElement`, `ResizeElement`,
`DeleteElement`, `ChangeProperty`, `ReorderElement`.

Limite default: 100 comandos. Configurável via `DesignerOptions.HistoryDepth`.

## Themes

Duas variantes via `data-theme` no `<html>`:

```javascript
document.documentElement.setAttribute('data-theme', 'dark');
```

Todos os componentes consomem tokens via `var(--*)`. **Não há literais de cor**
nos `.razor.css` — uma lint rule no CI valida.

## Customização

O ponto de extensão público do `<ReportDesigner />` é o `DesignerState` passado em
`InitialState` — monte-o antes de renderizar o componente:

- **Catálogos**: popule `DataSources`, `Parameters` e `Relations` (fontes, parâmetros e
  relações master-detail disponíveis no editor).
- **Relatório inicial**: `ReplaceActiveReport(...)` ou `Load(repxBytes)` para abrir um `.repx`.
- **Dados de preview**: `PreviewDataRegistry` injeta dados in-memory para o preview renderizar
  elementos data-bound (gráficos/tablix/map) sem um banco vivo.
- **Tema**: `Theme = "dark"` (ou `data-theme` no `<html>`); sobrescreva os tokens `--*` do
  `tokens.css` para repaginar a paleta.

`DesignerState` é `sealed` — componha (monte e configure a instância), não herde.
