using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record MatrixCell(string Cli, string Prod, decimal Val);

public sealed record ProdutoRow(string Cod, string Nome, decimal Preco);

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

    private static double RightMm(RenderedPage page) =>
        page.Primitives.Count == 0 ? 0 : page.Primitives.Max(p => p.Bounds.Right.ToPoints()) / 72.0 * 25.4;

    private static double PageWidthMm(RenderedPage page) => page.PageSetup.PageWidth.ToPoints() / 72.0 * 25.4;

    // A crosstab of 3 clients (rows) × `cols` product columns — wide but short. With MinColumnWidth set the
    // columns no longer fit the page, so the matrix paginates HORIZONTALLY (column tiles), repeating the row
    // headers; with it unset the columns just squeeze onto one page (classic behaviour).
    private static Report WideMatrix(int cols, double minColMm)
    {
        var rows = Enumerable.Range(1, 3).SelectMany(c =>
            Enumerable.Range(1, cols).Select(m => new MatrixCell($"C{c}", $"M{m:00}", c * m * 1m))).ToArray();

        return ReportBuilder.Create("WideMatrix")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("D", rows)
            .ReportHeader(h => h.Height(20)
                .Tablix(t =>
                {
                    t.RowGroup("Fields.Cli").ColumnGroup("Fields.Prod").Corner("Cliente").Cell("Fields.Val");
                    if (minColMm > 0) { t.MinColumnWidth(minColMm); }
                })
                .At(0, 0).Size(180, 40))
            .Detail(d => d.Height(0))
            .Build();
    }

    [Fact]
    public async Task A_wide_matrix_with_MinColumnWidth_paginates_across_columns()
    {
        var rendered = await WideMatrix(cols: 20, minColMm: 25).PaginateAsync();

        rendered.Pages.Count.Should().BeGreaterThan(1, "20 columns at 25mm are far wider than one A4 page");
        foreach (var pg in rendered.Pages)
        {
            RightMm(pg).Should().BeLessThanOrEqualTo(PageWidthMm(pg) + 0.5, "no column tile overflows the page width");
        }
    }

    [Fact]
    public async Task Each_column_tile_repeats_the_row_headers()
    {
        var rendered = await WideMatrix(cols: 20, minColMm: 25).PaginateAsync();

        foreach (var pg in rendered.Pages)
        {
            var texts = pg.Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
            texts.Should().Contain("Cliente", "the corner/row-header reprints on every column tile");
            texts.Should().Contain("C1").And.Contain("C2").And.Contain("C3", "every column tile shows all row headers");
        }
    }

    [Fact]
    public async Task Every_column_value_survives_horizontal_tiling()
    {
        var rendered = await WideMatrix(cols: 20, minColMm: 25).PaginateAsync();

        var texts = rendered.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToHashSet();
        for (int m = 1; m <= 20; m++)
        {
            texts.Should().Contain($"M{m:00}", "no column header may be dropped when the matrix tiles across pages");
        }
    }

    [Fact]
    public async Task A_wide_matrix_without_MinColumnWidth_squeezes_onto_one_page()
    {
        var rendered = await WideMatrix(cols: 20, minColMm: 0).PaginateAsync();

        rendered.Pages.Count.Should().Be(1, "without MinColumnWidth the columns squeeze to fit (no horizontal tiling)");
    }

    [Fact]
    public async Task A_matrix_big_in_both_dimensions_tiles_rows_and_columns()
    {
        // 50 clients (rows) × 20 products (columns) with MinColumnWidth: too TALL and too WIDE — it must tile on
        // BOTH axes (the nested row-band × column-tile loop, "Across then Down"). Regression guard for the 2D path.
        var rows = Enumerable.Range(1, 50).SelectMany(c =>
            Enumerable.Range(1, 20).Select(m => new MatrixCell($"C{c:000}", $"M{m:00}", c * m * 1m))).ToArray();

        var report = ReportBuilder.Create("BigMatrix2D")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("D", rows)
            .ReportHeader(h => h.Height(20)
                .Tablix(t => t.RowGroup("Fields.Cli").ColumnGroup("Fields.Prod").Corner("Cliente").Cell("Fields.Val").MinColumnWidth(25))
                .At(0, 0).Size(180, 260))
            .Detail(d => d.Height(0))
            .Build();

        var rendered = await report.PaginateAsync();

        rendered.Pages.Count.Should().BeGreaterThan(1, "a 50×20 matrix tiles on both axes");
        foreach (var pg in rendered.Pages)
        {
            BottomMm(pg).Should().BeLessThanOrEqualTo(PageMm(pg) + 0.5, "no tile overflows the page height");
            RightMm(pg).Should().BeLessThanOrEqualTo(PageWidthMm(pg) + 0.5, "no tile overflows the page width");
        }
        var texts = rendered.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToHashSet();
        for (int c = 1; c <= 50; c++) { texts.Should().Contain($"C{c:000}", "every row header survives 2D tiling"); }
        for (int m = 1; m <= 20; m++) { texts.Should().Contain($"M{m:00}", "every column header survives 2D tiling"); }
    }

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

    // ---- Flat (non-matrix) table pagination — same row-level splitting as the matrix path ----

    // A flat table of `n` rows in the ReportHeader (renders once). With many rows it is taller than one A4 page,
    // forcing the flat table to paginate by row, reprinting the header (like the matrix).
    private static Report FlatTable(int n, bool repeatHeaders = true, bool keepTogether = false)
    {
        var rows = Enumerable.Range(1, n).Select(i => new ProdutoRow($"P{i:000}", $"Produto {i}", i * 1.5m)).ToArray();

        return ReportBuilder.Create("BigFlat")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("D", rows)
            .ReportHeader(h => h.Height(20)
                .Tablix(t =>
                {
                    t.Column("CODIGO", "Fields.Cod").Column("NOME", "Fields.Nome").Column("PRECO", "{Fields.Preco:C}");
                    if (!repeatHeaders) { t.RepeatColumnHeaders(false); }
                    if (keepTogether) { t.KeepTogether(); }
                })
                .At(0, 0).Size(180, 260))
            .Detail(d => d.Height(0))
            .Build();
    }

    [Fact]
    public async Task A_tall_flat_table_paginates_across_pages_without_overflow()
    {
        var rendered = await FlatTable(60).PaginateAsync();

        rendered.Pages.Count.Should().BeGreaterThan(1, "60 rows are taller than one A4 page");
        foreach (var pg in rendered.Pages)
        {
            BottomMm(pg).Should().BeLessThanOrEqualTo(PageMm(pg) + 0.5, "no content overflows the physical page");
        }
    }

    [Fact]
    public async Task The_flat_header_reprints_on_every_page_by_default()
    {
        var rendered = await FlatTable(60).PaginateAsync();

        foreach (var pg in rendered.Pages)
        {
            pg.Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text)
                .Should().Contain("CODIGO", "the flat table's header row reprints at the top of each continuation page");
        }
    }

    [Fact]
    public async Task Every_flat_row_survives_pagination()
    {
        var rendered = await FlatTable(60).PaginateAsync();

        var texts = rendered.Pages.SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToHashSet();
        for (int i = 1; i <= 60; i++)
        {
            texts.Should().Contain($"P{i:000}", "no data row may be dropped when the flat table splits");
        }
    }

    [Fact]
    public async Task Flat_RepeatColumnHeaders_false_prints_the_header_only_on_the_first_page()
    {
        var rendered = await FlatTable(60, repeatHeaders: false).PaginateAsync();

        rendered.Pages.Count.Should().BeGreaterThan(1);
        rendered.Pages[0].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text).Should().Contain("CODIGO");
        rendered.Pages[1].Primitives.OfType<DrawTextPrimitive>().Select(t => t.Text)
            .Should().NotContain("CODIGO", "continuation pages omit the header when RepeatColumnHeaders is off");
    }

    [Fact]
    public async Task Flat_KeepTogether_opts_out_of_pagination_keeping_the_table_on_one_page()
    {
        var rendered = await FlatTable(60, keepTogether: true).PaginateAsync();

        rendered.Pages.Count.Should().Be(1, "KeepTogether keeps the flat table atomic (it overflows rather than splitting)");
    }

    [Fact]
    public async Task A_flat_table_that_fits_stays_on_one_page()
    {
        var rendered = await FlatTable(3).PaginateAsync();

        rendered.Pages.Count.Should().Be(1, "a small flat table is not paginated (no behaviour change)");
        BottomMm(rendered.Pages[0]).Should().BeLessThanOrEqualTo(PageMm(rendered.Pages[0]) + 0.5);
    }
}
