using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Paper;
using Reporting.Styling;

namespace Reporting.Layout.Tests;

public sealed record Venda(string Cliente, string Produto, decimal Total);

internal static class TestData
{
    public static IReadOnlyList<Venda> ThreeRows() =>
    [
        new("Ana", "Caneta", 10m),
        new("Ana", "Caderno", 25m),
        new("Beto", "Caneta", 5m),
    ];

    public static IReadOnlyList<Venda> ManyRows(int count) =>
        System.Linq.Enumerable.Range(0, count)
            .Select(i => new Venda("c" + (i / 10), "p" + i, (i + 1) * 1m))
            .ToList();

    public static ReportDefinition GroupedReport(int detailHeightMm = 6, int groupHeaderMm = 10, int groupFooterMm = 8)
    {
        var detail = new DetailBand(
            Unit.FromMm(detailHeightMm),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Id = "detail-text",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), Unit.FromMm(detailHeightMm)),
                    Text = "Linha",
                }));

        var groupHeader = new ReportBand(
            BandKind.GroupHeader,
            Unit.FromMm(groupHeaderMm),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "group-header",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), Unit.FromMm(groupHeaderMm)),
                    Expression = "Fields.Cliente",
                }));

        var groupFooter = new ReportBand(
            BandKind.GroupFooter,
            Unit.FromMm(groupFooterMm),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "group-footer",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), Unit.FromMm(groupFooterMm)),
                    Expression = "Sum(Fields.Total, 'Group')",
                }));

        var group = new GroupBand("PorCliente", "Fields.Cliente", groupHeader, groupFooter);
        return ReportDefinition.Empty("VendasAgrupadas") with
        {
            DataSources = EquatableArray.Create(new Reporting.Data.DataSourceDefinition("Vendas")),
            Groups = EquatableArray.Create(group),
            Detail = detail,
        };
    }

    public static PaginationRequest BuildRequest(ReportDefinition def, IEnumerable<Venda> rows)
    {
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Venda>("Vendas", rows));
        return new PaginationRequest { Definition = def, DataSources = registry };
    }
}
