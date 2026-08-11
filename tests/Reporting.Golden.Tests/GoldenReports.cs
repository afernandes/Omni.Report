using System.Globalization;
using Reporting.CodeFirst;
using Reporting.Styling;

namespace Reporting.Golden.Tests;

internal sealed record Venda(string Produto, string Regiao, int Quantidade, decimal Valor);

/// <summary>
/// The catalogue of reports the golden suite pins. Each one is deliberately small and targets a
/// distinct slice of the engine, so a diff points at the subsystem that moved instead of at "the
/// layout changed".
/// </summary>
/// <remarks>
/// <para>Adding a report here is cheap and welcome; the rule is that it must stay
/// <em>deterministic</em>. No <c>DateTime.Now</c>, no random data, no real font measurement — see
/// <see cref="DisplayList"/>.</para>
///
/// <para><b>Every report pins an explicit culture.</b> Without one the format strings resolve
/// against the machine's culture, so <c>Format("N2")</c> writes <c>1.450,90</c> on a pt-BR
/// workstation and <c>1,450.90</c> on a CI runner — a golden that is green for the author and
/// permanently red in CI. Culture-specific formatting is a concern for the formatter's own tests,
/// which compare against what the culture renders rather than against a literal; here it would only
/// be noise on top of the layout signal.</para>
///
/// <para>The pin is <c>en-US</c> rather than <see cref="CultureInfo.InvariantCulture"/>, which would
/// be the natural choice: <c>ReportBuilderRoot.Culture(CultureInfo.InvariantCulture)</c> throws,
/// because it forwards the invariant culture's empty <c>Name</c> into <c>Language(string)</c> and
/// that guard rejects empty. Storing the empty name would not help either — the paginator maps a
/// blank <c>Metadata["Language"]</c> back to "no culture given" and falls through to the ambient
/// culture, i.e. silently the opposite of what the caller asked for. Real invariant support needs a
/// sentinel understood on both sides; tracked in the roadmap.</para>
/// </remarks>
internal static class GoldenReports
{
    /// <summary>Pinned so number/date formatting is identical on every machine — see the remarks.</summary>
    private static ReportBuilderRoot New(string name) =>
        ReportBuilder.Create(name).Culture(CultureInfo.GetCultureInfo("en-US"));

    public static readonly Venda[] Vendas =
    [
        new("Teclado", "Sul", 12, 1_450.90m),
        new("Monitor", "Sul", 3, 2_310.00m),
        new("Mouse", "Norte", 25, 875.25m),
        new("Headset", "Norte", 7, 1_099.99m),
    ];

    /// <summary>Band stacking end-to-end: report header, repeating page header, detail per row,
    /// page footer with page numbering. The vertical positions in the golden encode the whole
    /// band-flow contract.</summary>
    public static Report Bandas() => New("Bandas")
        .Page(p => p.A4().Portrait().Margins(15))
        .DataSource("Vendas", Vendas)
        .ReportHeader(h => h.Height(14)
            .Label("Relatório de vendas").At(0, 0).Size(120, 8).Font("Arial", 16, FontStyle.Bold))
        .PageHeader(h => h.Height(8)
            .Label("Produto").At(0, 0).Size(50, 5).Bold()
            .Label("Região").At(50, 0).Size(35, 5).Bold()
            .Label("Qtd").At(85, 0).Size(20, 5).Bold().AlignRight()
            .Label("Valor").At(105, 0).Size(30, 5).Bold().AlignRight()
            .Line().At(0, 6).Size(135, 0))
        .Detail(d => d.Height(6)
            .Text("{Fields.Produto}").At(0, 0).Size(50, 5)
            .Text("{Fields.Regiao}").At(50, 0).Size(35, 5)
            .Text("{Fields.Quantidade}").At(85, 0).Size(20, 5).AlignRight()
            .Text("{Fields.Valor}").At(105, 0).Size(30, 5).AlignRight().Format("N2"))
        .PageFooter(f => f.Height(8)
            .Text("Página {Page.Number} de {Page.Total}").At(0, 2).Size(60, 5))
        .Build();

    /// <summary>Style resolution: font family/size/weight, colours, both alignment axes, borders and
    /// number/date formatting. These are the values a renderer silently drops.</summary>
    public static Report Estilos() => New("Estilos")
        .Page(p => p.A5().Portrait().Margins(10))
        .ReportHeader(h => h.Height(60)
            .Label("Times 14 itálico").At(0, 0).Size(80, 8).Font("Times New Roman", 14, FontStyle.Italic)
            .Label("Vermelho").At(0, 10).Size(80, 6).Color(Color.FromHex("#CC0000"))
            .Label("Direita/meio").At(0, 18).Size(80, 8).AlignRight().AlignMiddle()
            .Label("Com borda").At(0, 28).Size(80, 8).Border(BorderLineStyle.Solid, 1.0, Color.FromHex("#0033AA"))
            .Text("1234.5").At(0, 38).Size(80, 6).Format("C2")
            .Label("Sem quebra de linha neste texto longo").At(0, 46).Size(40, 6).NoWrap()
            // Texto de relatório é dado do autor, não vocabulário controlado: aspas e barra invertida
            // quebrariam a estrutura de linha do golden se não fossem escapadas. Fica no catálogo para
            // que o próprio golden seja a prova de que o escape acontece.
            .Label("Aspas \"X\" e barra \\ no meio").At(0, 52).Size(80, 6))
        .Build();

    /// <summary>Vector shapes, including a gradient fill. A gradient that degrades to its start
    /// colour is invisible to a pixel-count assertion but obvious in this golden.</summary>
    public static Report Formas() => New("Formas")
        .Page(p => p.A5().Landscape().Margins(10))
        .ReportHeader(h => h.Height(70)
            .Rectangle().At(0, 0).Size(60, 20).Fill(Color.FromHex("#DDEEFF"))
            .Rectangle().At(0, 24).Size(60, 20).CornerRadius(3)
                .BackgroundGradient(Color.FromHex("#FF8800"), Color.FromHex("#FFFFFF"))
            .Ellipse().At(70, 0).Size(40, 20).Fill(Color.FromHex("#EEFFEE"))
            .Line().At(0, 50).Size(120, 0).Thickness(2.0)
            .Line().At(0, 56).Size(120, 0).Thickness(0.5))
        .Build();

    /// <summary>Enough rows to overflow onto further pages. Pins the page-break position, the
    /// repeated page header, and <c>Page.Total</c> — which only resolves through the paginator's
    /// second pass, so a regression there shows up as a wrong total in the golden.</summary>
    public static Report Multipagina()
    {
        var muitas = Enumerable.Range(1, 60)
            .Select(i => new Venda($"Item {i:000}", i % 2 == 0 ? "Sul" : "Norte", i, i * 10.5m))
            .ToArray();

        return New("Multipagina")
            .Page(p => p.A5().Portrait().Margins(12))
            .DataSource("Vendas", muitas)
            .PageHeader(h => h.Height(7)
                .Label("Item").At(0, 0).Size(40, 5).Bold()
                .Label("Valor").At(60, 0).Size(30, 5).Bold().AlignRight())
            .Detail(d => d.Height(5)
                .Text("{Fields.Produto}").At(0, 0).Size(40, 4)
                .Text("{Fields.Valor}").At(60, 0).Size(30, 4).AlignRight().Format("N2"))
            .PageFooter(f => f.Height(6)
                .Text("{Page.Number}/{Page.Total}").At(0, 0).Size(30, 4))
            .Build();
    }
}
