# Especificação do formato OmniReport

Especificação formal do **formato/modelo OmniReport** — o análogo, para o nosso formato nativo, do que a [MS-RDL] é para o RDL. Define normativamente o modelo de objetos, as serializações nativas (`.repx`/`.repjson`), a linguagem de expressão, a semântica de layout e o interop com RDL.

**Versão 1.0** · alinhada a `ReportDefinition.SchemaVersion = "1.0"`.

## Princípio central

O **modelo nativo imutável é a fonte da verdade**. Code-first, low-level e Designer produzem o mesmo `ReportDefinition`; `.repx`/`.repjson` são projeções lossless dele; e o **RDL é interop/projeção** (lossless via `<CustomProperties>`, válido pelo XSD, abre no SSRS). A saída é sempre **estática**. Detalhes em [§0 Introdução e escopo](00-overview.md).

## Sumário

| § | Documento | Resumo |
|---|---|---|
| 0 | [Introdução e escopo](00-overview.md) | Filosofia, invariante estática, convenções, conformância. |
| 1 | [Modelo de objetos: `ReportDefinition`](01-object-model.md) | Record raiz, árvore do modelo, `PageSetup`, igualdade estrutural. |
| 2 | [Bandas e layout](02-bands-and-layout.md) | Bandas e `BandKind`; paginação (Measure≡Render, split, CanShrink). |
| 3 | [Report items (`ReportElement`)](03-report-items.md) | Hierarquia de elementos + propriedades comuns. |
| 4 | [Modelo de dados](04-data-model.md) | Fontes, campos, campos calculados, relações, parâmetros, variáveis. |
| 5 | [Estilo e geometria](05-styling-and-geometry.md) | `Style`/`Font`/`Color`/`Border`, `Format`, `Unit`/`Rectangle`. |
| 6 | [Linguagem de expressão](06-expression-language.md) | Dialeto OmniReport: campos, agregados/escopos, builtins, templates. |
| 7 | [Serialização](07-serialization.md) | Estrutura `.repx`/`.repjson`, auto-wiring, round-trip. |
| 8 | [Interop com RDL](08-rdl-interop.md) | Mapeamento elemento→RDL, conversão de dialeto, `<CustomProperties>`. |

## Documentos relacionados

- [`docs/rdl-spec-compliance.md`](../rdl-spec-compliance.md) — matriz de cobertura RDL (render × round-trip), por elemento.

---

[MS-RDL]: https://learn.microsoft.com/en-us/openspecs/sql_server_protocols/ms-rdl/
