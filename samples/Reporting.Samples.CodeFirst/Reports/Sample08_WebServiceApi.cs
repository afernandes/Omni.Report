using Reporting.CodeFirst;
using Reporting.DataSources.WebService;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Demonstrates the <see cref="WebServiceDataSource"/> against a public test API.
/// Because CI runners may not have outbound HTTP, the sample uses a stubbed
/// <see cref="HttpClient"/> backed by an in-memory payload — guaranteed reproducible
/// across machines, while still exercising the same code path a real REST integration
/// would use in production.
/// </summary>
/// <remarks>
/// <para>To point this at a real API, replace <see cref="BuildOfflineClient"/> with a
/// real <see cref="HttpClient"/> (or pass <c>null</c> to let the provider create one).
/// Add an Authorization header to the options for token-based auth — the provider
/// hands every header in the <c>Headers</c> dictionary to the request.</para>
/// </remarks>
public static class Sample08_WebServiceApi
{
    public static Report Build()
    {
        // Stubbed payload — same shape as a typical REST list endpoint that wraps the
        // results in a {"items":[...]} envelope. The WebService provider's JsonRootPath
        // navigates into "items" so the rows arrive flat.
        const string stub = """
            {
              "page": 1,
              "totalPages": 1,
              "items": [
                { "sku": "SKU-001", "name": "Notebook Dell XPS 13",  "category": "Laptops",     "price": 9500.00, "stock":  4, "rating": 4.6 },
                { "sku": "SKU-002", "name": "Mouse Logitech MX Master","category": "Acessórios", "price":  449.90, "stock": 23, "rating": 4.8 },
                { "sku": "SKU-003", "name": "Teclado Keychron K2",    "category": "Acessórios", "price":  890.00, "stock": 15, "rating": 4.7 },
                { "sku": "SKU-004", "name": "Monitor LG 27 UltraFine","category": "Monitores",  "price": 3290.00, "stock":  8, "rating": 4.5 },
                { "sku": "SKU-005", "name": "Headset Sony WH-1000XM5","category": "Áudio",      "price": 2890.00, "stock": 12, "rating": 4.9 },
                { "sku": "SKU-006", "name": "SSD Samsung 980 PRO 1TB","category": "Storage",    "price":  799.00, "stock": 18, "rating": 4.8 },
                { "sku": "SKU-007", "name": "Webcam Logitech Brio",   "category": "Acessórios", "price":  990.00, "stock":  6, "rating": 4.4 }
              ]
            }
            """;

        var rest = new WebServiceDataSource("Produtos",
            new WebServiceDataSourceOptions
            {
                Url = "https://api.demo/produtos",
                Method = "GET",
                Headers = new Dictionary<string, string>
                {
                    // Headers are demonstrated as they would be used in production —
                    // the stub HttpClient ignores them but the call shape stays
                    // identical to a real Authorization-protected endpoint.
                    ["Authorization"] = "Bearer demo-token",
                    ["Accept"] = "application/json",
                },
                JsonRootPath = "items",
            }, httpClient: BuildOfflineClient(stub));

        return ReportBuilder
            .Create("Catálogo de Produtos (REST)")
            .Page(p => p.A4().Portrait().Margins(18))
            .DataSource("Produtos", rest)
            .DataSourceSortBy("Fields.category")
            .DataSourceSortBy("Fields.name")
            .ReportHeader(h => h.Height(28)
                .Text("Catálogo de Produtos · Fonte REST")
                    .At(0, 0).Size(174, 12)
                    .Font("Arial", 16, FontStyle.Bold)
                    .Center()
                .Text("Origem: GET https://api.demo/produtos · Bearer demo-token · path: items")
                    .At(0, 14).Size(174, 6)
                    .Center()
                    .Color(Color.Gray)
                .Line().From(0, 22).To(174, 22).Thickness(0.5))
            .PageHeader(h => h.Height(8)
                .Label("SKU").At(0, 0).Size(22, 6).Bold()
                .Label("Produto").At(24, 0).Size(78, 6).Bold()
                .Label("Preço").At(104, 0).Size(26, 6).Bold().AlignRight()
                .Label("Estoque").At(132, 0).Size(20, 6).Bold().AlignRight()
                .Label("★").At(154, 0).Size(20, 6).Bold().AlignRight()
                .Line().From(0, 6).To(174, 6).Thickness(0.25))
            .Group("PorCategoria", "Fields.category", g => g
                .Header(h => h.Height(8)
                    .Text("{Fields.category}")
                        .At(0, 1).Size(174, 6)
                        .Font("Arial", 11, FontStyle.Bold)
                        .Color(Color.FromHex("#C2410C"))))
            .Detail(d => d.Height(6)
                .Text("{Fields.sku}").At(0, 0).Size(22, 6).Font("Arial", 9, FontStyle.Regular)
                .Text("{Fields.name}").At(24, 0).Size(78, 6)
                .Text("{Fields.price:C}").At(104, 0).Size(26, 6).AlignRight()
                .Text("{Fields.stock:N0}").At(132, 0).Size(20, 6).AlignRight()
                .Text("{Fields.rating:N1}").At(154, 0).Size(20, 6).AlignRight())
            .DetailNoRows("API não retornou resultados.")
            .ReportFooter(f => f.Height(12)
                .Line().From(0, 0).To(174, 0).Thickness(0.5)
                .Text("Itens: {Count(Fields.sku)} · Estoque total: {Sum(Fields.stock):N0}")
                    .At(0, 2).Size(174, 6).Bold().AlignRight())
            .PageFooter(f => f.Height(6)
                .Text("OmniReport · Página {Page.Number} de {Page.Total}")
                    .At(0, 0).Size(174, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();
    }

    /// <summary>Builds an <see cref="HttpClient"/> that returns the supplied JSON body
    /// for every request. Used so the sample runs offline and deterministically.
    /// Replace with a real <c>HttpClient</c> (or pass null) for production usage.</summary>
    private static HttpClient BuildOfflineClient(string responseJson)
        => new(new StaticHandler(responseJson));

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StaticHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
