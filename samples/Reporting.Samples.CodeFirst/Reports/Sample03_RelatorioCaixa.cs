using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Daily cashier movement grouped by payment method. Demonstrates group footer totals
/// in pt-BR currency formatting and per-payment-method subtotals.
/// </summary>
public static class Sample03_RelatorioCaixa
{
    public static Report Build(IEnumerable<CaixaMovimento>? rows = null) =>
        ReportBuilder
            .Create("Movimento de Caixa")
            .Page(p => p.A4().Portrait().Margins(20))
            .Parameters(p => p
                .Add<DateTime>("Data", prompt: "Data do movimento", defaultValue: new DateTime(2026, 5, 23)))
            .DataSource("Caixa", rows ?? SampleData.Caixa())
            .ReportHeader(h => h.Height(25)
                .Text("Movimento de Caixa")
                    .At(0, 0).Size(170, 12)
                    .Font("Arial", 16, FontStyle.Bold)
                    .Center()
                .Text("Data: {Parameters.Data:dd/MM/yyyy}")
                    .At(0, 13).Size(170, 6)
                    .Center()
                .Line().From(0, 22).To(170, 22).Thickness(0.5))
            .Group("PorFormaPagamento", "Fields.FormaPagamento", g => g
                .Header(h => h.Height(10)
                    .Rectangle().At(0, 1).Size(170, 7).Fill(Color.FromHex("#F4F2EC"))
                    .Text("Forma de pagamento: {Fields.FormaPagamento}")
                        .At(2, 2).Size(170, 5)
                        .Font("Arial", 10, FontStyle.Bold)
                        .Color(Color.FromHex("#C2410C")))
                .Detail(d => d.Height(6)
                    .Text("{Fields.Data:HH:mm}").At(2, 0).Size(20, 6)
                        .Font("Consolas", 9)
                    .Text("{Fields.Documento}").At(24, 0).Size(60, 6)
                    .Text("{Fields.FormaPagamento}").At(86, 0).Size(50, 6)
                        .Color(Color.Gray)
                    .Text("{Fields.Valor:C}").At(138, 0).Size(30, 6).AlignRight())
                .Footer(f => f.Height(8)
                    .Line().From(0, 0).To(170, 0).Thickness(0.25)
                    .Text("Subtotal: {Sum(Fields.Valor, 'Group'):C} · {Count(Fields.Valor, 'Group')} movimento(s)")
                        .At(0, 2).Size(170, 5).AlignRight().Bold()))
            .ReportFooter(f => f.Height(18)
                .Line().From(0, 0).To(170, 0).Thickness(0.5)
                .Text("Total do dia: {Sum(Fields.Valor):C}")
                    .At(0, 3).Size(170, 8).AlignRight()
                    .Font("Arial", 12, FontStyle.Bold)
                .Text("Movimentos: {Count(Fields.Valor)} · Maior: {Max(Fields.Valor):C} · Menor: {Min(Fields.Valor):C}")
                    .At(0, 12).Size(170, 5).AlignRight().Color(Color.Gray))
            .PageFooter(f => f.Height(8)
                .Text("OmniReport · {Now:dd/MM/yyyy HH:mm}")
                    .At(0, 1).Size(85, 5).Color(Color.Gray).Font("Arial", 8)
                .Text("Página {Page.Number} de {Page.Total}")
                    .At(85, 1).Size(85, 5).AlignRight().Color(Color.Gray).Font("Arial", 8))
            .Build();
}
