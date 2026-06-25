using Reporting.CodeFirst;
using Reporting.Samples.CodeFirst.Data;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// <b>Crosstab que ocupa VÁRIAS PÁGINAS.</b> 50 clientes × 4 produtos formam uma matriz bem mais alta que
/// uma página A4, então ela <b>pagina por linha</b> (estilo SSRS / DevExpress XtraReports): o cabeçalho de
/// coluna é reimpresso no topo de cada página e nenhuma linha é perdida na quebra. Uma coluna "Total geral"
/// (ColumnSubtotals) soma cada cliente. Tudo é estático — sem interatividade em runtime.
/// </summary>
public static class Sample18_MatrizGrande
{
    public static Report Build(IEnumerable<Venda>? rows = null) =>
        ReportBuilder
            .Create("Matriz Paginada")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", rows ?? GerarVendas())
            .ReportHeader(h => h.Height(20)
                .Text("Vendas por Cliente × Produto — matriz paginada (50 clientes)")
                    .At(0, 0).Size(180, 8).Font("Arial", 13, FontStyle.Bold)
                // Matriz mais alta que a página → pagina por linha, repetindo o cabeçalho de coluna em cada página.
                .Tablix(t => t
                    .RowGroup("Fields.Cliente")
                    .ColumnGroup("Fields.Produto")
                    .Corner("Cliente \\ Produto")
                    .ColumnSubtotals() // coluna "Total geral" somando os produtos de cada cliente
                    .Cell("Fields.Total", s => s with
                    {
                        ForeColor = Color.FromHex("#166534"), // green-800
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

    // 50 clientes, cada um comprando os 4 produtos (200 linhas) → matriz densa de 50 linhas × 4 colunas.
    private static IReadOnlyList<Venda> GerarVendas()
    {
        var produtos = new (string Nome, decimal Preco)[]
        {
            ("Caneta", 2.50m), ("Caderno", 27.40m), ("Mochila", 148.90m), ("Estojo", 19.50m),
        };
        var list = new List<Venda>(50 * produtos.Length);
        for (int i = 1; i <= 50; i++)
        {
            foreach (var (nome, preco) in produtos)
            {
                list.Add(new Venda($"Cliente {i:00}", nome, (i % 9) + 1, preco, new DateTime(2026, 5, 1)));
            }
        }
        return list;
    }
}
