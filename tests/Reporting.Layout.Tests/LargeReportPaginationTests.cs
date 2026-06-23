using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// Validation of a LARGE, grouped, multi-page report: data integrity (no detail row lost or duplicated),
/// furniture in the right place (page header/footer on every page, report header/footer once, correct page
/// numbers, group headers repeating across page breaks), correct aggregates, and that nothing renders off-page.
/// </summary>
public class LargeReportPaginationTests
{
    private static Rectangle R(double x, double y, double w, double h)
        => new(Unit.FromMm(x), Unit.FromMm(y), Unit.FromMm(w), Unit.FromMm(h));

    /// <summary>A4 grouped sales report: report header, repeating page header, per-group header/footer (subtotal),
    /// detail showing the unique product + value, report footer (grand total) and a "Página N de M" page footer.</summary>
    private static ReportDefinition BigReport(int detailMm = 6) =>
        ReportDefinition.Empty("RelatorioGrande") with
        {
            DataSources = EquatableArray.Create(new DataSourceDefinition("Vendas")),
            ReportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(12), EquatableArray.Create<ReportElement>(
                new LabelElement { Bounds = R(0, 0, 120, 8), Text = "RELATORIO DE VENDAS" })),
            PageHeader = new ReportBand(BandKind.PageHeader, Unit.FromMm(8), EquatableArray.Create<ReportElement>(
                new LabelElement { Bounds = R(0, 0, 120, 6), Text = "PRODUTO / TOTAL" })),
            Groups = EquatableArray.Create(new GroupBand("PorCliente", "Fields.Cliente",
                new ReportBand(BandKind.GroupHeader, Unit.FromMm(8), EquatableArray.Create<ReportElement>(
                    new TextBoxElement { Bounds = R(0, 0, 120, 6), Expression = "Cliente: {Fields.Cliente}" })),
                new ReportBand(BandKind.GroupFooter, Unit.FromMm(6), EquatableArray.Create<ReportElement>(
                    new TextBoxElement { Bounds = R(0, 0, 120, 6), Expression = "Subtotal {Fields.Cliente}: {Sum(Fields.Total, 'Group')}" })))
            {
                RepeatHeaderOnNewPage = true,
            }),
            Detail = new DetailBand(Unit.FromMm(detailMm), EquatableArray.Create<ReportElement>(
                new TextBoxElement { Bounds = R(0, 0, 60, detailMm), Expression = "Fields.Produto" },
                new TextBoxElement { Bounds = R(60, 0, 40, detailMm), Expression = "Fields.Total" })),
            ReportFooter = new ReportBand(BandKind.ReportFooter, Unit.FromMm(10), EquatableArray.Create<ReportElement>(
                new TextBoxElement { Bounds = R(0, 0, 120, 8), Expression = "TOTAL GERAL: {Sum(Fields.Total)}" })),
            PageFooter = new ReportBand(BandKind.PageFooter, Unit.FromMm(8), EquatableArray.Create<ReportElement>(
                new TextBoxElement { Bounds = R(0, 0, 120, 6), Expression = "Pagina {Page.Number} de {Page.Total}" })),
        };

    private static async Task<RenderedReport> Paginate(ReportDefinition def, int rows)
        => await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(rows)));

    private static IEnumerable<string> Texts(RenderedPage p) => p.Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text);

    [Fact]
    public async Task A_600_row_report_spans_many_pages()
    {
        var report = await Paginate(BigReport(), 600);
        report.PageCount.Should().BeGreaterThan(5);
        report.Pages.Select(p => p.PageNumber).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task No_detail_row_is_lost_or_duplicated()
    {
        const int rows = 600;
        var report = await Paginate(BigReport(), rows);

        // Each row's product (p0..p599) is unique; every one must render exactly once across all pages.
        var products = report.Pages.SelectMany(Texts).Where(t => Regex.IsMatch(t, @"^p\d+$")).ToList();
        var expected = Enumerable.Range(0, rows).Select(i => "p" + i).ToList();

        products.Should().HaveCount(rows, "no detail row may be dropped or duplicated");
        products.Distinct().Should().HaveCount(rows, "no product renders twice");
        products.Should().BeEquivalentTo(expected, "every input row reaches the output exactly once");
    }

    [Fact]
    public async Task Page_header_and_footer_render_on_every_page()
    {
        var report = await Paginate(BigReport(), 600);

        foreach (var page in report.Pages)
        {
            Texts(page).Should().Contain("PRODUTO / TOTAL", "the page header repeats on page {0}", page.PageNumber);
            Texts(page).Should().Contain(t => t.StartsWith("Pagina "), "the page footer repeats on page {0}", page.PageNumber);
        }
    }

    [Fact]
    public async Task Report_header_is_only_on_the_first_page_and_footer_only_on_the_last()
    {
        var report = await Paginate(BigReport(), 600);

        report.Pages.Count(p => Texts(p).Contains("RELATORIO DE VENDAS")).Should().Be(1);
        Texts(report.Pages.First()).Should().Contain("RELATORIO DE VENDAS");

        var totalPages = report.Pages.Count(p => Texts(p).Any(t => t.StartsWith("TOTAL GERAL:")));
        totalPages.Should().Be(1, "the report footer (grand total) prints once");
        Texts(report.Pages.Last()).Should().Contain(t => t.StartsWith("TOTAL GERAL:"));
    }

    [Fact]
    public async Task Page_numbers_are_correct_and_total_matches_the_page_count()
    {
        var report = await Paginate(BigReport(), 600);

        foreach (var page in report.Pages)
        {
            var footer = Texts(page).Single(t => t.StartsWith("Pagina "));
            var m = Regex.Match(footer, @"^Pagina (\d+) de (\d+)$");
            m.Success.Should().BeTrue("footer '{0}' must be 'Pagina N de M'", footer);
            int.Parse(m.Groups[1].Value).Should().Be(page.PageNumber, "page number printed matches the physical page");
            int.Parse(m.Groups[2].Value).Should().Be(report.PageCount, "total-pages printed matches the real count");
        }
    }

    [Fact]
    public async Task Nothing_renders_off_the_page()
    {
        var report = await Paginate(BigReport(), 600);

        foreach (var page in report.Pages)
        {
            var h = page.PageSetup.PageHeight.ToMm();
            var w = page.PageSetup.PageWidth.ToMm();
            foreach (var prim in page.Primitives)
            {
                prim.Bounds.Y.ToMm().Should().BeGreaterThanOrEqualTo(-0.01, "no primitive starts above the page on page {0}", page.PageNumber);
                (prim.Bounds.Y + prim.Bounds.Height).ToMm().Should().BeLessThanOrEqualTo(h + 0.01,
                    "no primitive overflows the bottom of page {0}", page.PageNumber);
                (prim.Bounds.X + prim.Bounds.Width).ToMm().Should().BeLessThanOrEqualTo(w + 0.01,
                    "no primitive overflows the right of page {0}", page.PageNumber);
            }
        }
    }

    [Fact]
    public async Task Detail_rows_flow_top_to_bottom_without_overlapping()
    {
        var report = await Paginate(BigReport(), 600);

        foreach (var page in report.Pages)
        {
            var detailYs = page.Primitives.OfType<DrawTextPrimitive>()
                .Where(t => Regex.IsMatch(t.Text, @"^p\d+$"))
                .Select(t => t.Bounds.Y.Mils)
                .ToList();
            detailYs.Should().BeInAscendingOrder("detail rows flow downward on page {0}", page.PageNumber);
            detailYs.Should().OnlyHaveUniqueItems("two detail rows must not overlap at the same Y on page {0}", page.PageNumber);
        }
    }

    [Fact]
    public async Task Grand_total_and_a_group_subtotal_are_correct()
    {
        const int rows = 600; // Total = 1..600 → grand total 180300; group c0 = 1..10 → 55
        var report = await Paginate(BigReport(), rows);
        var all = report.Pages.SelectMany(Texts).ToList();

        all.Should().Contain(t => t.Replace(".00", "").Replace(",00", "").Contains("180300"),
            "the grand total equals the sum of all rows");
        all.Should().Contain(t => t.StartsWith("Subtotal c0:") && t.Contains("55"),
            "the first group's subtotal sums its 10 rows (1..10 = 55)");
    }

    [Fact]
    public async Task A_group_header_repeats_when_its_group_spans_a_page_break()
    {
        // One huge group (all rows share Cliente) forces the group to span many pages — its header must
        // reprint at the top of every continuation page (RepeatHeaderOnNewPage).
        var def = BigReport();
        var oneBigGroup = Enumerable.Range(0, 400).Select(i => new Venda("UNICO", "p" + i, (i + 1) * 1m)).ToList();
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, oneBigGroup));

        report.PageCount.Should().BeGreaterThan(3);
        report.Pages.Count(p => Texts(p).Contains("Cliente: UNICO"))
            .Should().Be(report.PageCount, "the spanning group's header reprints on every page it occupies");
    }
}
