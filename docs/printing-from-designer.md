# Impressão no Report Designer

Este guia explica como a impressão funciona no Designer, em qualquer um dos
três hosts suportados (Blazor Server, Blazor WebAssembly, MAUI Blazor).

## Arquitetura

O Designer separa o "o que imprimir" (decidido pela aplicação, configurável
pela janela `PrintDialog`) do "como mandar pra impressora" (decidido pelo
host). Essa separação é a mesma que DevExpress, Telerik e FastReport fazem
nos seus designers web.

```
            ┌─────────────────────────────────────┐
            │  PrintDialog.razor                  │
            │   - Página / Tamanho / Orientação   │
            │   - Cópias + Intercalar             │
            │   - Cor                             │
            │   - Modo: Diálogo / Direto / PDF    │
            └─────────────────────────────────────┘
                          │
                          ▼ PrintRequest
            ┌─────────────────────────────────────┐
            │     IDesignerPrintService           │
            └─────────────────────────────────────┘
                          │
            ┌─────────────┴─────────────┐
            ▼                           ▼
  ┌────────────────────┐      ┌────────────────────┐
  │ BrowserPrintService│      │NativePrinterAdapter│
  │ (Server/WASM/MAUI) │      │  (MAUI + Windows / │
  │  PDF +window.print │      │   Android Native)  │
  └────────────────────┘      └────────────────────┘
                                       │
                                       ▼
                            ┌──────────────────┐
                            │  IReportPrinter  │
                            │ (WindowsSpooler/ │
                            │  EscPos/Android) │
                            └──────────────────┘
```

## Por que essa arquitetura?

### Browser-based como padrão universal

Hosts web (Server / WASM) **não podem** acessar a impressora local
diretamente — sandbox do navegador bloqueia. A solução universal é:

1. Gerar PDF no .NET
2. Mandar os bytes pro JS
3. JS carrega o PDF em um `<iframe>` oculto
4. Chamar `iframe.contentWindow.print()`
5. O usuário vê o **diálogo nativo do SO** (Windows/macOS/Linux/Android)
   pela ponte do navegador — escolhe impressora, duplex, papel, tudo lá

Esse mesmo flow funciona no **MAUI Blazor Hybrid** porque o WebView nativo
expõe `window.print()` que abre o diálogo do SO. Uma implementação,
três hosts.

### Native como opt-in

Para casos que justificam (impressão silenciosa sem diálogo, ESC/POS térmica
direta, kiosks que não querem mostrar nada ao operador), a aplicação pode
registrar um `IReportPrinter` (Windows Spooler / ESC/POS / Android) e o
`NativePrinterAdapter` rotear pra ele quando o usuário escolhe **Direto** no
diálogo do Designer.

## Como configurar

### 1. Web puro (qualquer host) — zero config além do default

```csharp
// Program.cs
builder.Services.AddOmniReportDesignerPrinting();
```

Isso registra o `BrowserPrintService`. Não precisa fazer nada no front —
o `designer.js` já carrega o `designer-print.js` automaticamente.

### 2. MAUI Windows — diálogo + opção "Direto"

```csharp
// MauiProgram.cs
builder.Services
    .AddOmniReportDesignerPrinting()
    .WithNativePrinter<WindowsSpoolerPrinter>();
```

O diálogo agora mostra o dropdown de impressoras e habilita o botão
**Direto** (sem prompt do SO).

### 3. MAUI Android — Print Framework do Android

```csharp
builder.Services
    .AddOmniReportDesignerPrinting()
    .WithNativePrinter<AndroidPrintFrameworkPrinter>();
```

### 4. Impressora térmica ESC/POS (kiosks, NFC-e cupom)

```csharp
builder.Services
    .AddOmniReportDesignerPrinting()
    .WithNativePrinter<EscPosPrinter>(); // configurar TCP/serial via opções
```

## A janela PrintDialog

A janela tem duas colunas, mimica Adobe Acrobat e SAP Crystal Reports:

**Esquerda (configuração):**

- **Destino**: impressora (quando o adapter native está ligado) + modo de
  saída (Diálogo / Direto / PDF)
- **Páginas**: Todas / Atual / Personalizado (`1-3, 5, 7-9`)
- **Papel**: manter do relatório OU forçar A4 / A5 / Letter / Legal / Térmica
  58 mm / Térmica 80 mm + orientação
- **Opções**: cópias + intercalar + cor (colorido / cinza / P&B)

**Direita (pré-visualização):**

- Thumbnail da página atual com navegação `‹ Página N de M ›`
- Atualiza ao mudar paper/orientação no painel da esquerda (o owner
  re-paginar e passar `PreviewPages` atualizado)

**Rodapé:** Cancelar / Imprimir (com estado de "Enviando…" e mensagens de
erro inline).

## Modos de saída

| Modo | O que acontece | Quando usar |
|------|-----------------|-------------|
| 🖨 Diálogo | Gera PDF + `window.print()` no iframe | Padrão. Usuário escolhe impressora no diálogo do SO. |
| ⚡ Direto | Vai direto via `IReportPrinter` | Quando `NativePrinterAdapter` registrado e usuário quer impressão silenciosa. |
| 💾 PDF | Baixa o arquivo .pdf | Quer guardar / enviar por email em vez de imprimir. |

## Diferenças vs outras engines

| Engine | Padrão para web | Direto-spooler |
|--------|------------------|------------------|
| OmniReport | PDF + `window.print()` (universal) | ✅ Opt-in via `WithNativePrinter` |
| DevExpress | PDF + `window.print()` | ❌ apenas WinForms / WPF |
| Telerik | PDF + `window.print()` | ❌ apenas .NET Framework |
| FastReport | PDF + `window.print()` | ❌ apenas desktop |
| Crystal Reports | ActiveX (Windows) ou PDF | ✅ ActiveX (deprecado) |

OmniReport segue o consenso da indústria moderna (PDF + browser print) e
ADICIONA a rota native como opt-in — o melhor dos dois mundos.

## Atalho de teclado

`Ctrl+P` abre a janela de impressão direto do canvas do Designer (sem
precisar entrar no preview primeiro). Se ainda não houver paginação cache,
o Designer roda o paginate antes — UX igual a Acrobat onde Ctrl+P sempre
"funciona".

## Testes

- `PrintRequestTests` — 14 casos de parsing de range (`"1-3, 5, 7-9"`,
  edge cases tipo `"-3"`, `"5-"`, fora do range, garbage).
- `BrowserPrintServiceTests` — `SupportsDirectPrint=false`,
  `ListPrintersAsync` vazia, dispatch correto pro JS na rota
  `BrowserDialog`, `SystemSpooler` joga `NotSupportedException`.
