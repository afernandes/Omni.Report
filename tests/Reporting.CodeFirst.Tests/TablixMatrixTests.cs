using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record VendaRegional(string Regiao, string Mes, decimal Total);

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
}
