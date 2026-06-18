using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Paper;
using Reporting.Rendering.Skia;
using Reporting.Styling;
using Xunit;

namespace Reporting.Rendering.Tests;

public sealed record Sale(string Customer, string Product, decimal Total);

public class PaginatorWithSkiaIntegrationTests
{
    [Fact]
    public async Task Render_full_grouped_report_to_pdf()
    {
        var rows = new[]
        {
            new Sale("Ana", "Caneta", 10m),
            new Sale("Ana", "Caderno", 25m),
            new Sale("Beto", "Caneta", 5m),
            new Sale("Beto", "Lápis", 3m),
        };

        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 6.Mm()),
                    Expression = "Fields.Product",
                },
                new TextBoxElement
                {
                    Bounds = new Rectangle(85.Mm(), 0.Mm(), 30.Mm(), 6.Mm()),
                    Expression = "{Fields.Total:C}",
                    Style = Style.Default with { HorizontalAlignment = HorizontalAlignment.Right },
                }));
        var groupHeader = new ReportBand(BandKind.GroupHeader, Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 120.Mm(), 10.Mm()),
                    Expression = "Cliente: {Fields.Customer}",
                    Style = Style.Default with { Font = new Font("Arial", 12, FontStyle.Bold) },
                }));
        var groupFooter = new ReportBand(BandKind.GroupFooter, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Bounds = new Rectangle(85.Mm(), 0.Mm(), 30.Mm(), 8.Mm()),
                    Expression = "{Sum(Fields.Total, 'Group'):C}",
                    Style = Style.Default with { HorizontalAlignment = HorizontalAlignment.Right, Font = new Font("Arial", 10, FontStyle.Bold) },
                }));
        var group = new GroupBand("PorCliente", "Fields.Customer", groupHeader, groupFooter);

        var def = new ReportDefinition("VendasPorCliente", PageSetup.A4Portrait, detail) with
        {
            DataSources = EquatableArray.Create(new Reporting.Data.DataSourceDefinition("Vendas")),
            Groups = EquatableArray.Create(group),
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(15),
                EquatableArray.Create<ReportElement>(
                    new LabelElement
                    {
                        Bounds = new Rectangle(0.Mm(), 0.Mm(), 170.Mm(), 15.Mm()),
                        Text = "Relatório de Vendas",
                        Style = Style.Default with { Font = new Font("Arial", 16, FontStyle.Bold), HorizontalAlignment = HorizontalAlignment.Center },
                    })),
        };

        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Sale>("Vendas", rows));
        var req = new PaginationRequest { Definition = def, DataSources = registry };

        var paginator = new ReportPaginator();
        var rendered = await paginator.PaginateAsync(req);

        using var ctx = new SkiaRenderingContext();
        RenderedReportPlayer.Play(rendered, ctx);

        ctx.Pages.Should().HaveCount(rendered.Pages.Count);
        var pdf = ctx.ToPdfBytes();
        pdf.Should().NotBeEmpty();
        var prefix = System.Text.Encoding.ASCII.GetString(pdf, 0, 5);
        prefix.Should().Be("%PDF-");
    }

    [Fact]
    public async Task Thermal_continuous_paper_renders_full_content_height()
    {
        // Regression: SkiaRenderingContext used to allocate a square bitmap for
        // Paper.Height == 0 (thermal). Content beyond `widthPx` (~80mm worth of pixels)
        // was silently clipped. RenderedReportPlayer now resolves the continuous height
        // from primitives BEFORE BeginPage — every renderer (Skia raster + GDI + future)
        // gets a properly sized surface.
        var rows = new[] { new Sale("Cliente", "Item", 10m) };
        var data = new EnumerableDataSource<Sale>("Sales", rows);

        // Build a tall thermal-80 report: header 30mm + detail 80mm + footer 30mm = 140mm tall,
        // way past the 80mm width that the old code used as placeholder height.
        var header = new ReportBand(BandKind.ReportHeader, Unit.FromMm(30),
            new EquatableArray<ReportElement>([
                new LabelElement { Text = "TOP", Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()) },
            ]));
        var detail = new DetailBand(Unit.FromMm(80), new EquatableArray<ReportElement>([
            new TextBoxElement
            {
                Expression = "Fields.Customer",
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()),
            },
        ]));
        var footer = new ReportBand(BandKind.ReportFooter, Unit.FromMm(30),
            new EquatableArray<ReportElement>([
                new LabelElement { Text = "BOTTOM", Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()) },
            ]));

        var thermal = PageSetup.A4Portrait with
        {
            Paper = new PaperSize("Thermal80", Unit.FromMm(80), Unit.Zero),
            Margins = new Thickness(Unit.FromMm(2), Unit.FromMm(2), Unit.FromMm(2), Unit.FromMm(2)),
        };

        var def = new ReportDefinition("Thermal cont. test", thermal, detail)
        {
            ReportHeader = header,
            ReportFooter = footer,
        };

        var registry = new DataSourceRegistry();
        registry.Register(data);

        var rendered = await new ReportPaginator().PaginateAsync(
            new PaginationRequest { Definition = def, DataSources = registry });

        using var ctx = new SkiaRenderingContext(dpi: 96);
        RenderedReportPlayer.Play(rendered, ctx);

        ctx.Pages.Should().NotBeEmpty();
        var page = ctx.Pages[0];
        // 80mm @ 96dpi ≈ 302 px wide.
        page.WidthPx.Should().BeInRange(290, 310);
        // 140mm of content + 2mm bottom margin = 142mm tall ≈ 536px. The OLD broken behaviour
        // gave a square bitmap = ~302px tall, clipping ~234px of the receipt. Anything well
        // above the width proves the regression is fixed.
        page.HeightPx.Should().BeGreaterThan(450,
            "thermal continuous bitmap must size to actual content height, not the placeholder square");
    }
}
