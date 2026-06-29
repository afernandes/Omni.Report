using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// <b>Crosstab mais LARGO que a página.</b> 12 clientes × 18 produtos formam uma matriz bem mais larga que
/// uma página A4. Com <c>MinColumnWidth</c> definido, as colunas não cabem nessa largura mínima legível, então
/// a matriz <b>pagina por COLUNA</b> (tiling horizontal, estilo SSRS <i>"Across then Down"</i>): as colunas
/// quebram em <b>tiles</b> entre páginas, reimprimindo os cabeçalhos de <b>linha</b> (clientes) em cada tile e
/// sem perder nenhuma coluna. Compõe com a paginação por linha (uma matriz grande nas duas dimensões vira uma
/// grade de tiles). Tudo é estático — sem interatividade em runtime.
/// </summary>
public static class Sample19_CrosstabLargo
{
    public static Report Build(IEnumerable<Venda>? rows = null) =>
        ReportBuilder
            .Create("Crosstab Largo (tiling de colunas)")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", rows ?? GerarVendas())
            .ReportHeader(h => h.Height(20)
                .Text("Vendas por Cliente × Produto — crosstab largo (18 produtos): pagina por coluna")
                    .At(0, 0).Size(180, 8).Font("Arial", 12, FontStyle.Bold)
                // 18 produtos como colunas → bem mais largo que a página. MinColumnWidth fixa uma largura legível
                // por coluna; como elas não cabem todas, a matriz quebra as colunas em tiles horizontais
                // (Across then Down), repetindo os cabeçalhos de linha em cada tile.
                .Tablix(t => t
                    .RowGroup("Fields.Cliente")
                    .ColumnGroup("Fields.Produto")
                    .Corner("Cliente")
                    .MinColumnWidth(24) // largura mínima por coluna → dispara o tiling horizontal
                    .Cell("Fields.Total", s => s with
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Format = "C",
                    }))
                    .At(0, 10).Size(180, 262))
            // Sem detalhe por linha — a matriz agrega todo o dataset e renderiza uma vez (paginando).
            .Detail(d => d.Height(0))
            .PageFooter(f => f.Height(8)
                .Text("Página {Page.Number} de {Page.Total}")
                    .At(0, 1).Size(180, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();

    // 12 clientes × 18 produtos (216 linhas) → matriz 12 linhas × 18 colunas, bem mais larga que a página.
    private static IReadOnlyList<Venda> GerarVendas()
    {
        var list = new List<Venda>(12 * 18);
        for (int c = 1; c <= 12; c++)
        {
            for (int p = 1; p <= 18; p++)
            {
                list.Add(new Venda($"Cliente {c:00}", $"P{p:00}", ((c + p) % 7) + 1, p * 3.5m, new DateTime(2026, 5, 1)));
            }
        }
        return list;
    }
}
