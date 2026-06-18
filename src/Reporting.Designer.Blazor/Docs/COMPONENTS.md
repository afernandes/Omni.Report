# Component Spec — Report Designer

Este documento descreve os **36 componentes** que compõem o Report Designer. Cada um vai virar um componente Razor isolado em `RD.Designer.Components.*`, com CSS isolado por componente (`.razor.css`).

## Como ler

Cada componente segue o template:

```
## NomeDoComponente
Propósito · Anatomia · Props · Eventos · Estados · Tokens · Atalhos · Notas Blazor · Variantes
```

**Convenções:**
- Propriedades em `PascalCase` (Blazor); CSS classes em `kebab-case`.
- Eventos em `EventCallback<T>`; preferir tipos fortes a `object`.
- CSS tokens consumidos: ver `DESIGN-TOKENS.md`.
- "Estados" omite o trivial — listo só os que afetam visual.

---

# Estruturais

## 1. AppShell
**Propósito:** Composição raiz que monta TopBar, TabBar, Toolbar, Workspace (com painéis e canvas) e StatusBar num grid CSS responsivo.
**Anatomia:** `<header.topbar>` · `<div.tabbar>` · `<div.toolbar>` · `<div.workspace>` (LeftPanel + CanvasRegion + RightPanel) · `<footer.statusbar>`.
**Props:**
- `ReportDocument? CurrentDocument` — default `null` (estado vazio)
- `IEnumerable<ReportDocument> OpenDocuments` — abas
- `ThemeMode Theme = ThemeMode.System`
**Eventos:** `OnDocumentChanged`, `OnThemeChanged`.
**Estados:** loading (skeleton de painéis), empty (placeholder "Abrir relatório / Novo"), normal.
**Tokens:** `--bg-panel`, `--border`, `--h-topbar`, `--h-tabbar`, `--h-toolbar`, `--h-statusbar`.
**Atalhos:** todos os globais — propagados via `KeyboardShortcutService`.
**Blazor:**
- `RD.Designer.Components.AppShell`
- Injetar: `IDocumentService`, `IShortcutService`, `IThemeService`.
- Aplicar `data-theme` no `<html>` via JSInterop no `OnInitialized`.
**Variantes:** —

---

## 2. TopBar
**Propósito:** Barra superior com marca, menu hierárquico textual, Command Palette **sempre visível** no centro e ações de conta/tema à direita.
**Anatomia:** `.brand` (logo + nome + crumb) · `.menu` (Arquivo/Editar/…) · `.cmd-trigger` (input fake que abre palette) · `.topbar-right` (ícones validate/share/notifications/settings/avatar).
**Props:**
- `string DocumentName`
- `string? Crumb` — caminho do projeto
- `bool HasNotifications = false`
- `UserAccount? Account`
**Eventos:** `OnOpenMenu(string menuId)`, `OnOpenPalette`, `OnOpenSettings`.
**Estados:** menu aberto (item destacado), notification dot, avatar com gradiente do usuário.
**Tokens:** `--bg-panel`, `--border`, `--text-primary`, `--text-tertiary`, `--accent`.
**Atalhos:** `⌘K` (palette), `⌘,` (settings), `⌥⇧L/D` (theme).
**Blazor:**
- `RD.Designer.Components.TopBar`
- O `cmd-trigger` é botão, não input — abre o `CommandPalette` real.
- Use `<MenuItem>` filho para cada entrada de menu.
**Variantes:** Compact (some menu vira hamburger em ≤1200px).

---

## 3. TabBar
**Propósito:** Abas de relatórios abertos, estilo navegador, com indicador de dirty e botão "+".
**Anatomia:** `.tab` (ícone tipo + nome + dirty-dot + close-btn) · `.tab-new` · `.tabbar-right` (lista de abas dropdown).
**Props:**
- `IList<TabModel> Tabs`
- `string ActiveTabId`
- `bool ShowOverflow = true`
**Eventos:** `OnSelect(string id)`, `OnClose(string id)`, `OnNew`, `OnReorder(IList<string> newOrder)`.
**Estados:** active (borda accent no topo), dirty (dot), hover (close visível), disabled (ícone esmaecido).
**Tokens:** `--bg-panel-strong`, `--bg-canvas` (active), `--accent` (dot + topo), `--border`.
**Atalhos:** `⌘Tab`, `⌘⇧Tab`, `⌘W` (fechar), `⌘1..9` (saltar para aba N).
**Blazor:**
- `RD.Designer.Components.TabBar`
- Drag-reorder via JSInterop (SortableJS ou nativo HTML5 DnD).
- `OnClose` deve checar dirty e abrir DirtyConfirmDialog.
**Variantes:** —

---

## 4. Toolbar
**Propósito:** Barra de ações contextual, agrupada por função, com inserção rápida de elementos, alinhamento (condicional) e zoom.
**Anatomia:** `.tb-group` (separados por `.tb-sep`) · `.tb-btn` (icon-only ou icon+label) · `.tb-zoom` (compound) · `.tb-btn.primary` (Preview) · `.tb-align-grp` (condicional).
**Props:**
- `IList<ElementSelection> Selection`
- `int Zoom = 100`
- `bool GridVisible`, `bool SnapEnabled`, `bool RulerVisible`
**Eventos:** `OnAction(string actionId)`, `OnZoomChange(int)`, `OnTogglePreview`.
**Estados:** disabled (cinza, sem hit), toggled (fundo accent-soft + borda accent-border), align-group oculto/visível por count.
**Tokens:** `--bg-panel`, `--accent-soft`, `--accent-border` (toggled).
**Atalhos:** ver KEYBOARD-SHORTCUTS para todos os botões.
**Blazor:**
- `RD.Designer.Components.Toolbar`
- `<ToolbarButton Action="..." Icon="..." Shortcut="..." />`
- O grupo Align observa `Selection.Count >= 2` via cascading parameter.
**Variantes:** Inverted (em Preview, troca para nav/zoom/export/print).

