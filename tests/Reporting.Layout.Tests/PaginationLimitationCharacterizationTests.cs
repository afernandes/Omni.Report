using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// CHARACTERIZATION tests — they lock the paginator's CURRENT behaviour for known limitations,
/// NOT the ideal RDL behaviour. Each test documents a feature that is intentionally NOT
/// implemented yet; the assertions reflect what the code really does today so a future change
/// that implements the feature will (deliberately) break these tests and prompt an update.
///
/// These are NOT bug reports. They exist so the limitations are visible and a "green" feature
/// test never accidentally claims a behaviour the engine does not have.
/// </summary>
public class PaginationLimitationCharacterizationTests
{
    // 10. ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// RESIDUAL (documented, not a bug): a SINGLE element taller than a whole column cannot be split — text is
    /// not line-split in this static engine (which keeps VerticalAlignment well-defined). The band-split path
    /// (see <c>PaginationEdgeCaseTests.Band_taller_than_page_splits_by_element_*</c>) distributes a multi-element
    /// band across pages, but when one indivisible element exceeds a full column it is emitted WHOLE on its own
    /// page (overflowing) and pagination still TERMINATES — never loops. This locks that termination + whole-
    /// emission guarantee for the lone-oversized-element case.
    /// </summary>
    [Fact]
    public async Task Single_oversized_element_is_emitted_whole_and_pagination_terminates()
    {
        // Page content height = 60 - 2*5 = 50mm. Detail band = 80mm > 50mm: it cannot ever fit.
        var page = new PageSetup(new PaperSize("MiniA6", Unit.FromMm(60), Unit.FromMm(60)),
            Margins: Thickness.Uniform(Unit.FromMm(5)));
        var detail = new DetailBand(Unit.FromMm(80),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "huge",
                Bounds = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 80.Mm()),
                Text = "oversized",
            }));
        var def = new ReportDefinition("oversize", page, detail);
        var req = TestData.BuildRequest(def, TestData.ManyRows(3)); // 3 oversized rows

        // Termination guard: if the engine ever loops on an unfittable band this Task never
        // completes; WaitAsync converts that into a fast, deterministic test failure.
        var report = await new ReportPaginator().PaginateAsync(req)
            .WaitAsync(TimeSpan.FromSeconds(10));

        // Each row is emitted exactly once — the band is NOT split into pieces.
        var rowPrimitives = report.Pages.SelectMany(p => p.Primitives)
            .OfType<DrawTextPrimitive>().Where(t => t.SourceElementId == "huge").ToList();
        rowPrimitives.Should().HaveCount(3, "every oversized row is emitted exactly once (whole, no split)");
        rowPrimitives.Should().OnlyContain(t => t.Bounds.Height.ToMm() > 50,
            "the band keeps its full 80mm height and overflows the 50mm content area — it is not clipped or split");

        // No two pages carry a fragment of the SAME row id with different heights (a split would do that).
        report.Pages.Should().OnlyContain(
            p => p.Primitives.OfType<DrawTextPrimitive>().Count(t => t.SourceElementId == "huge") <= 1,
            "an unfittable band produces at most one piece per page — never a head/tail split across pages");

        // documents limitation; switch to a split-based assertion when band splitting exists.
    }

    // 11. ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// LIMITATION: continuous paper IGNORES <c>Columns &gt; 1</c>.
    /// <para>
    /// <see cref="Reporting.Layout.Internal.PageAccumulator"/> forces a single column when
    /// <c>PageSetup.IsContinuous</c> (thermal roll, height 0). Even with <c>Columns=3</c> the
    /// content does not snake: every row stacks in one left-aligned column on one long page.
    /// (PaginatorTests already locks the single column X; this complements it by locking the
    /// VERTICAL stacking — rows keep growing downward instead of wrapping into columns.)
    /// </para>
    /// <para>Replace when (if) multi-column continuous output is supported.</para>
    /// </summary>
    [Fact]
    public async Task Continuous_paper_ignores_multiple_columns_and_stacks_rows_in_one_column()
    {
        var page = new PageSetup(PaperSize.Thermal80, Margins: Thickness.Uniform(Unit.FromMm(2)), Columns: 3);
        var detail = new DetailBand(Unit.FromMm(10),
            EquatableArray.Create<ReportElement>(new LabelElement
            {
                Id = "row", Bounds = new Rectangle(0.Mm(), 0.Mm(), 18.Mm(), 10.Mm()), Text = "row",
            }));
        var def = new ReportDefinition("roll3", page, detail);
        var req = TestData.BuildRequest(def, TestData.ManyRows(9));

        var report = await new ReportPaginator().PaginateAsync(req);

        report.Pages.Should().ContainSingle("continuous paper emits one long page");
        var rows = report.Pages[0].Primitives.OfType<DrawTextPrimitive>()
            .Where(t => t.SourceElementId == "row").ToList();
        rows.Should().HaveCount(9);

        // All rows share a single column X (the left margin) — no snake offset despite Columns=3.
        rows.Select(t => t.Bounds.X.ToMm()).Distinct().Should().ContainSingle(
            "Columns=3 is ignored on continuous paper → one column at the left margin");
        // And they stack vertically: 9 distinct ascending Y positions, not 3 columns of 3.
        var ys = rows.Select(t => t.Bounds.Y.ToMm()).OrderBy(y => y).ToList();
        ys.Should().OnlyHaveUniqueItems("each row stacks below the previous one (single-column flow)");
        ys.Last().Should().BeApproximately(ys.First() + 8 * 10, 1.0,
            "9 rows of 10mm span ~80mm of vertical flow — they are NOT packed into 3 short columns");

        // documents limitation; revisit when continuous multi-column layout is implemented.
    }
}
