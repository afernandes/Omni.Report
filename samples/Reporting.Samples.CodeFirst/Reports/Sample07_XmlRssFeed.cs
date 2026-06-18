using Reporting.CodeFirst;
using Reporting.DataSources.Xml;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Generates a "blog digest" report from an RSS 2.0 feed XML document. Demonstrates:
/// <list type="bullet">
/// <item>Selecting row nodes with XPath (<c>/rss/channel/item</c> picks every blog item).</item>
/// <item>Discovery mode <see cref="XmlColumnDiscovery.Elements"/> — each direct child
/// element of <c>&lt;item&gt;</c> becomes a column (title, author, pubDate, category, link).</item>
/// <item>Automatic type inference: pubDate strings become <see cref="DateTime"/>,
/// the rest stay as strings.</item>
/// <item>Sorting newest-first and grouping by author for a digest layout.</item>
/// </list>
/// </summary>
public static class Sample07_XmlRssFeed
{
    public static Report Build()
    {
        var rss = new XmlDataSource("Posts", new XmlDataSourceOptions
        {
            FilePath = Sample06_JsonPedidos.ResolveSamplePath("rss-feed.xml"),
            // /rss/channel/item is the canonical row XPath for RSS 2.0 — every news
            // item in the channel becomes a row.
            RowsXPath = "/rss/channel/item",
            Discovery = XmlColumnDiscovery.Elements,
        });

        return ReportBuilder
            .Create("Blog Digest (RSS)")
            .Page(p => p.A4().Portrait().Margins(18))
            .DataSource("Posts", rss)
            // Newest first — pubDate strings were coerced to DateTime by the provider's
            // type inference, so the comparison sorts chronologically.
            .DataSourceSortBy("Fields.pubDate", Reporting.Data.SortDirection.Descending)
            .ReportHeader(h => h.Height(32)
                .Text("OmniReport Blog · Resumo")
                    .At(0, 0).Size(174, 14)
                    .Font("Arial", 18, FontStyle.Bold)
                    .Center()
                .Text("5 últimos posts · Fonte: feed RSS XML")
                    .At(0, 16).Size(174, 6)
                    .Center()
                    .Color(Color.Gray)
                .Line().From(0, 26).To(174, 26).Thickness(0.5))
            .Group("PorAutor", "Fields.author", g => g
                .SortBy("Fields.author")
                .Header(h => h.Height(8)
                    .Text("{Fields.author}")
                        .At(0, 1).Size(174, 6)
                        .Font("Arial", 11, FontStyle.Bold)
                        .Color(Color.FromHex("#C2410C"))))
            .Detail(d => d.Height(14)
                .Text("{Fields.title}")
                    .At(4, 0).Size(170, 6)
                    .Font("Arial", 10, FontStyle.Bold)
                .Text("{Fields.category} · {Fields.pubDate:dd/MM/yyyy}")
                    .At(4, 6).Size(120, 5)
                    .Font("Arial", 8, FontStyle.Italic)
                    .Color(Color.Gray)
                .Text("{Fields.link}")
                    .At(4, 6).Size(170, 5)
                    .AlignRight()
                    .Font("Arial", 7, FontStyle.Regular)
                    .Color(Color.FromHex("#2563EB"))
                .Line().From(4, 12).To(170, 12).Thickness(0.15).Color(Color.FromHex("#E5E5E5")))
            .DetailNoRows("Nenhum post encontrado no feed.")
            .PageFooter(f => f.Height(6)
                .Text("OmniReport · Página {Page.Number} de {Page.Total}")
                    .At(0, 0).Size(174, 6).AlignRight()
                    .Color(Color.Gray))
            .Build();
    }
}