---

## 5. StatusBar
**Propósito:** Rodapé compacto com coordenadas do cursor, página, zoom, contagem de seleção, validação e estado da fonte de dados.
**Anatomia:** série de `.sb-item` separados por borda 1px; cada item tem `.key` (label) e `.val` (valor mono tabular).
**Props:**
- `CursorPosition Cursor` (`X`, `Y` em mm)
- `int CurrentPage, TotalPages`
- `int Zoom`
- `int SelectedCount`
- `ValidationStatus Validation` (`Ok` / `Warnings` / `Errors`)
- `string DataSourceName`, `bool DataSourceConnected`
**Eventos:** `OnClickValidation`, `OnClickDataSource`.
**Estados:** validation = `ok`/`warn`/`error` muda ícone e cor; data source disconnected mostra danger pill.
**Tokens:** `--bg-panel-strong`, `--text-tertiary`, `--text-primary`, `--font-mono`, `--success`, `--warning`, `--danger`.
**Atalhos:** clicar em validation abrirá Problems panel (futuro).
**Blazor:**
- `RD.Designer.Components.StatusBar`
- Throttle de cursor (`~30ms`) — sem isso re-renderiza a cada pixel.
**Variantes:** —

---

## 6. SplitPane
**Propósito:** Divisor entre painéis adjacentes, com handle de 4px (hot zone) e linha 1px visível.
**Anatomia:** `<div.split-handle.{left|right|top|bottom}>` posicionado absolute; cursor `col-resize` / `row-resize` no hover.
**Props:**
- `Orientation Orientation` (Vertical | Horizontal)
- `int InitialSize, MinSize, MaxSize`
- `string PersistKey` (chave em localStorage)
**Eventos:** `OnSizeChanged(int newSize)`.
**Estados:** idle, hover (linha vira selection-blue), dragging (linha persiste).
**Tokens:** `--selection` (handle ativo), `--border` (invisible em idle).
**Atalhos:** —
**Blazor:**
- `RD.Designer.Components.SplitPane`
- Listener em `pointerdown` (não `mousedown`, suporta touch).
- Persistir size em `localStorage` via JSInterop.
**Variantes:** —

---

# Painel esquerdo

## 7. LeftPanel
**Propósito:** Container do painel esquerdo com tabs internas (Toolbox / Dados / Parâmetros) e SplitPane na borda direita.
**Anatomia:** `.panel-tabs` · `.panel-body` · `.split-handle.right`.
**Props:**
- `string ActiveTab = "toolbox"`
- `int Width = 240, MinWidth = 180, MaxWidth = 360`
**Eventos:** `OnTabChange`, `OnResize`.
**Estados:** active tab (underline accent), badge-count.
**Tokens:** `--bg-panel`, `--border`, `--accent` (active indicator).
**Atalhos:** `⌘⇧E` (toggle painel), `⌘1/2/3` (trocar tab).
**Blazor:** `RD.Designer.Components.LeftPanel`. Cada tab é um `<TabPane>` filho.
**Variantes:** Collapsed (mostra só ícones verticais — futuro).

---

## 8. Toolbox
**Propósito:** Lista de elementos arrastáveis, agrupados por categoria colapsável.
**Anatomia:** `.toolbox-search` · `.tb-group-h` (header com chevron) · `.toolbox-list` (lista de `ToolboxItem`).
**Props:**
- `IList<ToolboxGroup> Groups`
- `string FilterText`
- `IDictionary<string, bool> Expanded`
**Eventos:** `OnFilterChange(string)`, `OnToggleGroup(string id)`.
**Estados:** group collapsed (chevron `right`), expanded (`down`); search com matches destaca termo.
**Tokens:** `--text-tertiary` (group label), `--accent` (highlight do filter match).
**Atalhos:** `/` foca o filter.
**Blazor:** `RD.Designer.Components.Toolbox`. Categorias e itens via DI ou config JSON.
**Variantes:** —

---

## 9. ToolboxItem
**Propósito:** Item individual da Toolbox; arrastável para o canvas; gera um ElementCard ao soltar.
**Anatomia:** `.tbx-item` (grid 22px / 1fr / auto) com `.icon`, nome, e `.meta` (atalho ou tipo).
**Props:**
- `string Id`, `string Name`, `string IconName`
- `string ElementType` — qual tipo cria
- `string? Hint` — meta à direita
**Eventos:** `OnDragStart(DragData)`, `OnDoubleClick` (insere no centro do canvas).
**Estados:** default, hover (fundo surface), drag-source (fundo accent-soft, color accent).
**Tokens:** `--bg-surface`, `--accent-soft`, `--accent`, `--text-secondary`.
**Atalhos:** —
**Blazor:**
- `RD.Designer.Components.ToolboxItem`
- HTML5 DnD via `draggable="true"` + `dataTransfer.setData("application/x-rd-tool", Id)`.
- Drag preview customizado via canvas snapshot (JSInterop).
**Variantes:** —

