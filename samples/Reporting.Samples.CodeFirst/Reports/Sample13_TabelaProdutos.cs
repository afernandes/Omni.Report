using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Tablix (RDL Table data region) over the product master: a header row plus one detail row per
/// product, with gridlines, built with the fluent <c>.Tablix(t =&gt; t.Column(...))</c> surface.
/// </summary>
public static class Sample13_TabelaProdutos
{
    public static Report Build(IEnumerable<Produto>? rows = null) =>
        ReportBuilder
            .Create("Catálogo de Produtos (Tablix)")
            .Page(p => p.A4().Portrait().Margins(18))
            .DataSource("Produtos", rows ?? SampleData.Produtos())
            .ReportHeader(h => h.Height(95)
                .Text("Catálogo de Produtos")
                    .At(0, 0).Size(174, 10).Font("Arial", 15, FontStyle.Bold).Center()
                .Text("Tablix — cabeçalho + uma linha por registro, com gridlines")
                    .At(0, 11).Size(174, 6).Center().Color(Color.Gray)
                // Relative column weights give "Descrição" extra room so longer names don't
                // bleed into the next column; the remaining columns share the rest evenly.
                .Tablix(t => t
                    .Column("Código", "Fields.Codigo", 1.0)
                    .Column("Descrição", "Fields.Descricao", 2.6)
                    .Column("EAN-13", "Fields.Ean13", 1.5)
                    .Column("Varejo", "{Fields.PrecoVarejo:C}", 1.1)
                    .Column("Atacado", "{Fields.PrecoAtacado:C}", 1.1))
                    .At(0, 20).Size(174, 72))
            // The Tablix renders the data; the detail band stays empty (zero height).
            .Detail(d => d.Height(0))
            .Build();
}
