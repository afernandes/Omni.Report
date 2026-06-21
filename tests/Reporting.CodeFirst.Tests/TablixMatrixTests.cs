using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record VendaRegional(string Regiao, string Mes, decimal Total);

public sealed record VendaDetalhada(string Regiao, string Cidade, string Mes, decimal Total);

public sealed record VendaUF(string Regiao, string Estado, string Cidade, string Mes, decimal Total);

/// <summary>
/// Covers the Tablix <b>matrix/crosstab</b> path: a row group × column group with a summed body
/// cell, proving the renderer pivots the data (not just a flat table) — code-first
/// (<see cref="TablixBuilder.RowGroup"/>/<see cref="TablixBuilder.ColumnGroup"/>/<see cref="TablixBuilder.Cell"/>).
/// </summary>
public class TablixMatrixTests
{
    private static readonly VendaRegional[] Rows =
    [
        new("Sul",   "Jan", 100m),
        new("Sul",   "Fev", 200m),
        new("Norte", "Jan",  50m),
        new("Norte", "Fev",  70m),
        new("Sul",   "Jan",  25m), // second Sul/Jan → the cell must sum to 125
    ];

    [Fact]
    public async Task Matrix_crosstabs_rows_by_columns_and_sums_the_intersection()
    {
        var report = ReportBuilder.Create("Crosstab")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", Rows)
            .ReportHeader(h => h.Height(60)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao")
                    .ColumnGroup("Fields.Mes")
                    .Corner("Região")
                    .Cell("Fields.Total"))
                .At(0, 0).Size(150, 40))
            .Build();

        var prims = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
        var texts = prims.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        // Corner + distinct column values (months) + distinct row values (regions).
        texts.Should().Contain("Região");
        texts.Should().Contain("Jan");
        texts.Should().Contain("Fev");
        texts.Should().Contain("Sul");
        texts.Should().Contain("Norte");

