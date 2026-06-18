using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Product mirror: code, description, EAN-13 barcode field (text-only in stage 3, full
/// barcode rendering arrives with the barcode element implementation), wholesale + retail
/// prices. Demonstrates conditional formatting (highlighting expensive items).
/// </summary>
public static class Sample02_EspelhoProdutos
{
    public static Report Build(IEnumerable<Produto>? rows = null) =>
        ReportBuilder
            .Create("Espelho de Produtos")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Produtos", rows ?? SampleData.Produtos())
            .ReportHeader(h => h.Height(20)
                .Text("Espelho de Produtos")
                    .At(0, 0).Size(180, 12)
                    .Font("Arial", 18, FontStyle.Bold)
                    .Center()
                .Text("Atualizado em {Today:dd/MM/yyyy}")
                    .At(0, 13).Size(180, 6)
                    .Center().Color(Color.Gray))
            .PageHeader(h => h.Height(8)
                .Rectangle().At(0, 0).Size(180, 6).Fill(Color.FromHex("#F4F2EC"))
                .Label("Código").At(2, 0).Size(15, 6).Bold().AlignMiddle()
                .Label("Descrição").At(20, 0).Size(85, 6).Bold().AlignMiddle()
                .Label("EAN-13").At(108, 0).Size(28, 6).Bold().AlignMiddle()
                .Label("Atacado").At(138, 0).Size(20, 6).Bold().AlignMiddle().AlignRight()
                .Label("Varejo").At(160, 0).Size(20, 6).Bold().AlignMiddle().AlignRight())
            .Detail(d => d.Height(7)
                .Text("{Fields.Codigo}").At(2, 0).Size(15, 7).AlignMiddle()
                .Text("{Fields.Descricao}").At(20, 0).Size(85, 7).AlignMiddle()
                .Text("{Fields.Ean13}").At(108, 0).Size(28, 7)
                    .Font("Consolas", 9).AlignMiddle()
                .Text("{Fields.PrecoAtacado:C}").At(138, 0).Size(20, 7).AlignRight().AlignMiddle()
                .Text("{Fields.PrecoVarejo:C}").At(160, 0).Size(20, 7).AlignRight().AlignMiddle()
                .ConditionalFormat("Fields.PrecoVarejo > 100",
                    new Style(ForeColor: Color.FromHex("#C2410C"), Font: new Font("Arial", 10, FontStyle.Bold))))
            .ReportFooter(f => f.Height(10)
                .Line().From(0, 0).To(180, 0).Thickness(0.25)
                .Text("Total de itens: {Count(Fields.Codigo)} · Maior preço: {Max(Fields.PrecoVarejo):C}")
                    .At(0, 2).Size(180, 6).AlignRight().Color(Color.Gray))
            .PageFooter(f => f.Height(6)
                .Text("OmniReport — Espelho de Produtos · Página {Page.Number}/{Page.Total}")
                    .At(0, 0).Size(180, 6).Center().Color(Color.Gray)
                    .Font("Arial", 8))
            .Build();
}
