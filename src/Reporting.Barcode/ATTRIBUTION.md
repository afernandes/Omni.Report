# Attribution

The barcode and QR encoding algorithms in this assembly are ports of
[Radzen.Blazor](https://github.com/radzenhq/radzen-blazor)'s
`RadzenBarcodeEncoder` and `RadzenQREncoder`, originally licensed under MIT.

We chose to port (rather than depend on the NuGet package) because:

1. **Zero dependencies** — the algorithms are pure managed math; pulling a
   2-MB Blazor UI library just for two static classes is overkill.
2. **Decoupled from UI** — Radzen ships these inside a Blazor component
   library. We extract the algorithms so any renderer (Skia, GDI, ESC/POS,
   server-side SVG) can consume them.
3. **Stable surface** — the encoders are deterministic and don't change
   often; vendoring keeps our PDF/PNG snapshots reproducible.

The original Radzen.Blazor MIT license terms are preserved alongside this
notice (see `LICENSE-Radzen.txt`).

## What we kept

- `Code128B`, `Code39`, `Codabar`, `ITF`, `EAN-13`, `EAN-8`, `UPC-A`,
  `ISBN`-as-EAN-13, `ISSN`-as-EAN-13.
- Full QR encoding (versions 1–40, ECC levels L/M/Q/H), Reed-Solomon over
  GF(256), all 8 mask patterns + penalty selection, BCH format/version info.

## What we left out

- `Pharmacode`, `POSTNET`, `RM4SCC`, `MSI/Plessey`, `Telepen` — rare
  symbologies; can be added later if needed.
- QR styling (rounded eyes, custom shapes, embedded logo) — purely a
  rendering concern handled at the renderer layer.
- SVG generation — Radzen's encoders emit SVG strings; ours emit raw
  geometry (`BarcodeRect[]` / `bool[,]`) so the host renderer draws
  natively. This keeps PDF vector-perfect and avoids string parsing.