---

## 10. DataSourceExplorer
**Propósito:** Árvore hierárquica de fontes de dados → entidades → campos. Cada campo é arrastável e cria um TextBox bindado ao soltar.
**Anatomia:** `.tree` · `.tree-node.depth-{0|1|2}` (chevron + type-icon + nome + tail).
**Props:**
- `IList<DataSource> DataSources`
- `string? FilterText`
- `IDictionary<string, bool> Expanded`
**Eventos:** `OnFieldDragStart(FieldRef)`, `OnContextMenu(FieldRef)`, `OnDoubleClick(FieldRef)`.
**Estados:** field hover destaca nome em accent; type-icon colorido por tipo (`#` num, `T` texto, calendar date, check bool, `$` money).
**Tokens:** `--data-bound` (binding hint), `--syn-number`, `--accent`.
**Atalhos:** clicar abre Field Inspector; `⌘C` copia path.
**Blazor:**
- `RD.Designer.Components.DataSourceExplorer`
- Carrega esquema via `IDataSourceService.GetSchemaAsync()`.
- Drag data: `application/x-rd-field` + JSON com path.
**Variantes:** —

---

## 11. ParametersList
**Propósito:** Lista de parâmetros do relatório com tipo, prompt e valor default; botão "+ Adicionar".
**Anatomia:** `.param-row` (grid `1fr / auto`) · `.p-name` (ícone + nome) · `.p-type` (chip mono) · `.p-value`.
**Props:**
- `IList<ReportParameter> Parameters`
**Eventos:** `OnAdd`, `OnEdit(ReportParameter)`, `OnDelete(string id)`, `OnReorder`.
**Estados:** row hover, required (asterisco no nome), invalid (borda danger).
**Tokens:** `--bg-surface` (chip type), `--text-tertiary`, `--accent` (asterisk required).
**Atalhos:** `⌘N` (com foco na lista) adiciona; `Del` remove.
**Blazor:** `RD.Designer.Components.ParametersList`. Edit via `ParameterEditDialog`.
**Variantes:** —

---

# Canvas

## 12. CanvasViewport
**Propósito:** Área de scroll/zoom/pan onde a página é renderizada. Gerencia cursor (default, grab, grabbing, crosshair, resize).
**Anatomia:** `.canvas-scroll` (overflow auto) · `.canvas-stage` (centered, padded) · descendentes Page, Rulers, SmartGuides etc.
**Props:**
- `int Zoom = 100` (50–400)
- `Tool ActiveTool` (`Select` / `Hand` / `Insert<T>`)
- `PageModel Page`
**Eventos:** `OnZoomChange`, `OnPan(int dx, int dy)`, `OnElementSelect`, `OnDropFromToolbox`, `OnDropField`.
**Estados:** cursors: `default`, `grab`/`grabbing` (Space), `crosshair` (Insert), `*-resize` (handle).
**Tokens:** `--bg-canvas`.
**Atalhos:** ver Canvas — Navegação.
**Blazor:**
- `RD.Designer.Components.CanvasViewport`
- Pan via `pointermove` quando Space pressionado; zoom via `wheel` + `ctrlKey`.
- `OnDragOver`/`OnDrop` recebem ToolboxItem ou Field.
**Variantes:** Preview (substituído pelo PreviewPane, mesma região).

---

## 13. Ruler
**Propósito:** Régua superior e esquerda em mm com indicador de cursor.
**Anatomia:** SVG inline com `<pattern>` de ticks (1mm subtle, 5mm médio, 10mm forte + número) + `<line>` para cursor.
**Props:**
- `Orientation Orientation`
- `int OffsetPx` (alinhar com page)
- `int CursorPositionMm`
- `int Zoom`
**Eventos:** clique cria guide line (futuro).
**Estados:** cursor visível ou não; tick highlight em range selecionado.
**Tokens:** `--ruler-bg`, `--ruler-tick`, `--ruler-text`, `--accent` (cursor line).
**Atalhos:** —
**Blazor:** `RD.Designer.Components.Ruler`. Render SVG; recomputa ticks ao mudar zoom.
**Variantes:** Horizontal | Vertical.

---

## 14. Page
**Propósito:** "Folha" central proporcional ao papel (A4 default), com sombra sutil sobre canvas, margens dashed e bandas filhas.
**Anatomia:** `.page-shell` (wrapper) · `.page` (a folha) · `.page-margins` (overlay dashed) · `Band`s como filhas.
**Props:**
- `PageSize Size` (A4, Letter, Custom)
- `Orientation Orientation`
- `Margins Margins` (top/right/bottom/left em mm)
- `int Columns = 1`
**Eventos:** `OnSizeChange`, `OnMarginsChange`.
**Estados:** com bandas, vazia (placeholder "Arraste uma banda"), printing (sem chrome).
**Tokens:** `--bg-surface`, `--page-shadow`, `--border` (margins).
**Atalhos:** `⌘⇧M` abre PageSetupDialog.
**Blazor:** `RD.Designer.Components.Page`. CalcGeometry baseado em `--mm` e zoom.
**Variantes:** Print (sem margens visíveis), Designer (com margens overlay).

---

