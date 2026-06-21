using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Layout.Tests;

public class PaginatorTests
{
    [Fact]
    public async Task Empty_dataset_still_emits_a_single_page_when_no_bands_present()
    {
        var def = ReportDefinition.Empty("Empty");
        var registry = new DataSourceRegistry();
        registry.Register(new EnumerableDataSource<Venda>("Vendas", []));
        var req = new PaginationRequest { Definition = def, DataSources = registry };

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        report.Pages.Count.Should().Be(1);
        report.Pages[0].PageNumber.Should().Be(1);
        report.Pages[0].Primitives.Count.Should().Be(0);
    }

    [Fact]
    public async Task Detail_band_emits_one_block_per_row()
    {
        var def = TestData.GroupedReport() with { Groups = EquatableArray<GroupBand>.Empty };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        // 3 rows × 1 element each = 3 primitives total
        report.Pages.Sum(p => p.Primitives.Count).Should().Be(3);
    }

    [Fact]
    public async Task Group_header_emits_once_per_distinct_key()
    {
        var def = TestData.GroupedReport();
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        var headerPrimitives = report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "group-header")
            .ToList();
        headerPrimitives.Should().HaveCount(2); // Ana, Beto
        headerPrimitives.Select(t => t.Text).Should().BeEquivalentTo(["Ana", "Beto"]);
    }

    [Fact]
    public async Task Group_footer_sum_resets_per_group()
    {
        var def = TestData.GroupedReport();
        var req = TestData.BuildRequest(def, TestData.ThreeRows());
        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        var footers = report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "group-footer")
            .Select(t => t.Text)
            .ToList();
        footers.Should().HaveCount(2);
        // First group (Ana): 10 + 25 = 35; second group (Beto): 5
        footers[0].Should().Contain("35");
        footers[1].Should().Contain("5");
    }

    [Fact]
    public async Task Pagination_is_deterministic_for_same_input()
    {
        var def = TestData.GroupedReport();
        var rows = TestData.ThreeRows();
        var paginator = new ReportPaginator();
        var first = await paginator.PaginateAsync(TestData.BuildRequest(def, rows));
        var second = await paginator.PaginateAsync(TestData.BuildRequest(def, rows));
        first.Should().Be(second);
    }

    [Fact]
    public async Task Page_breaks_when_detail_overflows()
    {
        // Force many rows on a small page so pagination kicks in.
        var smallPage = new PageSetup(
            new PaperSize("MiniA6", Unit.FromMm(60), Unit.FromMm(60)),
            Margins: Thickness.Uniform(Unit.FromMm(5)));
        var detail = new DetailBand(
            Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), Unit.FromMm(10)),
                    Text = "row",
                }));
        var def = new ReportDefinition("paged", smallPage, detail);
        var req = TestData.BuildRequest(def, TestData.ManyRows(20));

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        report.Pages.Count.Should().BeGreaterThan(1);
        report.Pages.Sum(p => p.Primitives.Count).Should().Be(20);
    }

    // 60×60mm page, 5mm margins → 50mm content height; a 10mm detail fits 5 rows per column.
    private static (PageSetup page, DetailBand detail) SnakeFixture(int columns)
    {
        var page = new PageSetup(
            new PaperSize("MiniA6", Unit.FromMm(60), Unit.FromMm(60)),
            Margins: Thickness.Uniform(Unit.FromMm(5)),
            Columns: columns,
            ColumnSpacing: Unit.FromMm(4));
        var detail = new DetailBand(
            Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 18.Mm(), Unit.FromMm(10)),
                    Text = "row",
                }));
        return (page, detail);
    }

    [Fact]
    public async Task Snake_columns_pack_rows_into_columns_before_breaking_the_page()
    {
        // 2 columns × 4 rows/column = 8 rows per physical page; 20 rows → 3 pages (vs 5 single-column).
        var (page, detail) = SnakeFixture(columns: 2);
        var def = new ReportDefinition("snake", page, detail);
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(20)));

        report.Pages.Count.Should().Be(3, "two columns roughly halve the page count (5 → 3)");
        report.Pages.Sum(p => p.Primitives.Count).Should().Be(20, "every row is still emitted");

        // The first physical page uses both columns: some labels at the left margin (col 0) and some offset
        // to the right (col 1). Content width 50mm, spacing 4mm → column width 23mm, col 1 X = 5 + 23 + 4 = 32mm.
        var xs = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Bounds.X.ToMm()).ToList();
        xs.Should().Contain(x => x > 4 && x < 6, "column 0 sits at the left margin (~5mm)");
        xs.Should().Contain(x => x > 30 && x < 34, "column 1 is offset by column width + spacing (~32mm)");
    }

    [Fact]
    public async Task Single_column_is_unchanged_by_the_column_engine()
    {
        // Regression: Columns=1 (default) keeps the original single-column page count (4 rows/page → 5 pages).
        var (page, detail) = SnakeFixture(columns: 1);
        var def = new ReportDefinition("plain", page, detail);
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(20)));

        report.Pages.Count.Should().Be(5);
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>()
            .Should().OnlyContain(t => t.Bounds.X.ToMm() > 4 && t.Bounds.X.ToMm() < 6, "everything stays at the left margin");
    }

    [Fact]
    public async Task Continuous_paper_forces_a_single_column()
    {
        // Thermal roll (height 0) must not snake even if Columns>1 is set.
        var page = new PageSetup(PaperSize.Thermal80, Margins: Thickness.Uniform(Unit.FromMm(2)), Columns: 3);
        var (_, detail) = SnakeFixture(columns: 3);
        var def = new ReportDefinition("roll", page, detail);
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(8)));

        report.Pages.Should().ContainSingle("continuous paper emits one long page");
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>()
            .Should().OnlyContain(t => t.Bounds.X.ToMm() < 4, "single column at the left margin, no snake offset");
    }

    [Fact]
    public async Task Total_pages_two_pass_resolves_correct_count()
    {
        var smallPage = new PageSetup(
            new PaperSize("MiniA6", Unit.FromMm(60), Unit.FromMm(60)),
            Margins: Thickness.Uniform(Unit.FromMm(5)));
        var detail = new DetailBand(
            Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), Unit.FromMm(10)),
                    Text = "row",
                }));
        var pageFooter = new ReportBand(
            BandKind.PageFooter,
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "footer-total",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), Unit.FromMm(6)),
                    Expression = "Página {Page.Number} de {Page.Total}",
                }));
        var def = new ReportDefinition("paged", smallPage, detail) { PageFooter = pageFooter };
        var req = TestData.BuildRequest(def, TestData.ManyRows(20));

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        var footerTexts = report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "footer-total")
            .Select(t => t.Text)
            .ToList();
        footerTexts.Should().HaveCount(report.Pages.Count);
        foreach (var (text, idx) in footerTexts.Select((t, i) => (t, i + 1)))
        {
            text.Should().Be($"Página {idx} de {report.Pages.Count}");
        }
    }

    [Fact]
    public async Task Report_scoped_aggregate_in_report_header_shows_grand_total()
    {
        // Regression: the ReportHeader renders before the detail loop accumulates rows, so a
        // report-scoped Sum used to evaluate against an empty buffer → 0. The paginator now primes
        // the Report scope with the full row set, so the header shows the dataset grand total
        // (the same value the footer would show) — matching SSRS semantics.
        var reportHeader = new ReportBand(
            BandKind.ReportHeader,
            Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "header-total",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()),
                    Expression = "Sum(Fields.Total)",
                }));
        var reportFooter = new ReportBand(
            BandKind.ReportFooter,
            Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "footer-total",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()),
                    Expression = "Sum(Fields.Total)",
                }));
        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 6.Mm()),
                    Text = "row",
                }));
        var def = new ReportDefinition("agg", PageSetup.A4Portrait, detail)
        {
            ReportHeader = reportHeader,
            ReportFooter = reportFooter,
        };
        var req = TestData.BuildRequest(def, TestData.ThreeRows()); // 10 + 25 + 5 = 40

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        string TextOf(string id) => report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>().Single(t => t.SourceElementId == id).Text;

        // The header now matches the footer — both the dataset grand total, not 0.
        TextOf("header-total").Should().Contain("40");
        TextOf("footer-total").Should().Contain("40");
    }

    [Fact]
    public async Task Parameters_are_visible_to_band_expressions()
    {
        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "x",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 6.Mm()),
                    Expression = "Parameters.Limite",
                }));
        var def = new ReportDefinition("p", PageSetup.A4Portrait, detail) with
        {
            Parameters = EquatableArray.Create(
                new Reporting.Parameters.ReportParameter("Limite", typeof(decimal), DefaultValue: 42m)),
        };
        var req = TestData.BuildRequest(def, [new Venda("c", "p", 1m)]);

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);
        var text = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Single(t => t.SourceElementId == "x");
        text.Text.Should().Be("42");
    }

    [Fact]
    public async Task Page_footer_appears_on_every_page()
    {
        var smallPage = new PageSetup(
            new PaperSize("MiniA6", Unit.FromMm(60), Unit.FromMm(60)),
            Margins: Thickness.Uniform(Unit.FromMm(5)));
        var detail = new DetailBand(
            Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 10.Mm()),
                    Text = "row",
                }));
        var footer = new ReportBand(
            BandKind.PageFooter,
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Id = "footer-text",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 6.Mm()),
                    Text = "FOOT",
                }));
        var def = new ReportDefinition("paged", smallPage, detail) { PageFooter = footer };
        var req = TestData.BuildRequest(def, TestData.ManyRows(20));

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        var footerCount = report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>()
            .Count(t => t.SourceElementId == "footer-text");
        footerCount.Should().Be(report.Pages.Count);
    }

    [Fact]
    public async Task Can_grow_expands_textbox_to_fit_wrapped_text()
    {
        var longText = string.Concat(System.Linq.Enumerable.Repeat("uma frase razoavelmente longa ", 12));
        var detail = new DetailBand(
            Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement
                {
                    Id = "big",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 30.Mm(), 6.Mm()),
                    Expression = "'" + longText + "'",
                    CanGrow = true,
                }));
        var def = new ReportDefinition("g", PageSetup.A4Portrait, detail);
        var req = TestData.BuildRequest(def, [new Venda("c", "p", 1m)]);

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);

        var primitive = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Single(t => t.SourceElementId == "big");
        primitive.Bounds.Height.ToMm().Should().BeGreaterThan(6);
    }

    [Fact]
    public async Task Keep_together_pushes_group_to_next_page_when_it_does_not_fit()
    {
        // Page large enough for ~4 rows; without KeepTogether group header may sit at the bottom
        // and footer roll to next page. With KeepTogether the whole group header+footer pair is
        // placed only when both fit.
        var page = new PageSetup(
            new PaperSize("Mini", Unit.FromMm(80), Unit.FromMm(40)),
            Margins: Thickness.Uniform(Unit.FromMm(2)));
        var detail = new DetailBand(
            Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new LabelElement
                {
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()),
                    Text = "row",
                }));
        var header = new ReportBand(BandKind.GroupHeader, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new TextBoxElement { Id = "h", Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()), Expression = "Fields.Cliente" }));
        var footer = new ReportBand(BandKind.GroupFooter, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(
                new LabelElement { Id = "f", Bounds = new Rectangle(0.Mm(), 0.Mm(), 60.Mm(), 8.Mm()), Text = "end" }));
        var group = new GroupBand("g", "Fields.Cliente", header, footer, KeepTogether: true);
        var def = new ReportDefinition("k", page, detail) with { Groups = EquatableArray.Create(group) };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());

        var paginator = new ReportPaginator();
        var report = await paginator.PaginateAsync(req);
        report.Pages.Count.Should().BeGreaterThan(1);
    }
}
