using Reporting.CodeFirst;
using Reporting.DataSources.Json;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Pedidos report bound to a JSON data file. Demonstrates the full path of working
/// with a JSON document as a report data source:
/// <list type="bullet">
/// <item>Loading a multi-level JSON document via <see cref="JsonDataSource"/>.</item>
/// <item>Navigating to a nested array using <see cref="JsonDataSourceOptions.RootPath"/>
/// (<c>data.results</c> in our sample document — the rows live two levels deep, NOT
/// at the document root).</item>
/// <item>Filtering ("Pago" only) and sorting on the report side, without server-side
/// query rewriting — RDL Phase 1 filter/sort run in-process.</item>
/// <item>Grouping the resulting rows by cliente, with a per-customer subtotal footer
/// and a grand total in the report footer.</item>
/// </list>
/// </summary>
public static class Sample06_JsonPedidos
{
    public static Report Build()
    {
        // The JSON file lives next to the binary (csproj copies it). Resolve a path that
        // works whether the user runs from the sample's output folder or from a CI sandbox
        // where the working directory might be the repo root.
        var jsonPath = ResolveSamplePath("pedidos.json");

        var pedidos = new JsonDataSource("Pedidos", new JsonDataSourceOptions
        {
            FilePath = jsonPath,
            // The document wraps the rows in {"data": {"results": [...]}} — a common
            // pagination envelope shape. The dot-path navigates to the array.
            RootPath = "data.results",
        });

        return ReportBuilder
            .Create("Pedidos (JSON)")
            .Page(p => p.A4().Portrait().Margins(20))
            .DataSource("Pedidos", pedidos)
            // Skip canceled orders entirely — RDL-style data-set filter applied BEFORE
            // any region consumes rows. Equivalent to a SQL WHERE clause but expressed in
            // the report expression language.
            .DataSourceFilter("Fields.status != 'Cancelado'")
            // Sort by cliente then by data so the group transitions land at clean
            // boundaries (rows for the same customer arrive together).
            .DataSourceSortBy("Fields.cliente")
            .DataSourceSortBy("Fields.data")
            .ReportHeader(h => h.Height(28)
                .Text("Relatório de Pedidos · Fonte JSON")
                    .At(0, 0).Size(170, 12)
                    .Font("Arial", 16, FontStyle.Bold)
                    .Center()
                .Text("Origem: pedidos.json · Carregado via JsonDataSource (path: data.results)")
                    .At(0, 14).Size(170, 6)
                    .Center()
                    .Color(Color.Gray)
                .Line().From(0, 22).To(170, 22).Thickness(0.5))
            .PageHeader(h => h.Height(8)
                .Label("Produto").At(0, 0).Size(80, 6).Bold()
                .Label("Qtde").At(82, 0).Size(15, 6).Bold().AlignRight()
                .Label("Preço").At(99, 0).Size(28, 6).Bold().AlignRight()
                .Label("Total").At(129, 0).Size(28, 6).Bold().AlignRight()
                .Label("Status").At(159, 0).Size(11, 6).Bold().AlignRight()
                .Line().From(0, 6).To(170, 6).Thickness(0.25))
            .Group("PorCliente", "Fields.cliente", g => g
                .Header(h => h.Height(10)
                    .Text("Cliente: {Fields.cliente}")
                        .At(0, 2).Size(170, 6)
                        .Font("Arial", 11, FontStyle.Bold)
                        .Color(Color.FromHex("#C2410C")))
                .Footer(f => f.Height(8)
                    .Line().From(0, 0).To(170, 0).Thickness(0.25)
                    .Text("Subtotal: {Sum(Fields.total, 'Group'):C}")
                        .At(0, 1).Size(170, 6).AlignRight().Bold()))
            .Detail(d => d.Height(6)
                .Text("{Fields.produto}").At(0, 0).Size(80, 6)
                .Text("{Fields.quantidade:N0}").At(82, 0).Size(15, 6).AlignRight()
                .Text("{Fields.precoUnitario:C}").At(99, 0).Size(28, 6).AlignRight()
                .Text("{Fields.total:C}").At(129, 0).Size(28, 6).AlignRight()
                .Text("{Fields.status}").At(159, 0).Size(11, 6).AlignRight()
                    .Font("Arial", 8, FontStyle.Regular)
                    .Color(Color.Gray))
            .DetailNoRows("Nenhum pedido encontrado para os filtros aplicados.")
            .ReportFooter(f => f.Height(15)
                .Line().From(0, 0).To(170, 0).Thickness(0.5)
                .Text("Total geral: {Sum(Fields.total):C}")
                    .At(0, 2).Size(170, 10).Bold().AlignRight()
                    .Font("Arial", 11, FontStyle.Bold))
            .PageFooter(f => f.Height(8)
                .Text("OmniReport · Página {Page.Number} de {Page.Total}")
                    .At(0, 1).Size(170, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();
    }

    /// <summary>Resolves a sample data file path. Tries the working directory first
    /// (binary output), then falls back to walking upward looking for the SampleData
    /// folder — works for IDE F5 runs as well as <c>dotnet run</c> from the repo root.</summary>
    internal static string ResolveSamplePath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "SampleData", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "SampleData", fileName),
            Path.Combine(Directory.GetCurrentDirectory(),
                "samples", "Reporting.Samples.CodeFirst", "SampleData", fileName),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        throw new FileNotFoundException(
            $"Sample data file '{fileName}' not found. Tried: {string.Join(", ", candidates)}");
    }
}