## 15. Band
**Propósito:** Faixa horizontal sobre a página, contendo elementos. Tem header strip à esquerda e resize handle inferior.
**Anatomia:** `.band.t-{rh|ph|gh|d|gf|pf|rf}` · `.band-strip` (BandLabel + meta + color stripe) · `.band-content` (positioned absolute) · `.band-resize` (4px linha inferior).
**Props:**
- `BandType Type`
- `int HeightMm`
- `string? GroupName` (para GroupHeader/Footer)
- `bool Collapsed = false`
**Eventos:** `OnResize(int newHeight)`, `OnContextMenu`, `OnToggleCollapse`.
**Estados:** hover (strip mais claro), focused (band content highlight), resizing (cursor ns-resize), collapsed (height 24px só com label).
**Tokens:** `--band-strip`, `--band-strip-hover`, `--accent` / `--data-bound` / `--text-primary` (color stripe por tipo).
**Atalhos:** `⌘⇧B` insere nova banda; `Del` na strip remove.
**Blazor:** `RD.Designer.Components.Band`. Filhos via `RenderFragment ChildContent`.
**Variantes:** Por tipo (RH, PH, GH, D, GF, PF, RF) — cor + label distintos.

---

## 16. BandLabel
**Propósito:** Indicador do tipo de banda no header strip; 2 letras (`RH`, `PH`, `GH`, `DT`, `GF`, `PF`, `RF`) + altura em mm abaixo.
**Anatomia:** `.band-label` (mono bold 11px) · `.band-meta` (mono 8.5px) · color stripe à direita do strip.
**Props:**
- `BandType Type` — define abreviação + cor
- `int HeightMm`
- `string? Subtitle` — ex.: "PorCliente" (mostrado em tooltip)
**Eventos:** —
**Estados:** hover mostra full name (`ReportHeader`) em tooltip.
**Tokens:** `--accent` (RH/RF), `--data-bound` (GH/GF), `--text-primary` (D), `--text-secondary` (PH/PF).
**Atalhos:** —
**Blazor:** `RD.Designer.Components.BandLabel`. Map type→abbr via static dictionary.
**Variantes:** Compact (sem meta, quando banda < 5mm).

---

## 17. ElementCard
**Propósito:** Elemento renderizado no canvas (TextBox, Image, Line, etc.). Borda sutil em hover, selection ring + handles quando selecionado.
**Anatomia:** `.el.{is-data?}.{is-selected?}` posicionado absolute na banda · conteúdo (texto ou outro) · `.fx-badge` (se bindado e selected/hover) · `.handles` (se selected).
**Props:**
- `ElementType Type`, `string Id`
- `Rect Bounds` (X, Y, W, H em mm)
- `bool IsBound`, `bool IsSelected`, `bool IsLocked`
- `string? Content`, `string? Expression`
- `Style Style` (font, color, align, border, padding…)
**Eventos:** `OnSelect`, `OnDragStart`, `OnResize`, `OnDoubleClick` (edit inline).
**Estados:** default (transparente), hover (border-strong), selected (border selection + handles + fx badge), dragging (opacity 0.7), locked (ícone candado), error (borda danger se expression inválida).
**Tokens:** `--selection`, `--selection-soft`, `--data-bound` (left stripe se bound), `--border-strong` (hover).
**Atalhos:** todos de Canvas — Manipulação.
**Blazor:**
- `RD.Designer.Components.ElementCard`
- Polimórfico — usa `DynamicComponent` para renderizar conteúdo por Type.
- Bounds em mm; convertido a px via Zoom no parent.
**Variantes:** TextBox, Label, Image, Line, Rect, CheckBox, Barcode, Chart, Subreport.

---

## 18. SelectionHandles
**Propósito:** 8 quadradinhos brancos com borda azul nas 4 cantos + 4 meios; cursors apropriados.
**Anatomia:** `.handles` (inset -4px no .el) · `.handle.{nw|n|ne|e|se|s|sw|w}`.
**Props:**
- `Rect Bounds`
- `bool Constrained` (mantém proporção)
**Eventos:** `OnResize(string handle, Rect newBounds)`.
**Estados:** ativos durante drag (handle destacado).
**Tokens:** `--selection` (border), `--bg-surface` (fill).
**Atalhos:** `⇧+drag` constrange proporção; `⌥+drag` redimensiona pelo centro.
**Blazor:** `RD.Designer.Components.SelectionHandles`. Listener compartilhado de pointer no parent.
**Variantes:** —

---

## 19. SmartGuides
**Propósito:** Linhas finas magenta que aparecem durante drag para alinhar com bordas/centros de outros elementos. Texto da distância em mono.
**Anatomia:** `.smart-guide.{h|v}` (linha 1px) · `.smart-guide .dim` (chip mono com mm).
**Props:**
- `IList<Guide> ActiveGuides`
- `bool SnapEnabled = true`
- `int SnapThresholdPx = 4`
**Eventos:** internos — populated pelo CanvasViewport durante drag.
**Estados:** hidden, visible (durante drag/resize).
**Tokens:** `--smart-guide` (magenta `#E879F9` / `#F0ABFC` dark).
**Atalhos:** `⌘;` toggle snap.
**Blazor:** `RD.Designer.Components.SmartGuides`. Algoritmo: para cada elemento NÃO selecionado, calcule alinhamentos (left/center/right × top/middle/bottom) e snap se dentro do threshold.
**Variantes:** —

---

