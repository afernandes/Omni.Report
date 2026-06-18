using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Visual dashboard exercising every new chart/KPI element on one page:
/// bar + pie + line charts, a radial gauge with coloured bands, an area sparkline, a directional
/// KPI indicator, and a per-row data bar in the detail band. All read the same "Mensal" source.
/// </summary>
public static class Sample12_Dashboard
{
    public static Report Build(IEnumerable<VendaMensal>? rows = null) =>
        ReportBuilder
            .Create("Dashboard de Vendas")
            .Page(p => p.A4().Portrait().Margins(18))
            .DataSource("Mensal", rows ?? DashboardData.Mensal())
            .ReportHeader(h => h.Height(182)
                .Text("Dashboard de Vendas — 1º semestre")
                    .At(0, 0).Size(174, 10).Font("Arial", 15, FontStyle.Bold).Center()

                // Row 1 — bar (left) + pie (right)
                .Chart(ChartKind.Bar, "Faturamento por mês")
                    .At(0, 14).Size(86, 56)
                    .Series("Total", "Fields.Mes", "Fields.Total")
                .Chart(ChartKind.Pie, "Participação por mês")
                    .At(90, 14).Size(84, 56)
                    .Series("Total", "Fields.Mes", "Fields.Total")

                // Row 2 — line with two series (realized vs. target)
                .Chart(ChartKind.Line, "Realizado vs. Meta")
                    .At(0, 74).Size(174, 52)
                    .Series("Realizado", "Fields.Mes", "Fields.Total")
                    .Series("Meta", "Fields.Mes", "Fields.Meta")

                // Row 3 — gauge + sparkline + KPI indicator
                .Gauge("Sum(Fields.Total)")
                    .At(0, 130).Size(60, 50)
                    .Range(0, 300000)
                    .GaugeBand(0, 150000, "#DC2626")
                    .GaugeBand(150000, 250000, "#F59E0B")
                    .GaugeBand(250000, 300000, "#16A34A")
                .Label("Tendência (sparkline)")
                    .At(66, 130).Size(60, 5).Font("Arial", 9, FontStyle.Bold)
                .Sparkline("Fields.Total", SparklineKind.Area)
                    .At(66, 136).Size(60, 26)
                .Label("Meta do semestre")
                    .At(132, 130).Size(42, 5).Font("Arial", 9, FontStyle.Bold)
                .Indicator("Sum(Fields.Total)", IndicatorKind.DirectionalArrow)
                    .At(146, 138).Size(16, 16)
                    .State(0, 150000).State(150000, 250000).State(250000, 400000))

            .PageHeader(h => h.Height(7)
                .Label("Mês").At(0, 0).Size(24, 6).Bold()
                .Label("Faturamento").At(24, 0).Size(40, 6).Bold().AlignRight()
                .Label("Progresso (teto R$ 55.000)").At(70, 0).Size(104, 6).Bold()
                .Line().From(0, 6).To(174, 6).Thickness(0.25))
            .Detail(d => d.Height(7)
                .Text("{Fields.Mes}").At(0, 0).Size(24, 6)
                .Text("{Fields.Total:C}").At(24, 0).Size(40, 6).AlignRight()
                .DataBar("Fields.Total", "#2563EB").At(70, 1).Size(104, 4).Range(0, 55000))
            .ReportFooter(f => f.Height(10)
                .Line().From(0, 0).To(174, 0).Thickness(0.5)
                .Text("Total do semestre: {Sum(Fields.Total):C}")
                    .At(0, 2).Size(174, 7).Font("Arial", 11, FontStyle.Bold).AlignRight())
            .Build();
}
