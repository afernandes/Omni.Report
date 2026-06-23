using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// A LARGE, multi-page grouped sales report (~770 rows over 24 clients). Exercises the paginator end to end:
/// repeating page header + footer ("Página N de M"), per-group header/footer (subtotal), a report header (once)
/// and report footer (grand total, once), and — because a client's rows often outrun a page — the group header
/// REPRINTS at the top of each continuation page (<see cref="GroupBuilder.RepeatHeader"/>).
/// </summary>
public static class Sample16_RelatorioGrandePaginado
{
    public static Report Build(IEnumerable<Venda>? rows = null) =>
        ReportBuilder
            .Create("Relatório Grande Paginado")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", rows ?? Data())
            .ReportHeader(h => h.Height(18)
                .Text("Relatório de Vendas — grande e paginado")
                    .At(0, 0).Size(180, 12).Font("Arial", 16, FontStyle.Bold).Center()
                .Text("{Count(Fields.Total)} linhas · gerado para validar paginação")
                    .At(0, 13).Size(180, 5).Center().Color(Color.Gray))
            .PageHeader(h => h.Height(9)
                .Label("Produto")        .At(0, 0).Size(92, 6).Bold()
                .Label("Qtd")            .At(92, 0).Size(18, 6).Bold().AlignRight()
                .Label("Preço Unitário") .At(112, 0).Size(32, 6).Bold().AlignRight()
                .Label("Total")          .At(146, 0).Size(34, 6).Bold().AlignRight()
                .Line().From(0, 7.5).To(180, 7.5).Thickness(0.3))
            .Group("PorCliente", "Fields.Cliente", g => g
                .RepeatHeader() // reprint the group header on every continuation page (the paginator now honours this)
                .Header(h => h.Height(8)
                    .Text("Cliente: {Fields.Cliente}")
                        .At(0, 1).Size(180, 6).Font("Arial", 11, FontStyle.Bold).Color(Color.FromHex("#C2410C")))
                .Detail(d => d.Height(6)
                    .Text("{Fields.Produto}").At(0, 0).Size(92, 6)
                    .Text("{Fields.Quantidade:N0}").At(92, 0).Size(18, 6).AlignRight()
                    .Text("{Fields.PrecoUnitario:C}").At(112, 0).Size(32, 6).AlignRight()
                    .Text("{Fields.Total:C}").At(146, 0).Size(34, 6).AlignRight())
                .Footer(f => f.Height(8)
                    .Line().From(0, 0).To(180, 0).Thickness(0.2)
                    .Text("Subtotal {Fields.Cliente}: {Sum(Fields.Total, 'Group'):C}")
                        .At(0, 1).Size(180, 6).AlignRight().Bold()))
            .ReportFooter(f => f.Height(12)
                .Line().From(0, 0).To(180, 0).Thickness(0.5)
                .Text("TOTAL GERAL: {Sum(Fields.Total):C}")
                    .At(0, 2).Size(180, 10).Font("Arial", 12, FontStyle.Bold).AlignRight())
            .PageFooter(f => f.Height(8)
                .Text("Página {Page.Number} de {Page.Total}")
                    .At(0, 1).Size(180, 6).AlignRight().Color(Color.Gray))
            .Build();

    /// <summary>~770 deterministic rows across 24 clients (some with enough rows to outrun a page).</summary>
    public static IReadOnlyList<Venda> Data()
    {
        var rng = new Random(42);
        var baseDate = new DateTime(2026, 1, 1);
        var rows = new List<Venda>();
        for (var c = 1; c <= 24; c++)
        {
            var cliente = $"Cliente {c:D2}";
            var count = 18 + rng.Next(28); // 18..45 rows per client → groups frequently span page breaks
            for (var i = 0; i < count; i++)
            {
                rows.Add(new Venda(
                    cliente,
                    $"Produto {c:D2}-{i:D3}",
                    1 + rng.Next(20),
                    Math.Round((decimal)(50 + rng.Next(4950)) / 100m, 2),
                    baseDate.AddDays(rng.Next(180))));
            }
        }
        return rows;
    }
}