## 20. MarqueeSelection
**Propósito:** Retângulo de seleção em massa quando o usuário arrasta em área vazia.
**Anatomia:** `.marquee` (fill selection-soft 0.1 + border 1px dashed selection).
**Props:**
- `Rect Bounds` (live)
- `bool Additive` (Shift held — adiciona à seleção atual)
**Eventos:** `OnSelectionChange(IList<string> elementIds)`.
**Estados:** drawing.
**Tokens:** `--selection`, `--selection-soft`.
**Atalhos:** —
**Blazor:** `RD.Designer.Components.MarqueeSelection`. Hit-test contra cada `ElementCard.Bounds`.
**Variantes:** —

---

## 21. GridOverlay
**Propósito:** Pontos ou linhas finas no canvas para alinhamento visual. Default oculto.
**Anatomia:** SVG ou CSS `background-image` com pattern de dots a cada 5mm.
**Props:**
- `bool Visible = false`
- `int StepMm = 5`
- `GridStyle Style` (Dots | Lines)
**Eventos:** —
**Estados:** on/off.
**Tokens:** `--grid-dot`.
**Atalhos:** `⌘'` toggle grid.
**Blazor:** `RD.Designer.Components.GridOverlay`. Pure CSS via `radial-gradient`.
**Variantes:** Dots | Lines.

---

## 22. ExpressionBadge
**Propósito:** Pequeno `fx` no canto sup-esquerdo do elemento bindado. Sempre presente; mais visível em hover/selected.
**Anatomia:** `.fx-badge` posicionado absolute top -7px left -6px. Fundo teal, texto branco mono.
**Props:**
- `string? BoundProperties` — quais propriedades estão bindadas (tooltip)
**Eventos:** `OnClick` abre ExpressionEditor.
**Estados:** hidden (default), visible (hover/selected), error (vermelho se expressão inválida).
**Tokens:** `--data-bound` (teal), `--danger` (error variant).
**Atalhos:** `F4` (com elemento selecionado) tem o mesmo efeito do clique.
**Blazor:** `RD.Designer.Components.ExpressionBadge`. Filho do ElementCard.
**Variantes:** Default, Error, Multi (badge mostra `fx*` se múltiplas propriedades).

---

# Painel direito

## 23. RightPanel
**Propósito:** Container com tabs (Propriedades / Outline / Eventos) e header de objeto selecionado.
**Anatomia:** `.split-handle.left` · `.panel-tabs` · object header (ícone + nome + meta) · `.panel-body`.
**Props:**
- `string ActiveTab = "properties"`
- `ElementSelection? Selection` — null = mostra estado vazio
- `int Width = 300, MinWidth = 240, MaxWidth = 420`
**Eventos:** `OnTabChange`, `OnResize`.
**Estados:** empty (nenhum selecionado → mostra propriedades do RELATÓRIO), single, multi (mostra intersection).
**Tokens:** `--bg-panel`, `--bg-surface` (header), `--border`, `--accent` (tab indicator).
**Atalhos:** `F12` toggle painel.
**Blazor:** `RD.Designer.Components.RightPanel`.
**Variantes:** —

---

## 24. PropertyGrid
**Propósito:** Lista agrupada e colapsável de propriedades editáveis do elemento selecionado.
**Anatomia:** série de `.prop-section`, cada uma com `.prop-section-head` + `n × .prop-row`.
**Props:**
- `IList<PropertyGroup> Groups`
- `string? FocusedPropertyKey`
**Eventos:** `OnPropertyChange(key, value)`, `OnFocusChange(key)`.
**Estados:** group collapsed/expanded; group "Data" tem fundo accent-soft quando tem bindings.
**Tokens:** `--bg-panel-strong` (header), `--accent-soft` (data section quando bindado).
**Atalhos:** `Tab`/`⇧Tab`, `F4` (expression).
**Blazor:** `RD.Designer.Components.PropertyGrid`. Order: Layout, Appearance, Data, Behavior, Misc.
**Variantes:** Multi (mostra valor comum ou `—` quando divergente).

---

## 25. PropertyGroup
**Propósito:** Header colapsável de um grupo de propriedades.
**Anatomia:** `.prop-section-head` (chevron + nome caps tracked + count).
**Props:**
- `string Title`, `int Count`, `bool Expanded`
- `string? Variant` ("default" | "data" — fundo destacado)
**Eventos:** `OnToggle`.
**Estados:** expanded/collapsed; data variant.
**Tokens:** `--bg-panel-strong`, `--accent-soft`, `--accent` (data variant).
**Atalhos:** —
**Blazor:** `RD.Designer.Components.PropertyGroup`. RenderFragment para children.
**Variantes:** Default, Data.

---

## 26. PropertyRow
**Propósito:** Linha de propriedade — label clicável + editor à direita; hover destaca; focused mostra ring azul.
**Anatomia:** grid `42% / 1fr` — `.p-label` (clicável; ::before `fx` se bound) · `.p-editor` (slot polimórfico).
**Props:**
- `string Key`, `string Label`
- `object Value`
- `PropertyEditor EditorKind`
- `bool IsBound = false`, `bool IsFocused = false`, `bool IsInvalid = false`
- `string? Unit` (mm, px, %, …)
**Eventos:** `OnChange(value)`, `OnFocus`, `OnRequestExpressionEditor`.
**Estados:** default, hover (fundo surface), focused (ring), bound (badge fx), invalid (texto danger).
**Tokens:** `--bg-surface`, `--selection`, `--data-bound`, `--danger`, `--text-tertiary` (unit).
**Atalhos:** `Tab`/`⇧Tab`/`Enter`/`Esc`/`F4`.
**Blazor:** `RD.Designer.Components.PropertyRow`. Despacha pro editor via `DynamicComponent` baseado em EditorKind.
**Variantes:** —

