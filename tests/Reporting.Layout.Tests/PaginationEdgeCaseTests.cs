using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Styling;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// Edge-case tests for <see cref="ReportPaginator"/> covering pagination features that are
/// actually IMPLEMENTED today: Detail PageBreak.Start / StartAndEnd, PageHeader/PageFooter
/// PrintOnFirstPage + PrintOnLastPage gating, element-level CanShrink/CanGrow, NoRowsMessage
/// ordering, zero margins, landscape + multi-column width, continuous paper growth, and snake
/// column flow.
///
/// All assertions were derived from the paginator + BandRenderer source, not from "ideal" RDL
/// semantics. Remaining limitations (continuous + columns) live in the sibling
/// <c>PaginationLimitationCharacterizationTests</c>.
/// </summary>
public class PaginationEdgeCaseTests
{
    // A small page (60x60mm, 5mm margins → 50mm content height) that fits ~5 rows of a 10mm detail.
    // Mirrors the SnakeFixture / smallPage idiom used across PaginatorTests so behaviour matches.
    private static PageSetup SmallPage(int columns = 1, Orientation orientation = Orientation.Portrait) =>
        new(new PaperSize("MiniA6", Unit.FromMm(60), Unit.FromMm(60)),
            orientation,
            Margins: Thickness.Uniform(Unit.FromMm(5)),
            Columns: columns,
            ColumnSpacing: Unit.FromMm(4));

