using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Demonstrates the opt-in Roslyn Code feature: a C# helper block is compiled at runtime and its
/// methods are called from expressions as <c>Code.MethodName(...)</c>. The runner wires the
/// resolver via <c>PaginationRequest.CodeFunctionResolver = RoslynCode.CreateResolver(CodeSource)</c>.
/// </summary>
/// <remarks>
/// ⚠ Executes C# embedded in the report definition — only enable for report sources you trust.
/// </remarks>
public static class Sample15_CodigoCustomizado
{
    /// <summary>The C# Code block compiled by the Roslyn package and exposed as <c>Code.*</c>.</summary>
    public const string CodeSource = @"
        public decimal Imposto(decimal valor) => System.Math.Round(valor * 0.18m, 2);
        public decimal Liquido(decimal valor) => valor - Imposto(valor);
        public string Faixa(decimal valor) => valor >= 1000m ? ""Alto"" : ""Padrão"";
    ";

    public static Report Build(IEnumerable<ItemTributo>? rows = null) =>
        ReportBuilder
            .Create("Tributos (Code / Roslyn)")
            .Page(p => p.A4().Portrait().Margins(18))
            .DataSource("Itens", rows ?? DashboardData.ItensTributo())
            .ReportHeader(h => h.Height(22)
                .Text("Cálculo de Tributos com Code customizado (C#)")
                    .At(0, 0).Size(174, 10).Font("Arial", 14, FontStyle.Bold).Center()
                .Text("As colunas chamam Code.Imposto / Code.Liquido / Code.Faixa — compilado via Roslyn (opt-in)")
                    .At(0, 12).Size(174, 6).Center().Color(Color.Gray))
            .PageHeader(h => h.Height(7)
                .Label("Produto").At(0, 0).Size(58, 6).Bold()
                .Label("Valor").At(58, 0).Size(28, 6).Bold().AlignRight()
                .Label("Imposto 18%").At(88, 0).Size(28, 6).Bold().AlignRight()
                .Label("Líquido").At(118, 0).Size(28, 6).Bold().AlignRight()
                .Label("Faixa").At(148, 0).Size(26, 6).Bold().AlignRight()
                .Line().From(0, 6).To(174, 6).Thickness(0.25))
            .Detail(d => d.Height(6)
                .Text("{Fields.Produto}").At(0, 0).Size(58, 6)
                .Text("{Fields.Valor:C}").At(58, 0).Size(28, 6).AlignRight()
                .Text("{Code.Imposto(Fields.Valor):C}").At(88, 0).Size(28, 6).AlignRight()
                .Text("{Code.Liquido(Fields.Valor):C}").At(118, 0).Size(28, 6).AlignRight()
                .Text("{Code.Faixa(Fields.Valor)}").At(148, 0).Size(26, 6).AlignRight())
            .ReportFooter(f => f.Height(10)
                .Line().From(0, 0).To(174, 0).Thickness(0.5)
                .Text("Total dos valores: {Sum(Fields.Valor):C}")
                    .At(0, 2).Size(174, 7).Font("Arial", 11, FontStyle.Bold).AlignRight())
            .Build();
}