---

## 27. PropertyEditors (família)

Sete editores polimórficos. Todos compartilham o slot `.p-editor`.

### 27.1 TextEditor
**Propósito:** Input de texto simples (Name, Tag).
**Props:** `string Value`, `string? Placeholder`, `int MaxLength`.
**Estados:** default, focus (ring), invalid.
**Blazor:** `RD.Designer.Components.Editors.TextEditor`.

### 27.2 NumberEditor
**Propósito:** Numérico com steppers (chevrons verticais) e unidade.
**Props:** `decimal Value`, `decimal Min, Max, Step`, `string Unit`.
**Estados:** stepper visível só em hover; setas ↑↓ no teclado.
**Blazor:** `RD.Designer.Components.Editors.NumberEditor`.

### 27.3 SelectEditor
**Propósito:** Dropdown nativo estilizado.
**Props:** `IList<SelectOption> Options`, `string Value`.
**Estados:** open (chevron rotacionado).
**Blazor:** `RD.Designer.Components.Editors.SelectEditor`. Custom `<select>` com `appearance:none`.

### 27.4 ColorEditor
**Propósito:** Swatch + hex; clique abre popover com paleta + HSL sliders + recentes + eyedropper.
**Props:** `Color Value`, `IList<Color> RecentColors`, `IList<Color> Palette`.
**Eventos:** `OnChange(Color)`.
**Estados:** popover open.
**Tokens:** `--bg-surface`, `--border`, `--popover-shadow`.
**Blazor:** `RD.Designer.Components.Editors.ColorEditor`. Popover via Tippy.js ou nativo Popover API.

### 27.5 FontEditor
**Propósito:** Combo família + tamanho + toggles B/I/U/S.
**Props:** `FontFamily Family`, `int Size`, `bool Bold, Italic, Underline, Strikethrough`.
**Estados:** toggle on (fundo text-primary).
**Tokens:** `--text-primary` (toggle on).
**Blazor:** `RD.Designer.Components.Editors.FontEditor`.

### 27.6 AlignmentEditor
**Propósito:** Linha de 6 ícones — esq/centro/dir + topo/meio/base.
**Props:** `HAlign HAlign`, `VAlign VAlign`.
**Estados:** is-on (fundo text-primary).
**Blazor:** `RD.Designer.Components.Editors.AlignmentEditor`.

### 27.7 BorderEditor
**Propósito:** Controle composto — cada lado (T/R/B/L) + estilo + cor + espessura.
**Props:** `Border Border` (record com 4 lados).
**Estados:** preview live no canto.
**Blazor:** `RD.Designer.Components.Editors.BorderEditor`. Popover ancorado.

### 27.8 ExpressionEditor (inline)
**Propósito:** Input mono com botão `fx` que abre o ExpressionEditorModal.
**Props:** `string Expression`, `string PropertyName`, `bool IsValid`.
**Eventos:** `OnOpenModal`.
**Estados:** valid (texto data-bound), invalid (texto danger).
**Tokens:** `--data-bound`, `--data-bound-soft`, `--danger`.
**Blazor:** `RD.Designer.Components.Editors.ExpressionEditor`. NÃO confundir com (28).

---

## 28. ExpressionEditorModal
**Propósito:** Popover grande (480×340) ancorado na propriedade — sidebar de Fields/Funcs + textarea com syntax highlight + autocomplete + footer com Validar/Aplicar.
**Anatomia:** `.expr-popover` · `.expr-head` (title + prop) · `.expr-body` (`.expr-side` sidebar + `.expr-edit` editor + `.expr-autocomplete`) · `.expr-foot`.
**Props:**
- `string Expression`
- `string PropertyContext` (ex: `TextBox5 · Text`)
- `IExpressionSchema Schema` (fields, params, functions)
**Eventos:** `OnApply(expr)`, `OnCancel`, `OnValidate` → retorna `ValidationResult`.
**Estados:** typing, autocomplete-open, validating, valid (success green), invalid (danger).
**Tokens:** `--syn-*` (highlighting), `--accent` (cursor caret), `--success`, `--danger`.
**Atalhos:** `⌃Space`, `↑↓` autocomplete, `Tab`/`↵` accept, `⌘↵` apply, `Esc` cancel.
**Blazor:**
- `RD.Designer.Components.ExpressionEditorModal`
- Highlight via Monaco/CodeMirror via JSInterop OU Prism + contenteditable.
- Validação async via `IExpressionService.ValidateAsync()`.
**Variantes:** Modal (centered overlay) — para multi-line longas — quando expand button.

---

## 29. Outline
**Propósito:** Árvore com toda a estrutura do relatório (bandas → elementos); sincronizada bidirecionalmente com seleção do canvas.
**Anatomia:** `.outline-row.{is-band|is-element}.depth-N` (chevron + type-icon + nome + actions).
**Props:**
- `ReportDocument Document`
- `string? SelectedId`
- `IDictionary<string, bool> Expanded`
**Eventos:** `OnSelect(id)`, `OnRename(id, newName)`, `OnReorder`, `OnDelete(id)`.
**Estados:** selected (fundo selection-soft + barra azul à esquerda 2px), hover, renaming (input inline).
**Tokens:** `--selection`, `--selection-soft`, `--accent` (band icon).
**Atalhos:** `F2` rename, `Del` excluir, `↑↓` navegar, `→/←` expand/collapse, `↵` foca canvas.
**Blazor:** `RD.Designer.Components.Outline`. DnD para reordenar.
**Variantes:** —

