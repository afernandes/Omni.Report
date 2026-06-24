using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Showcase of the visual-styling features: a report-level <b>named style</b> reused via
/// <c>BasedOn</c>, a two-colour <b>background gradient</b> banner, and a <b>matrix/crosstab</b>
/// whose value cells carry their own style (right-aligned, coloured). Everything renders
/// statically — no runtime interactivity.
/// </summary>
public static class Sample17_EstilosVisuais
{
    public static Report Build(IEnumerable<Venda>? rows = null) =>
        ReportBuilder
            .Create("Estilos Visuais")
            .Page(p => p.A4().Portrait().Margins(20))
            .DataSource("Vendas", rows ?? SampleData.Vendas())
            // Reusable named style — define once, apply by name (BasedOn). Inheritable layout (centred) + colour.
            .NamedStyle("titulo", s => s with
            {
                ForeColor = Color.FromHex("#1E3A8A"), // navy
                Font = new Font("Arial", 18, FontStyle.Bold),
                HorizontalAlignment = HorizontalAlignment.Center,
            })
            // The banner + subtitle + crosstab live in the ReportHeader so the matrix renders ONCE
            // (a Tablix in the repeating Detail band would re-render per data row).
            .ReportHeader(h => h.Height(112)
                // Gradient banner: orange → red, top to bottom, behind the title text.
                .Text("Relatório de Estilos")
                    .At(0, 0).Size(170, 16)
                    .BackgroundGradient(Color.FromHex("#C2410C"), Color.FromHex("#7F1D1D"), BackgroundGradientType.TopBottom)
                    .Color(Color.White)
                    .Font("Arial", 18, FontStyle.Bold)
                    .Center()
                // Subtitle inherits the "titulo" named style, then overrides the size inline.
                .Text("Vendas por cliente × produto")
                    .At(0, 20).Size(170, 8)
                    .BasedOn("titulo")
                    .FontSize(12)
                .Line().From(0, 30).To(170, 30).Thickness(0.5)
                // Matrix: Cliente (rows) × Produto (cols), summing Total. The value cells are
                // right-aligned and coloured via the styled .Cell — the matrix renderer honours it.
                .Tablix(t => t
                    .RowGroup("Fields.Cliente")
                    .ColumnGroup("Fields.Produto")
                    .Corner("Cliente \\ Produto")
                    .Cell("Fields.Total", s => s with
                    {
                        ForeColor = Color.FromHex("#166534"), // green-800
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Format = "C",
                    }))
                    .At(0, 36).Size(170, 72))
            // No per-row detail — the crosstab above aggregates the whole dataset.
            .Detail(d => d.Height(0))
            .PageFooter(f => f.Height(8)
                .Text("Página {Page.Number} de {Page.Total}")
                    .At(0, 1).Size(170, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();
}