        // Intersections are summed: Sul/Jan = 100 + 25 = 125; Norte/Fev = 70.
        texts.Should().Contain(t => t.Contains("125"), "Sul×Jan sums the two matching rows");
        texts.Should().Contain(t => t.Contains("70"), "Norte×Fev");
    }

    [Fact]
    public async Task Nested_row_groups_render_both_levels_and_sum_each_leaf()
    {
        VendaDetalhada[] rows =
        [
            new("Sul",   "Porto Alegre", "Jan", 100m),
            new("Sul",   "Curitiba",     "Jan",  40m),
            new("Sul",   "Porto Alegre", "Fev",  60m),
            new("Norte", "Manaus",       "Jan",  30m),
        ];

        var report = ReportBuilder.Create("NestedCrosstab")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", rows)
            .ReportHeader(h => h.Height(70)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao")
                    .RowGroup("Fields.Cidade")   // nested under região
                    .ColumnGroup("Fields.Mes")
                    .Corner("Local")
                    .Cell("Fields.Total"))
                .At(0, 0).Size(170, 55))
            .Build();

        var texts = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        // Both group levels surface as headers (outer once, inner per city).
        texts.Should().Contain("Sul").And.Contain("Norte");
        texts.Should().Contain("Porto Alegre").And.Contain("Curitiba").And.Contain("Manaus");
        texts.Should().Contain("Jan").And.Contain("Fev");

        // Leaf intersections sum independently within the nested path.
        texts.Should().Contain(t => t.Contains("100"), "Sul→Porto Alegre→Jan");
        texts.Should().Contain(t => t.Contains("40"), "Sul→Curitiba→Jan");
    }

    [Fact]
    public async Task Row_subtotals_emit_group_totals_and_a_grand_total()
    {
        VendaDetalhada[] rows =
        [
            new("Sul",   "Porto Alegre", "Jan", 100m),
            new("Sul",   "Curitiba",     "Jan",  40m),
            new("Sul",   "Porto Alegre", "Fev",  60m),
            new("Norte", "Manaus",       "Jan",  30m),
        ];

        Report Build(bool subtotals) => ReportBuilder.Create("Subtotais")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", rows)
            .ReportHeader(h => h.Height(90)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao")
                    .RowGroup("Fields.Cidade")
                    .ColumnGroup("Fields.Mes")
                    .Corner("Local")
                    .Cell("Fields.Total")
                    .RowSubtotals(subtotals))
                .At(0, 0).Size(170, 80))
            .Build();

        // Without subtotals there are no total rows.
        var plain = await TextsOf(Build(false));
        plain.Should().NotContain(t => t.Contains("Total geral"));

        var withTotals = await TextsOf(Build(true));
        withTotals.Should().Contain("Total Sul").And.Contain("Total Norte").And.Contain("Total geral");
        // Sul subtotal: Jan = 100 + 40 = 140; grand total Jan = 140 + 30 = 170.
        withTotals.Should().Contain(t => t.Contains("140"), "Sul subtotal sums its cities for Jan");
        withTotals.Should().Contain(t => t.Contains("170"), "grand total sums all regions for Jan");
    }

    [Fact]
    public async Task Row_subtotals_with_a_single_level_emit_only_the_grand_total()
    {
        var report = ReportBuilder.Create("GrandOnly")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", Rows) // single row level (Regiao)
            .ReportHeader(h => h.Height(70)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao")
                    .ColumnGroup("Fields.Mes")
                    .Corner("Região")
                    .Cell("Fields.Total")
                    .RowSubtotals())
                .At(0, 0).Size(150, 60))
            .Build();

        var texts = await TextsOf(report);
        // No inner group to subtotal → only the grand total fires (guard nRowLevels >= 2).
        texts.Should().Contain("Total geral");
        texts.Should().NotContain("Total Sul").And.NotContain("Total Norte");
        // Grand total Jan = Sul(100+25) + Norte(50) = 175.
        texts.Should().Contain(t => t.Contains("175"), "grand total sums every region for Jan");
    }

    [Fact]
    public async Task Row_subtotals_nest_for_three_levels_inner_before_outer()
    {
        VendaUF[] rows =
        [
            new("Sul",   "RS", "Porto Alegre", "Jan", 100m),
            new("Sul",   "RS", "Caxias",       "Jan",  20m),
            new("Sul",   "PR", "Curitiba",     "Jan",  40m),
            new("Norte", "AM", "Manaus",       "Jan",  30m),
        ];

        var report = ReportBuilder.Create("ThreeLevel")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", rows)
            .ReportHeader(h => h.Height(130)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao")
                    .RowGroup("Fields.Estado")
                    .RowGroup("Fields.Cidade")
                    .ColumnGroup("Fields.Mes")
                    .Corner("Local")
                    .Cell("Fields.Total")
                    .RowSubtotals())
                .At(0, 0).Size(170, 120))
            .Build();

        var texts = await TextsOf(report);
        // Subtotals at both outer levels (Estado, Regiao) + grand total.
        texts.Should().Contain("Total RS").And.Contain("Total PR")
             .And.Contain("Total Sul").And.Contain("Total geral");
        // RS = 100+20 = 120; Sul = 120+40 = 160; grand = 160+30 = 190.
        texts.Should().Contain(t => t.Contains("120")).And.Contain(t => t.Contains("160")).And.Contain(t => t.Contains("190"));
        // Inner subtotal (Estado) renders before the outer one (Regiao).
        texts.IndexOf("Total RS").Should().BeLessThan(texts.IndexOf("Total Sul"));
    }

    public sealed record VendaAnoMes(string Regiao, string Ano, string Mes, decimal Total);

    [Fact]
    public async Task Column_subtotals_emit_per_group_and_grand_total_columns()
    {
        VendaAnoMes[] rows =
        [
            new("Sul",   "2023", "Jan", 100m),
            new("Sul",   "2023", "Fev",  40m),
            new("Sul",   "2024", "Jan",  60m),
            new("Norte", "2023", "Jan",  30m),
        ];

        Report Build(bool colSub) => ReportBuilder.Create("ColSub")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", rows)
            .ReportHeader(h => h.Height(90)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao")
                    .ColumnGroup("Fields.Ano")
                    .ColumnGroup("Fields.Mes")
                    .Corner("Região")
                    .Cell("Fields.Total")
                    .ColumnSubtotals(colSub))
                .At(0, 0).Size(180, 80))
            .Build();

        // Without column subtotals, no total columns are added.
        var plain = await TextsOf(Build(false));
        plain.Should().NotContain(t => t.Contains("Total geral"));

        var withTotals = await TextsOf(Build(true));
        // Subtotal column header per outer (Ano) block + a grand-total column header.
        withTotals.Should().Contain("Total 2023").And.Contain("Total 2024").And.Contain("Total geral");
        // Sul: 2023 subtotal = 100 + 40 = 140; grand = 140 + 60 = 200.
        withTotals.Should().Contain(t => t.Contains("140"), "Sul 2023 subtotal sums its months");
        withTotals.Should().Contain(t => t.Contains("200"), "Sul grand total sums all years");
    }

    [Fact]
    public async Task Custom_total_labels_replace_the_defaults()
    {
        var report = ReportBuilder.Create("Labels")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", Rows)
            .ReportHeader(h => h.Height(80)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao")
                    .ColumnGroup("Fields.Mes")
                    .Corner("Região")
                    .Cell("Fields.Total")
                    .RowSubtotals()
                    .TotalLabels(subtotal: "Soma {0}", grandTotal: "Geral"))
                .At(0, 0).Size(150, 70))
            .Build();

        var texts = await TextsOf(report);
        texts.Should().Contain("Geral").And.NotContain("Total geral");
    }

    private static Report SortedMatrix(string? sort, bool descending) =>
        ReportBuilder.Create("Sorted")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", Rows) // data order encounters "Sul" before "Norte"
            .ReportHeader(h => h.Height(60)
                .Tablix(t => t
                    .RowGroup("Fields.Regiao", sort, descending)
                    .ColumnGroup("Fields.Mes")
                    .Corner("Região")
                    .Cell("Fields.Total"))
                .At(0, 0).Size(150, 40))
            .Build();

    private static async Task<List<string>> TextsOf(Report report) =>
        (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

    [Fact]
    public async Task Row_group_SortExpression_orders_group_instances_ascending()
    {
        // Without a sort the groups keep data order (Sul encountered first)…
        var unsorted = await TextsOf(SortedMatrix(null, false));
        unsorted.IndexOf("Sul").Should().BeLessThan(unsorted.IndexOf("Norte"));

        // …with an ascending SortExpression the instances sort by region (Norte before Sul).
        var sorted = await TextsOf(SortedMatrix("Fields.Regiao", false));
        sorted.IndexOf("Norte").Should().BeGreaterThanOrEqualTo(0);
        sorted.IndexOf("Norte").Should().BeLessThan(sorted.IndexOf("Sul"));
    }

    [Fact]
    public async Task Row_group_SortExpression_descending_reverses_order()
    {
        var desc = await TextsOf(SortedMatrix("Fields.Regiao", true));
        desc.IndexOf("Sul").Should().BeLessThan(desc.IndexOf("Norte"), "descending puts Sul before Norte");
    }
}