---

# Overlays

## 30. CommandPalette
**Propósito:** Modal estilo Raycast/Linear — input + lista de resultados com ícone + título + breadcrumb + atalho. Fuzzy search.
**Anatomia:** `.overlay.cmd-overlay` · `.cmdp` · `.cmdp-input` · `.cmdp-list` (grupos: Sugestões / Navegação / Recentes) · `.cmdp-foot`.
**Props:**
- `IList<Command> AllCommands`
- `IList<RecentCommand> Recent`
- `bool Open = false`
**Eventos:** `OnExecute(Command)`, `OnDismiss`.
**Estados:** result highlighted (accent-soft), empty (busca sem matches).
**Tokens:** `--bg-overlay`, `--bg-surface`, `--accent-soft`, `--popover-shadow`.
**Atalhos:** `⌘K`, `↑↓`, `↵`, `⌘↵`, `Esc`, `Tab` (categoria).
**Blazor:**
- `RD.Designer.Components.CommandPalette`
- Fuzzy match via `Fastenshtein` ou substring + scoring.
- Commands registrados via `[Command(Id, Title, Category, Shortcut)]`.
**Variantes:** —

---

## 31. ContextMenu
**Propósito:** Menu de contexto (right-click) com ações apropriadas ao alvo. Ícones + atalhos + submenus em hover.
**Anatomia:** `.ctx-menu` posicionado absolute · `.ctx-item` (ícone + label + shortcut) · `.ctx-sep`.
**Props:**
- `IList<MenuItem> Items` (suporta nested via children)
- `Position Position` (x, y; auto-flip near edges)
- `object? Target` (elemento que originou)
**Eventos:** `OnExecute(string itemId)`, `OnDismiss`.
**Estados:** item disabled, sub-open, separator.
**Tokens:** `--bg-surface`, `--bg-panel` (hover), `--popover-shadow`.
**Atalhos:** `↑↓`, `←→` (sub), `↵`, `Esc`.
**Blazor:** `RD.Designer.Components.ContextMenu`. Auto-flip via `getBoundingClientRect`.
**Variantes:** Por alvo — Element, Band, EmptyCanvas, Tab, Outline, PropertyRow.

---

## 32. PageSetupDialog
**Propósito:** Modal com tamanho de papel (combo + custom), orientação (toggle), margens (4 inputs), colunas, ordem de impressão.
**Anatomia:** `<dialog>` nativo · header · form-grid (size, orientation, margins T/R/B/L, columns, gap) · preview thumb · footer (Cancelar / Aplicar).
**Props:**
- `PageSetup Initial`
**Eventos:** `OnApply(PageSetup)`, `OnCancel`.
**Estados:** dirty (Aplicar habilitado), invalid (margens > metade da página).
**Tokens:** `--bg-surface`, `--popover-shadow`, `--accent` (Apply primary).
**Atalhos:** `↵` Apply, `Esc` Cancel, `Tab` navegar campos.
**Blazor:**
- `RD.Designer.Components.PageSetupDialog`
- `<dialog>` nativo via `Modal.showModal()` JSInterop.
- Preview thumb é SVG ~120×170 que reflete em vivo.
**Variantes:** —

---

## 33. PreviewPane
**Propósito:** Substitui o canvas pelo render real do relatório. Toolbar custom com navegação, zoom, exportar, imprimir.
**Anatomia:** `.preview-region` · `.preview-toolbar` (back + page nav + zoom + search + export + print) · `.preview-scroll` · `.preview-page` (folha renderizada).
**Props:**
- `ReportDocument Document`
- `IList<DocumentParameter> ParameterValues`
- `int CurrentPage = 1`
**Eventos:** `OnExitPreview`, `OnExport(format)`, `OnPrint`, `OnPageChange`.
**Estados:** loading (skeleton), empty (sem dados), normal, error (data source falhou).
**Tokens:** `--bg-panel-strong` (scroll bg), `--accent` (Imprimir primary).
**Atalhos:** `←/→`, `Home/End`, `⌘F`, `⌘P`, `⌘E`, `Esc/F5` (sair).
**Blazor:**
- `RD.Designer.Components.PreviewPane`
- Render via `IReportRenderingService.RenderAsync()` → HTML chunks.
- Exports delegados a `IExportService.ExportPdfAsync/ExcelAsync/HtmlAsync`.
**Variantes:** —

---

## 34. Tooltip
**Propósito:** Micro-tooltip preto/branco invertido (claro no dark, escuro no light). 11px, padding 6×8, raio 4px. 400ms delay.
**Anatomia:** `.tooltip` posicionado absolute via Floating UI.
**Props:**
- `string Content`
- `Position PreferredPosition = Top`
- `int DelayMs = 400`
**Eventos:** —
**Estados:** open / closed.
**Tokens:** `--text-inverted` (bg), `--bg-canvas` (text — inverted).
**Atalhos:** —
**Blazor:**
- `RD.Designer.Components.Tooltip`
- Wrap children + Portal pro `<body>`.
- Detectar `hover` (não `focus-within` — caso a controle queira mostrar próprio popover).
**Variantes:** Default, Rich (com kbd shortcut), Info (com ícone).