    private static DetailBand LabelDetail(int heightMm = 10, string id = "detail-text") =>
        new(Unit.FromMm(heightMm),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = id,
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 18.Mm(), Unit.FromMm(heightMm)),
                Text = "row",
            }));

    // A report header so CurrentY is already past the top margin before the first detail row.
    // Detail.PageBreak.Start is GATED on "CurrentY > Margins.Top + pageHeaderHeight" (matching RDL:
    // a Start break at the very top of a fresh page is a no-op). The header makes the break fire.
    private static ReportBand TopBanner() =>
        new(BandKind.ReportHeader, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "banner",
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 8.Mm()),
                Text = "BANNER",
            }));

    // 1. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Detail_PageBreak_Start_pushes_the_first_row_to_a_new_page()
    {
        // Mirror of RdlBehaviorTests.Detail_PageBreak_End but for Start: the break is held until
        // just before the first detail row. With a report header above the detail, CurrentY is
        // already past the top, so the Start break fires → the first (and all) detail rows move to
        // page 2. (ReportPaginator: detailBreakStartPending → BreakPage before the first iteration.)
        var def = new ReportDefinition("brk-start", PageSetup.A4Portrait, LabelDetail() with { PageBreak = PageBreak.Start })
        {
            ReportHeader = TopBanner(),
        };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());

        var report = await new ReportPaginator().PaginateAsync(req);

        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
        // Page 1 carries the banner but no detail rows (the break fired before any detail was emitted).
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Should().Contain(t => t.Text == "BANNER");
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "detail-text").Should().BeEmpty();
        // Every detail row landed on page 2+.
        report.Pages.Skip(1).SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
            .Count(t => t.SourceElementId == "detail-text").Should().Be(3);
    }

    // 2. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Detail_PageBreak_StartAndEnd_isolates_the_detail_block_on_its_own_page()
    {
        // StartAndEnd = Start (break before first row) + End (break after last row). With a report
        // header above the detail the Start break fires, so the detail rows occupy a page of their
        // own, bracketed by page breaks on both sides.
        var def = new ReportDefinition("brk-both", PageSetup.A4Portrait, LabelDetail() with { PageBreak = PageBreak.StartAndEnd })
        {
            ReportHeader = TopBanner(),
        };
        var req = TestData.BuildRequest(def, TestData.ThreeRows());

        var report = await new ReportPaginator().PaginateAsync(req);

        // Start break → page 1 empty of detail; End break → trailing page after the detail.
        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "detail-text").Should().BeEmpty();
        // The page that actually carries the detail rows carries ALL of them (no split, single page for the band run).
        var detailPages = report.Pages
            .Where(p => p.Primitives.OfType<DrawTextPrimitive>().Any(t => t.SourceElementId == "detail-text"))
            .ToList();
        detailPages.Should().ContainSingle("the isolated detail block lives on exactly one page");
        detailPages[0].Primitives.OfType<DrawTextPrimitive>()
            .Count(t => t.SourceElementId == "detail-text").Should().Be(3);
    }

    // 3. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PageHeader_PrintOnFirstPage_false_suppresses_the_header_only_on_page_one()
    {
        // ReportPaginator.EmitPageHeader gates: page 1 + !PrintOnFirstPage → skip. Subsequent
        // pages (created via BreakPage) always re-emit. A multi-page report therefore shows the
        // header on pages 2..N but not page 1.
        var pageHeader = new ReportBand(BandKind.PageHeader, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "page-header",
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 8.Mm()),
                Text = "HEADER",
            }),
            PrintOnFirstPage: false);
        var def = new ReportDefinition("ph", SmallPage(), LabelDetail()) { PageHeader = pageHeader };
        var req = TestData.BuildRequest(def, TestData.ManyRows(20)); // forces several pages

        var report = await new ReportPaginator().PaginateAsync(req);

        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
        bool HasHeader(RenderedPage p) =>
            p.Primitives.OfType<DrawTextPrimitive>().Any(t => t.SourceElementId == "page-header");
        HasHeader(report.Pages[0]).Should().BeFalse("PrintOnFirstPage=false suppresses the header on page 1");
        report.Pages.Skip(1).Should().OnlyContain(
            p => p.Primitives.OfType<DrawTextPrimitive>().Any(t => t.SourceElementId == "page-header"),
            "pages 2+ always carry the page header");
    }

    // 4. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CanShrink_textbox_with_short_content_emits_a_shorter_primitive_than_without()
    {
        // Element-level CanShrink (BandRenderer.EmitText): a TextBox whose measured content is
        // shorter than its declared bounds shrinks the emitted primitive's height. We compare the
        // SAME report with and without CanShrink and assert the shrunk primitive is shorter.
        //
        // NOTE on scope: this isolates ELEMENT-level CanShrink (the primitive height). Pulling the next
        // band UP requires BAND-level opt-in (DetailBand.CanShrink), which this test deliberately leaves
        // off — that behaviour is covered separately by Detail_band_shrinks_* below.
        static PaginationRequest Build(bool canShrink)
        {
            var detail = new DetailBand(
                Unit.FromMm(20), // tall band so there is room to shrink into
                EquatableArray.Create<ReportElement>(new TextBoxElement
                {
                    Id = "tb",
                    Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 20.Mm()),
                    Expression = "'x'", // one short line → measured height ≈ 4.4mm (10pt * 1.25)
                    CanShrink = canShrink,
                }));
            var def = new ReportDefinition("shrink", PageSetup.A4Portrait, detail);
            return TestData.BuildRequest(def, [new Venda("c", "p", 1m)]);
        }

        var without = await new ReportPaginator().PaginateAsync(Build(canShrink: false));
        var with = await new ReportPaginator().PaginateAsync(Build(canShrink: true));

        Unit Height(RenderedReport r) => r.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>().Single(t => t.SourceElementId == "tb").Bounds.Height;

        Height(without).Should().Be(Unit.FromMm(20), "no CanShrink keeps the declared 20mm bounds");
        Height(with).Should().BeLessThan(Unit.FromMm(20), "CanShrink collapses the textbox to its content height");
    }

    // 5. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task NoRowsMessage_is_sandwiched_between_report_header_and_report_footer_by_Y()
    {
        // RdlBehaviorTests already proves the message TEXT is emitted on empty data. This adds the
        // ORDERING contract: header (top) → NoRows message (middle) → footer (bottom), verified by
        // ascending Y. (ReportPaginator emits the message after the header and before the footer.)
        var reportHeader = new ReportBand(BandKind.ReportHeader, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "rh", Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 8.Mm()), Text = "TOP",
            }));
        var reportFooter = new ReportBand(BandKind.ReportFooter, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "rf", Bounds = new Rectangle(0.Mm(), 0.Mm(), 100.Mm(), 8.Mm()), Text = "BOTTOM",
            }));
        var def = ReportDefinition.Empty("norows") with
        {
            DataSources = EquatableArray.Create(new Reporting.Data.DataSourceDefinition("Vendas")),
            ReportHeader = reportHeader,
            ReportFooter = reportFooter,
            Detail = new DetailBand(Unit.FromMm(6), EquatableArray<ReportElement>.Empty,
                NoRowsMessage: "Nenhum registro encontrado."),
        };
        var req = TestData.BuildRequest(def, Array.Empty<Venda>());

        var report = await new ReportPaginator().PaginateAsync(req);
        var texts = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().ToList();

        double YOf(string predicateText) => texts.Single(t => t.Text == predicateText).Bounds.Y.ToMm();
        var headerY = YOf("TOP");
        var messageY = YOf("Nenhum registro encontrado.");
        var footerY = YOf("BOTTOM");

        headerY.Should().BeLessThan(messageY, "the report header sits above the no-rows message");
        messageY.Should().BeLessThan(footerY, "the no-rows message sits above the report footer");
    }

    // 6. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Zero_margins_paginate_without_crashing_and_fit_more_rows_per_page()
    {
        // Margins.Uniform(0): the whole page becomes content. A 60x60mm page with 0 margins fits
        // 6 rows of a 10mm detail per page (60/10) vs 5 with 5mm margins (50/10). Fewer pages.
        DetailBand detail = LabelDetail();
        async Task<RenderedReport> Run(Thickness margins)
        {
            var page = new PageSetup(new PaperSize("MiniA6", Unit.FromMm(60), Unit.FromMm(60)), Margins: margins);
            var def = new ReportDefinition("m", page, detail);
            return await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(18)));
        }

        var zero = await Run(Thickness.Uniform(Unit.Zero));
        var normal = await Run(Thickness.Uniform(Unit.FromMm(5)));

        zero.Pages.Should().NotBeEmpty("zero margins must not crash the paginator");
        zero.Pages.Sum(p => p.Primitives.Count).Should().Be(18, "every row is still emitted");
        // Rows start at the very top (Y = 0) when there are no margins.
        zero.Pages[0].Primitives.OfType<DrawTextPrimitive>()
            .Should().Contain(t => t.Bounds.Y.ToMm() < 0.5, "the first row sits flush at the top with no top margin");
        zero.Pages.Count.Should().BeLessThan(normal.Pages.Count,
            "no margins give a taller content area → more rows per page → fewer pages");
    }

    // 7. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Landscape_with_two_columns_uses_the_wider_page_for_column_offset()
    {
        // Landscape swaps width/height (PageSetup.PageWidth). For A4 landscape the content width is
        // 297 - 2*margins, far wider than portrait. With Columns=2 the second column's X reflects
        // that wider page. We compare landscape vs portrait to prove the wider geometry is used.
        async Task<List<double>> ColumnXs(Orientation orientation)
        {
            var page = new PageSetup(PaperSize.A4, orientation,
                Margins: Thickness.Uniform(Unit.FromMm(10)), Columns: 2, ColumnSpacing: Unit.FromMm(4));
            // Tall detail so each column fills and the engine is forced to use column 1 too.
            var detail = new DetailBand(Unit.FromMm(40),
                EquatableArray.Create<ReportElement>(new LabelElement
                {
                    Id = "c", Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 40.Mm()), Text = "row",
                }));
            var def = new ReportDefinition("ls", page, detail);
            var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(12)));
            return report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
                .Select(t => t.Bounds.X.ToMm()).Distinct().OrderBy(x => x).ToList();
        }

        var landscape = await ColumnXs(Orientation.Landscape);
        var portrait = await ColumnXs(Orientation.Portrait);

        // Two distinct column X positions in each orientation.
        landscape.Should().HaveCount(2);
        portrait.Should().HaveCount(2);
        // Column 0 sits at the left margin (10mm) in both orientations.
        landscape[0].Should().BeApproximately(10, 0.5);
        // Landscape A4: content width = 297-20 = 277mm; col width = (277-4)/2 = 136.5mm; col1 X = 10+136.5+4 = 150.5mm.
        // Portrait  A4: content width = 210-20 = 190mm; col width = (190-4)/2 =  93.0mm; col1 X = 10+ 93.0+4 = 107.0mm.
        landscape[1].Should().BeApproximately(150.5, 1.0, "landscape column 1 is offset by the WIDE-page column width");
        landscape[1].Should().BeGreaterThan(portrait[1], "landscape's wider page pushes column 1 further right than portrait");
    }

    // 8. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Continuous_paper_with_CanGrow_stays_a_single_page_that_grows_with_content()
    {
        // Continuous (thermal) paper has Height 0 → IsContinuous → ContentBottom = "infinite", so
        // the paginator never breaks the page. With many rows whose CanGrow textboxes wrap onto
        // several lines, the single page accumulates well past a normal A4 height.
        var longText = string.Concat(Enumerable.Repeat("uma linha de texto razoavelmente comprida ", 6));
        var detail = new DetailBand(Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Id = "grow",
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 6.Mm()),
                Expression = "'" + longText + "'",
                CanGrow = true,
            }));
        var page = new PageSetup(PaperSize.Thermal80, Margins: Thickness.Uniform(Unit.FromMm(3)));
        var def = new ReportDefinition("roll", page, detail);
        var req = TestData.BuildRequest(def, TestData.ManyRows(40));

        var report = await new ReportPaginator().PaginateAsync(req);

        report.Pages.Should().ContainSingle("continuous paper never breaks → one long page");
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Count(t => t.SourceElementId == "grow")
            .Should().Be(40, "all 40 rows accumulate on the single continuous page");
        // The accumulated content runs far below a normal page; the last row's Y proves the page grew.
        var lowestY = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().Max(t => t.Bounds.Y.ToMm());
        lowestY.Should().BeGreaterThan(297, "stacked growing rows push content past one A4 height (297mm)");
        // Each CanGrow textbox actually grew beyond its declared 6mm bounds.
        report.Pages[0].Primitives.OfType<DrawTextPrimitive>()
            .Should().OnlyContain(t => t.Bounds.Height.ToMm() > 6, "every wrapped row grew taller than 6mm");
    }

    // 9. ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Snake_detail_that_overflows_a_column_flows_to_the_next_column_then_breaks_the_page()
    {
        // 60x60mm page, 5mm margins; a 10mm detail packs 4 rows/column (matches the existing
        // Single_column_is_unchanged test: 20 rows → 5 single-column pages = 4 rows/page).
        // Columns=2 → 8 rows/physical page. Rows 5-8 cannot fit column 0, so they flow to column 1
        // at the SAME column top (Y); rows 9+ exhaust both columns → page 2. We assert the column X
        // partition AND that the second column's rows reuse the first column's top Y.
        var page = SmallPage(columns: 2);
        var def = new ReportDefinition("snake", page, LabelDetail());
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(11)));

        // 8 rows/page → 11 rows spill onto a 2nd page (8 + 3).
        report.Pages.Count.Should().Be(2);
        report.Pages.Sum(p => p.Primitives.Count).Should().Be(11);

        var p1 = report.Pages[0].Primitives.OfType<DrawTextPrimitive>().ToList();
        // Column 0 at the left margin (~5mm); column 1 offset by column width + spacing.
        // content width 50mm, spacing 4mm → col width 23mm; col1 X = 5 + 23 + 4 = 32mm.
        var col0 = p1.Where(t => t.Bounds.X.ToMm() is > 4 and < 6).ToList();
        var col1 = p1.Where(t => t.Bounds.X.ToMm() is > 30 and < 34).ToList();
        col0.Should().HaveCount(4, "column 0 packs 4 rows before overflowing");
        col1.Should().HaveCount(4, "the overflow snakes into column 1 (another 4 rows) on the same page");

        // The two columns share the same top Y: the first row of each column sits at the same Y.
        var col0Top = col0.Min(t => t.Bounds.Y.ToMm());
        var col1Top = col1.Min(t => t.Bounds.Y.ToMm());
        col1Top.Should().BeApproximately(col0Top, 0.5, "a column break resets Y to the column top, not the page margin");

        // The remaining 3 rows land on page 2, back in column 0.
        var p2 = report.Pages[1].Primitives.OfType<DrawTextPrimitive>().ToList();
        p2.Should().HaveCount(3);
        p2.Should().OnlyContain(t => t.Bounds.X.ToMm() > 4 && t.Bounds.X.ToMm() < 6,
            "the page break restarts at column 0");
    }

    // INCREMENTAL ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task Group_aggregate_survives_a_page_break_within_the_group()
    {
        // Complements PaginatorTests.Group_footer_sum_resets_per_group: that proves the reset on a
        // group CHANGE. This proves the group accumulator is NOT reset on a PAGE break inside the
        // same group — BreakPage calls ctx.ResetPage() but never ctx.ResetGroup(), so a single
        // group whose detail rows overflow several pages still shows the full Sum in its footer.
        var rows = Enumerable.Range(1, 12).Select(i => new Venda("Único", "p" + i, i)).ToList(); // 1..12 → Sum=78

        var detail = new DetailBand(Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "row", Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 10.Mm()), Text = "row",
            }));
        var groupFooter = new ReportBand(BandKind.GroupFooter, Unit.FromMm(8),
            EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Id = "g-sum",
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 8.Mm()),
                Expression = "Sum(Fields.Total, 'Group')",
            }));
        var group = new GroupBand("g", "Fields.Cliente", Header: null, Footer: groupFooter);
        var def = new ReportDefinition("agg-span", SmallPage(), detail) with
        {
            Groups = EquatableArray.Create(group),
        };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, rows));

        // 4 rows/page → the 12-row group spans multiple pages.
        report.Pages.Count.Should().BeGreaterThan(1, "the single group overflows one page");
        // Exactly one group footer (one group instance), and it carries the FULL sum 78, not a
        // per-page partial — proof the group accumulator was preserved across the page breaks.
        var footers = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "g-sum").ToList();
        footers.Should().ContainSingle("one group instance → one footer");
        footers[0].Text.Should().Contain("78", "the group Sum spans every row across the page break, not just the last page's");
    }

    // ── PrintOnLastPage / PrintOnFirstPage gating (camada paginação) ───────────────

    private static ReportBand Footer(bool printOnLast = true, bool printOnFirst = true) =>
        new(BandKind.PageFooter, Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "page-footer", Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 6.Mm()), Text = "FOOT",
            }),
            PrintOnFirstPage: printOnFirst, PrintOnLastPage: printOnLast);

    private static ReportBand Header(bool printOnLast = true, bool printOnFirst = true) =>
        new(BandKind.PageHeader, Unit.FromMm(6),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "page-header", Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 6.Mm()), Text = "HEAD",
            }),
            PrintOnFirstPage: printOnFirst, PrintOnLastPage: printOnLast);

    private static bool Has(RenderedPage p, string id) =>
        p.Primitives.OfType<DrawTextPrimitive>().Any(t => t.SourceElementId == id);

    [Fact]
    public async Task PageFooter_is_suppressed_on_the_last_page_when_PrintOnLastPage_false()
    {
        var def = new ReportDefinition("lastfoot", SmallPage(), LabelDetail()) { PageFooter = Footer(printOnLast: false) };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(20)));

        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
        Has(report.Pages[^1], "page-footer").Should().BeFalse("PrintOnLastPage=false hides the footer on the last page");
        report.Pages.Take(report.Pages.Count - 1).Should()
            .OnlyContain(p => Has(p, "page-footer"), "every non-last page keeps its footer");
    }

    [Fact]
    public async Task PageHeader_is_suppressed_on_the_last_page_when_PrintOnLastPage_false()
    {
        var def = new ReportDefinition("lasthead", SmallPage(), LabelDetail()) { PageHeader = Header(printOnLast: false) };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(20)));

        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
        Has(report.Pages[^1], "page-header").Should().BeFalse("PrintOnLastPage=false hides the header on the last page");
        Has(report.Pages[0], "page-header").Should().BeTrue("the header still prints on the first page");
    }

    [Fact]
    public async Task PageFooter_PrintOnFirstPage_false_hides_only_the_first_page_footer()
    {
        var def = new ReportDefinition("firstfoot", SmallPage(), LabelDetail()) { PageFooter = Footer(printOnFirst: false) };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(20)));

        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
        Has(report.Pages[0], "page-footer").Should().BeFalse("PrintOnFirstPage=false hides the first page footer");
        Has(report.Pages[^1], "page-footer").Should().BeTrue("later pages keep the footer");
    }

    [Fact]
    public async Task Single_page_report_applies_both_first_and_last_gating()
    {
        // One page is simultaneously first AND last — both suppressions apply independently.
        var def = new ReportDefinition("one", SmallPage(), LabelDetail())
        {
            PageFooter = Footer(printOnLast: false),
            PageHeader = Header(printOnFirst: false),
        };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(2)));

        report.Pages.Should().ContainSingle();
        Has(report.Pages[0], "page-footer").Should().BeFalse("last-page gating hides the footer on the only page");
        Has(report.Pages[0], "page-header").Should().BeFalse("first-page gating hides the header on the only page");
    }

    // ── Band-level CanShrink: the band collapses to content and pulls the next band up ─────

    // Two stacked detail rows. The FIRST row has a tall band with a short CanShrink textbox; the SECOND
    // row's Y reveals whether the first band shrank (pulling row 2 up) or kept its declared height.
    private static PaginationRequest TwoRowShrink(bool bandCanShrink)
    {
        var detail = new DetailBand(
            Unit.FromMm(20), // declared tall
            EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Id = "cell",
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 20.Mm()),
                Expression = "'x'", // one short line → ~4.4mm measured
                CanShrink = true,    // element shrinks its primitive
            }),
            CanShrink: bandCanShrink); // band opt-in: collapse to content
        var def = new ReportDefinition("bandshrink", PageSetup.A4Portrait, detail);
        return TestData.BuildRequest(def, [new Venda("a", "p", 1m), new Venda("b", "p", 2m)]);
    }

    private static Unit SecondRowY(RenderedReport r)
    {
        var ys = r.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "cell").Select(t => t.Bounds.Y).OrderBy(y => y.Mils).ToList();
        return ys[1]; // the second row's top
    }

    [Fact]
    public async Task Detail_band_shrinks_when_band_CanShrink_pulls_next_band_up()
    {
        var with = await new ReportPaginator().PaginateAsync(TwoRowShrink(bandCanShrink: true));
        var without = await new ReportPaginator().PaginateAsync(TwoRowShrink(bandCanShrink: false));

        SecondRowY(with).Should().BeLessThan(SecondRowY(without),
            "band-level CanShrink collapses the first band to its content, so the second row moves up");
    }

    [Fact]
    public async Task Detail_band_does_not_shrink_without_band_CanShrink_optin()
    {
        var report = await new ReportPaginator().PaginateAsync(TwoRowShrink(bandCanShrink: false));
        // Without opt-in the band keeps its declared 20mm: row 2 sits exactly one band-height below row 1.
        var ys = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "cell").Select(t => t.Bounds.Y).OrderBy(y => y.Mils).ToList();
        (ys[1] - ys[0]).ToMm().Should().BeApproximately(20, 0.5, "declared band height is the floor without opt-in");
    }

    [Fact]
    public async Task Band_with_a_growing_element_keeps_its_floor_even_with_CanShrink()
    {
        // Shrink-safety guard: a container Rectangle grows to its children, which Measure can't predict.
        // A band containing one must NOT drop its declared-height floor (or the next band would overlap),
        // so two stacked rows stay one full band-height apart even with DetailBand.CanShrink=true.
        var rect = new RectangleElement
        {
            Id = "r",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 8.Mm()),
            Children = EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "child", Text = "C", Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 4.Mm()),
            }),
        };
        var detail = new DetailBand(Unit.FromMm(20),
            EquatableArray.Create<ReportElement>(rect), CanShrink: true);
        var def = new ReportDefinition("rect-shrink", PageSetup.A4Portrait, detail);
        var report = await new ReportPaginator().PaginateAsync(
            TestData.BuildRequest(def, [new Venda("a", "p", 1m), new Venda("b", "p", 2m)]));

        var ys = report.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "child").Select(t => t.Bounds.Y).OrderBy(y => y.Mils).ToList();
        ys.Should().HaveCount(2);
        (ys[1] - ys[0]).ToMm().Should().BeApproximately(20, 0.5,
            "a band with a growing element keeps its declared height — shrink is suppressed to preserve Measure≡Render");
    }

    [Fact]
    public async Task Band_Measure_and_Render_agree_on_shrunk_height()
    {
        // Measure (page-fit pre-check) MUST equal Render (actual emission) or the next band overlaps/leaves a
        // gap. Verify on the shrink path: enough rows to fill multiple pages; assert no detail row overlaps
        // the next within a page (each row's bottom <= next row's top).
        var detail = new DetailBand(
            Unit.FromMm(20),
            EquatableArray.Create<ReportElement>(new TextBoxElement
            {
                Id = "cell", Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 20.Mm()),
                Expression = "'x'", CanShrink = true,
            }),
            CanShrink: true);
        var def = new ReportDefinition("measure-render", SmallPage(), detail);
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(12)));

        foreach (var page in report.Pages)
        {
            var rows = page.Primitives.OfType<DrawTextPrimitive>().Where(t => t.SourceElementId == "cell")
                .OrderBy(t => t.Bounds.Y.Mils).ToList();
            for (int i = 1; i < rows.Count; i++)
            {
                rows[i].Bounds.Y.Mils.Should().BeGreaterThanOrEqualTo(rows[i - 1].Bounds.Bottom.Mils - 1,
                    "rows must not overlap — Measure and Render agree on the shrunk band height");
            }
        }
    }

    [Fact]
    public async Task Default_PrintOnLastPage_keeps_header_and_footer_on_every_page()
    {
        // Regression: with the default flags (true), no second pass is forced and bands print everywhere.
        var def = new ReportDefinition("default", SmallPage(), LabelDetail())
        {
            PageHeader = Header(),
            PageFooter = Footer(),
        };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, TestData.ManyRows(20)));

        report.Pages.Count.Should().BeGreaterThanOrEqualTo(2);
        report.Pages.Should().OnlyContain(p => Has(p, "page-footer") && Has(p, "page-header"),
            "default PrintOnFirst/LastPage=true prints header+footer on every page");
    }
}
