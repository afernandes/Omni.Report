using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Sales report grouped by customer. Demonstrates: page setup, parameters, header/footer,
/// group header + footer with running aggregates, currency formatting, and totals.
/// </summary>
public static class Sample01_VendasPorCliente
{
    public static Report Build(IEnumerable<Venda>? rows = null) =>
        ReportBuilder
            .Create("Vendas por Cliente")
            .Page(p => p.A4().Portrait().Margins(20))
            .Parameters(p => p
                .Add<DateTime>("DataInicio", prompt: "Data inicial", defaultValue: new DateTime(2026, 5, 1))
                .Add<DateTime>("DataFim",    prompt: "Data final",   defaultValue: new DateTime(2026, 5, 31)))
            .DataSource("Vendas", rows ?? SampleData.Vendas())
            .ReportHeader(h => h.Height(40)
                .Text("Relatório de Vendas")
                    .At(0, 0).Size(170, 12)
                    .Font("Arial", 16, FontStyle.Bold)
                    .Center()
                .Text("Período: {Parameters.DataInicio:dd/MM/yyyy} a {Parameters.DataFim:dd/MM/yyyy}")
                    .At(0, 14).Size(170, 8)
                    .Center()
                .Line().From(0, 22).To(170, 22).Thickness(0.5))
            .PageHeader(h => h.Height(8)
                .Label("Produto")        .At(0, 0).Size(82, 6).Bold()
                .Label("Qtde")           .At(82, 0).Size(20, 6).Bold().AlignRight()
                .Label("Preço Unitário") .At(104, 0).Size(30, 6).Bold().AlignRight()
                .Label("Total")          .At(136, 0).Size(34, 6).Bold().AlignRight()
                .Line().From(0, 6).To(170, 6).Thickness(0.25))
            .Group("PorCliente", "Fields.Cliente", g => g
                .Header(h => h.Height(10)
                    .Text("Cliente: {Fields.Cliente}")
                        .At(0, 2).Size(170, 6)
                        .Font("Arial", 11, FontStyle.Bold)
                        .Color(Color.FromHex("#C2410C")))
                .Detail(d => d.Height(6)
                    .Text("{Fields.Produto}").At(0, 0).Size(80, 6)
                    .Text("{Fields.Quantidade:N2}").At(82, 0).Size(20, 6).AlignRight()
                    .Text("{Fields.PrecoUnitario:C}").At(104, 0).Size(30, 6).AlignRight()
                    .Text("{Fields.Total:C}").At(136, 0).Size(34, 6).AlignRight())
                .Footer(f => f.Height(8)
                    .Line().From(0, 0).To(170, 0).Thickness(0.25)
                    .Text("Subtotal {Fields.Cliente}: {Sum(Fields.Total, 'Group'):C}")
                        .At(0, 1).Size(170, 6).AlignRight().Bold()))
            .ReportFooter(f => f.Height(15)
                .Line().From(0, 0).To(170, 0).Thickness(0.5)
                .Text("Total geral: {Sum(Fields.Total):C}")
                    .At(0, 2).Size(170, 10).Bold().AlignRight()
                    .Font("Arial", 11, FontStyle.Bold))
            .PageFooter(f => f.Height(8)
                .Text("Página {Page.Number} de {Page.Total}")
                    .At(0, 1).Size(170, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();
}