---

## 35. Toast
**Propósito:** Notificação de salvamento/erro no canto inferior direito, 4s, com ação inline ("Desfazer").
**Anatomia:** `.toast` (ícone + mensagem + action) — empilhável.
**Props:**
- `ToastKind Kind` (Success | Info | Warning | Danger)
- `string Message`
- `string? ActionLabel`, `Action? OnAction`
- `int DurationMs = 4000`
**Eventos:** `OnDismiss`, `OnAction`.
**Estados:** entering (slide-up + fade), visible, exiting (slide-down + fade).
**Tokens:** `--bg-surface`, `--popover-shadow`, `--success`/`--warning`/`--danger` (ícone).
**Atalhos:** `⌘Z` cumpre o "Desfazer" se ainda visível.
**Blazor:**
- `RD.Designer.Components.Toast`
- Container singleton no AppShell; toasts via `IToastService.Show(ToastModel)`.
**Variantes:** Por Kind.

---

# Tema

## 36. ThemeToggle
**Propósito:** Botão no TopBar que alterna light/dark/system. Transição ~200ms em background/border (não texto).
**Anatomia:** botão com ícone sol/lua dependendo do estado; label opcional.
**Props:**
- `ThemeMode Mode` (`Light` | `Dark` | `System`)
**Eventos:** `OnChange(ThemeMode)`.
**Estados:** light, dark, system (mostra ícone "monitor").
**Tokens:** consome `--bg-surface`, `--border`, `--text-secondary`.
**Atalhos:** `⌥⇧L` light, `⌥⇧D` dark.
**Blazor:**
- `RD.Designer.Components.ThemeToggle`
- Aplica `<html data-theme="...">` via `IThemeService.Set()`.
- Persiste em `localStorage["rd-theme"]`.
- Em modo `System`, observa `prefers-color-scheme` via `matchMedia`.
**Variantes:** Cycle (light→dark→system), Toggle (apenas light↔dark).

---

# Apêndice — Mapa de portabilidade Blazor

Estrutura sugerida do projeto:

```
RD.Designer/
├── Components/
│   ├── AppShell.razor(.cs / .css)
│   ├── TopBar/
│   │   ├── TopBar.razor
│   │   ├── MenuItem.razor
│   │   └── CommandTrigger.razor
│   ├── Canvas/
│   │   ├── CanvasViewport.razor
│   │   ├── Page.razor
│   │   ├── Band.razor
│   │   ├── BandLabel.razor
│   │   ├── ElementCard.razor
│   │   ├── SelectionHandles.razor
│   │   ├── SmartGuides.razor
│   │   ├── MarqueeSelection.razor
│   │   ├── GridOverlay.razor
│   │   ├── ExpressionBadge.razor
│   │   └── Ruler.razor
│   ├── PropertyGrid/
│   │   ├── PropertyGrid.razor
│   │   ├── PropertyGroup.razor
│   │   ├── PropertyRow.razor
│   │   └── Editors/ (8 arquivos)
│   ├── Overlays/
│   │   ├── CommandPalette.razor
│   │   ├── ContextMenu.razor
│   │   ├── PageSetupDialog.razor
│   │   ├── ExpressionEditorModal.razor
│   │   ├── Tooltip.razor
│   │   └── Toast.razor
│   └── Shared/ (StatusBar, TabBar, Toolbar, SplitPane, ThemeToggle)
├── Services/
│   ├── IDocumentService.cs
│   ├── ISelectionService.cs
│   ├── IShortcutService.cs
│   ├── IExpressionService.cs
│   ├── IReportRenderingService.cs
│   ├── IExportService.cs
│   ├── IDataSourceService.cs
│   ├── IToastService.cs
│   └── IThemeService.cs
├── Models/
│   ├── ReportDocument.cs
│   ├── Band.cs (record)
│   ├── ElementModel.cs (abstract base + TextBox, Image, …)
│   ├── PageSetup.cs
│   ├── Style.cs (record)
│   └── Rect.cs (X, Y, W, H em mm)
├── Interop/
│   ├── canvas.js (DnD, pointer events, ruler)
│   ├── shortcuts.js (key normalization)
│   └── floating.js (popover positioning)
└── wwwroot/css/
    ├── tokens.css
    ├── base.css
    └── (componente.razor.css scoped)
```

## Padrões obrigatórios

- **State management:** `Fluxor` ou pattern `IObservable<State>` — mutação em um único lugar; canvas reage.
- **Drag & drop:** HTML5 DnD para Toolbox→Canvas (cross-zone); `pointerdown`/`pointermove` para mover/redimensionar elementos (mais responsivo dentro de uma zona).
- **CSS isolation:** sempre `.razor.css` quando o componente tem visual próprio. Tokens globais ficam em `wwwroot/css/tokens.css`.
- **Keyboard:** `IShortcutService` central com prioridade (modal > overlay > canvas > globais). Cada componente declara seus atalhos via atributo.
- **A11y:** `role`, `aria-label`, `aria-selected`, focus management em modais (focus trap), `prefers-reduced-motion` desabilita transições.
- **Testing:** bUnit para component snapshots; Playwright para o flow end-to-end por estado (os 8 estados do mock viram cenários de teste).
