# Design Tokens — Report Designer

Todos os tokens vivem em `styles/tokens.css`. O sistema usa CSS Custom Properties que respondem ao seletor `[data-theme="light"|"dark"]` no `<html>`. A paleta escolhida é a **"Print Studio"** (Opção A do brief) — referência semântica ao mundo da impressão.

---

## 🎨 Cores — Superfícies

| Token | Light | Dark | Uso |
|---|---|---|---|
| `--bg-canvas` | `#FAFAF7` | `#1A1A1A` | Fundo do canvas; "papel" |
| `--bg-surface` | `#FFFFFF` | `#232323` | Cartões, popovers, inputs |
| `--bg-panel` | `#F4F2EC` | `#1E1E1E` | Painéis laterais, top bar |
| `--bg-panel-strong` | `#ECE9E0` | `#181818` | Tab bar, status bar, headers de grupo |
| `--bg-overlay` | `rgba(20,18,14,0.32)` | `rgba(0,0,0,0.6)` | Scrim modal/overlay |

## 🎨 Cores — Bordas e divisores

| Token | Light | Dark |
|---|---|---|
| `--border` | `#E5E2D9` | `#2E2E2E` |
| `--border-soft` | `#EFEDE5` | `#262626` |
| `--border-strong` | `#C7C2B3` | `#3D3D3D` |
| `--border-divider` | `#DBD7CB` | `#353535` |

## 🎨 Cores — Texto

| Token | Light | Dark |
|---|---|---|
| `--text-primary` | `#1A1A1A` | `#F0EFE9` |
| `--text-secondary` | `#5A5750` | `#A8A59C` |
| `--text-tertiary` | `#8E8A7E` | `#6F6C64` |
| `--text-inverted` | `#FAFAF7` | `#1A1A1A` |

## 🎨 Cores — Acento (Print Studio "Ferrugem")

| Token | Light | Dark |
|---|---|---|
| `--accent` | `#C2410C` | `#F97316` |
| `--accent-hover` | `#9A3209` | `#FB923C` |
| `--accent-soft` | `#FFF1E6` | `#3A1F0E` |
| `--accent-border` | `#F5C6A4` | `#7A3812` |

## 🎨 Cores — Semânticas

| Token | Light | Dark | Uso |
|---|---|---|---|
| `--data-bound` | `#0F766E` | `#14B8A6` | Indicador de binding (badge fx, listras) |
| `--data-bound-soft` | `#D6F0EC` | `#0B3D38` | Fundo do badge `fx` |
| `--selection` | `#3B82F6` | `#60A5FA` | Borda + handles de seleção (Figma-blue) |
| `--selection-soft` | `rgba(59,130,246,0.10)` | `rgba(96,165,250,0.14)` | Fill da marquee |
| `--selection-strong` | `#1D4ED8` | `#93C5FD` | Texto sobre selection-soft |
| `--danger` | `#B91C1C` | `#EF4444` | Erros |
| `--danger-soft` | `#FEE2E2` | `#3C1414` | Fundo de erro |
| `--warning` | `#A16207` | `#EAB308` | Atenção |
| `--warning-soft` | `#FEF3C7` | `#3A2C09` | Fundo |
| `--success` | `#047857` | `#34D399` | Validações OK |
| `--smart-guide` | `#E879F9` | `#F0ABFC` | Linhas magenta de alinhamento |

## 🎨 Cores — Específicas de componente

| Token | Light | Dark |
|---|---|---|
| `--band-strip` | `#F0EDE4` | `#1B1B1B` |
| `--band-strip-hover` | `#E8E4D8` | `#242424` |
| `--ruler-bg` | `#F4F2EC` | `#1B1B1B` |
| `--ruler-tick` | `#A8A395` | `#4F4C44` |
| `--ruler-text` | `#6B675D` | `#8A867B` |
| `--grid-dot` | `#D8D4C7` | `#2E2E2E` |

## 🎨 Cores — Syntax highlighting (Expression Editor)

| Token | Light | Dark |
|---|---|---|
| `--syn-keyword` | `#9333EA` | `#C084FC` |
| `--syn-function` | `#C2410C` | `#F97316` |
| `--syn-string` | `#047857` | `#34D399` |
| `--syn-number` | `#1D4ED8` | `#93C5FD` |
| `--syn-field` | `#0F766E` | `#14B8A6` |
| `--syn-comment` | `#8E8A7E` | `#6F6C64` |

---

## 🔤 Tipografia

| Token | Valor |
|---|---|
| `--font-sans` | `"IBM Plex Sans", -apple-system, "Segoe UI Variable", system-ui, sans-serif` |
| `--font-mono` | `"IBM Plex Mono", "JetBrains Mono", ui-monospace, "SF Mono", Menlo, monospace` |

### Escala

