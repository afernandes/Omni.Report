using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record MatrixCell(string Cli, string Prod, decimal Val);

/// <summary>
/// Row-level pagination of a matrix Tablix: a crosstab taller than the page splits across pages, reprinting the
/// column header (SSRS / DevExpress XtraReports behaviour), instead of overflowing. Verified end-to-end through
/// the paginator (no primitive escapes the page bounds; every row-group value survives; the header repeats).
/// </summary>
public class TablixPaginationTests
{
    // A crosstab of `clientes` rows × 3 product columns, placed in the ReportHeader so it renders once. With many
    // clients it is far taller than one A4 page, forcing the matrix to paginate.
    private static Report Matrix(int clientes, bool repeatHeaders = true, bool keepTogether = false)
    {
        var rows = Enumerable.Range(1, clientes).SelectMany(i => new[]
        {
            new MatrixCell($"C{i:000}", "ProdA", i * 1m),
            new MatrixCell($"C{i:000}", "ProdB", i * 2m),
            new MatrixCell($"C{i:000}", "ProdC", i * 3m),
        }).ToArray();

        return ReportBuilder.Create("BigMatrix")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("D", rows)
            .ReportHeader(h => h.Height(20)
                .Tablix(t =>
                {
                    t.RowGroup("Fields.Cli").ColumnGroup("Fields.Prod").Corner("Cliente").Cell("Fields.Val");
                    if (!repeatHeaders) { t.RepeatColumnHeaders(false); }
                    if (keepTogether) { t.KeepTogether(); }
                })
                .At(0, 0).Size(180, 260))
            .Detail(d => d.Height(0))
            .Build();
    }

    private static double BottomMm(RenderedPage page) =>
        page.Primitives.Count == 0 ? 0 : page.Primitives.Max(p => p.Bounds.Bottom.ToPoints()) / 72.0 * 25.4;

    private static double PageMm(RenderedPage page) => page.PageSetup.PageHeight.ToPoints() / 72.0 * 25.4;

    [Fact]
    public async Task A_tall_matrix_paginates_across_pages_without_overflow()
    {
        var rendered = await Matrix(50).PaginateAsync();

        rendered.Pages.Count.Should().BeGreaterThan(1, "50 clients are far taller than one A4 page");
        foreach (var pg in rendered.Pages)
        {
            BottomMm(pg).Should().BeLessThanOrEqualTo(PageMm(pg) + 0.5, "no content overflows the physical page");
        }
    }

    [Fact]
    public async Task The_column_header_reprints_on_every_page_by_default()
    {
        var rendered = await Matrix(50).PaginateAsync();

        foreach (var pg in rendered.Pages)
        {
            pg.Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text)
                .Should().Contain("Cliente", "the column header reprints at the top of each continuation page");
        }
    }

    [Fact]
    public async Task Every_row_group_value_survives_pagination()
    {
        var rendered = await Matrix(50).PaginateAsync();

        var texts = rendered.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToHashSet();
        for (int i = 1; i <= 50; i++)
        {
            texts.Should().Contain($"C{i:000}", "no client row may be dropped when the matrix splits");
        }
    }

    [Fact]
    public async Task RepeatColumnHeaders_false_prints_the_header_only_on_the_first_page()
    {
        var rendered = await Matrix(50, repeatHeaders: false).PaginateAsync();

        rendered.Pages.Count.Should().BeGreaterThan(1);
        rendered.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).Should().Contain("Cliente");
        rendered.Pages[1].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text)
            .Should().NotContain("Cliente", "continuation pages omit the header when RepeatColumnHeaders is off");
    }

    [Fact]
    public async Task KeepTogether_opts_out_of_pagination_keeping_the_matrix_on_one_page()
    {
        var rendered = await Matrix(50, keepTogether: true).PaginateAsync();

        rendered.Pages.Count.Should().Be(1, "KeepTogether keeps the matrix atomic (it overflows rather than splitting)");
    }

    [Fact]
    public async Task A_matrix_that_fits_stays_on_one_page()
    {
        var rendered = await Matrix(3).PaginateAsync();

        rendered.Pages.Count.Should().Be(1, "a small matrix is not paginated (no behaviour change)");
        BottomMm(rendered.Pages[0]).Should().BeLessThanOrEqualTo(PageMm(rendered.Pages[0]) + 0.5);
    }

    [Fact]
    public async Task The_page_header_appears_on_every_page_when_a_report_header_matrix_spans_pages()
    {
        // Regression: a Report Header taller than the page splits across pages, which advanced the page counter
        // before the page-1 page header was emitted — leaving page 1 without it.
        var rows = Enumerable.Range(1, 50).SelectMany(i => new[]
        {
            new MatrixCell($"C{i:000}", "ProdA", i * 1m),
            new MatrixCell($"C{i:000}", "ProdB", i * 2m),
        }).ToArray();

        var report = ReportBuilder.Create("WithPageHeader")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("D", rows)
            .PageHeader(ph => ph.Height(8).Text("CABECALHO DE PAGINA").At(0, 0).Size(180, 6))
            .ReportHeader(h => h.Height(20)
                .Tablix(t => t.RowGroup("Fields.Cli").ColumnGroup("Fields.Prod").Corner("Cliente").Cell("Fields.Val"))
                .At(0, 0).Size(180, 260))
            .Detail(d => d.Height(0))
            .Build();

        var rendered = await report.PaginateAsync();

        rendered.Pages.Count.Should().BeGreaterThan(1);
        foreach (var pg in rendered.Pages)
        {
            pg.Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text)
                .Should().Contain("CABECALHO DE PAGINA", "the page header must print on every page, including page 1");
        }
    }
}