| Token | px | Uso |
|---|---|---|
| `--fs-xxs` | 10px | Régua, micro-labels |
| `--fs-xs` | 11px | Headers de painel, labels secundários, status bar |
| `--fs-sm` | 12px | Property grid, mono inline |
| `--fs-md` | 13px | UI base — botões, inputs, menus |
| `--fs-lg` | 14px | Headers de relatório |
| `--fs-xl` | 16px | Títulos no canvas |

### Line-height & tracking

| Token | Valor |
|---|---|
| `--lh-ui` | `1.4` (UI densa) |
| `--lh-text` | `1.6` (conteúdo editável) |
| `--tracked` | `0.04em` (caps de painel) |
| `--tracked-loose` | `0.08em` (raro, headers de seção) |

### Pesos

- **400** — body, valores em property grid
- **500** — labels, menus, botões
- **600** — Headings, valores destacados, panel headers
- **700** — Title em ReportHeader, KBD, badges fx

---

## 📐 Espaçamento (sistema 4px)

| Token | Valor | Uso típico |
|---|---|---|
| `--sp-0` | `0` | — |
| `--sp-1` | `4px` | Gap entre ícones no toolbar |
| `--sp-2` | `8px` | Gap entre itens próximos |
| `--sp-3` | `12px` | Padding de menu items, gap entre grupos |
| `--sp-4` | `16px` | Separação entre regiões |
| `--sp-5` | `20px` | (uso pontual) |
| `--sp-6` | `24px` | Padding do canvas stage |
| `--sp-8` | `32px` | Separações fortes |
| `--sp-10` | `48px` | Padding bottom do canvas |

> Em UI densa, a maioria dos gaps fica entre 4–12px. Use 16–24px apenas para separar regiões.

---

## 🔘 Raios

| Token | Valor | Uso |
|---|---|---|
| `--r-1` | `3px` | Kbd, badges, chips muito pequenos |
| `--r-2` | `4px` | Botões, inputs, controles |
| `--r-3` | `6px` | Cards, command palette, dropdowns |
| `--r-4` | `8px` | Modais |
| `--r-pill` | `999px` | Tags raras |

> Nada de raios grandes em ferramentas densas.

---

## 📏 Alturas fixas

| Token | Valor | Uso |
|---|---|---|
| `--h-statesbar` | `32px` | Strip de demo (não vai pra produção) |
| `--h-topbar` | `44px` | Top bar com menu, palette, conta |
| `--h-tabbar` | `36px` | Abas de relatórios abertos |
| `--h-toolbar` | `40px` | Toolbar de ações |
| `--h-statusbar` | `24px` | Status bar inferior |
| `--h-row` | `24px` | Linha do property grid |
| `--h-control` | `24px` | Inputs, dropdowns small |
| `--h-control-lg` | `28px` | Botões da toolbar |

---

## 🌫️ Sombras

| Token | Light | Dark |
|---|---|---|
| `--page-shadow` | `0 1px 0 rgba(20,18,14,0.04), 0 4px 18px rgba(20,18,14,0.06)` | `0 0 0 1px #2A2A2A, 0 16px 40px rgba(0,0,0,0.5)` |
| `--popover-shadow` | `0 1px 0 rgba(20,18,14,0.06), 0 8px 28px rgba(20,18,14,0.14), 0 24px 56px -8px rgba(20,18,14,0.18)` | `0 0 0 1px #303030, 0 16px 40px rgba(0,0,0,0.55)` |

> Em dark, sombras viram **bordas** — o eye não enxerga "lift" no escuro.

---

## ⏱️ Animação

| Token | Valor | Uso |
|---|---|---|
| `--t-fast` | `120ms` | Hover, focus, micro-feedback |
| `--t-base` | `200ms` | Theme toggle, abertura de palette |
| `--t-slow` | `320ms` | Slide-in de painéis |
| `--ease-out` | `cubic-bezier(0.22, 1, 0.36, 1)` | Entradas |
| `--ease-in-out` | `cubic-bezier(0.65, 0, 0.35, 1)` | Transições contínuas |

---

## 📃 Geometria do canvas

| Token | Valor | Notas |
|---|---|---|
| `--page-w` | `720px` | Largura visual da página (representa A4 = 210mm) |
| `--page-h` | `1018px` | Altura (A4 = 297mm, mesma proporção) |
| `--mm` | `3.43px` | 720 / 210 — fator de conversão mm→px |
| `--band-strip-w` | `38px` | Largura do header strip à esquerda da banda |
| `--ruler-size` | `22px` | Espessura da régua superior/lateral |

---

## 🎯 Como adicionar um novo token

1. Adicione em ambos os blocos (`:root, [data-theme="light"]` e `[data-theme="dark"]`).
2. Nomeie por **função**, não cor (`--accent` ✓, não `--orange`).
3. Verifique contraste WCAG AA (4.5:1 para texto normal, 3:1 para large/UI).
4. Documente aqui antes de usar.

> Nunca escreva um valor de cor literal num componente — sempre via `var(--*)`.
