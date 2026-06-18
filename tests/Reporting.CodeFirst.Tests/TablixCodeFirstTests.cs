using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record Produto(string Nome, decimal Preco);

/// <summary>
/// Covers the code-first Tablix (banded table) surface: the fluent
/// <see cref="BandContent.Tablix"/> builder producing the <see cref="TablixElement"/> model,
/// and the paginator rendering a header row + one detail row per data record, with gridlines.
/// </summary>
public class TablixCodeFirstTests
{
    private static readonly Produto[] Rows =
    [
        new("Caneta", 2.50m),
        new("Caderno", 27.40m),
        new("Lápis", 1.20m),
    ];

    [Fact]
    public void Tablix_fluent_builds_header_and_detail_cells_per_column()
    {
        var def = ReportBuilder.Create("t")
            .DataSource("Produtos", Rows)
            .ReportHeader(h => h.Height(60)
                .Tablix(t => t
                    .DataSet("Produtos")
                    .Column("Produto", "Fields.Nome")
                    .Column("Preço", "{Fields.Preco:C}"))
                .At(0, 0).Size(120, 50))
            .Build().Definition;

        var tablix = def.ReportHeader!.Elements.OfType<TablixElement>().Single();
        tablix.DataSetName.Should().Be("Produtos");
        tablix.Cells.Should().HaveCount(4); // 2 columns × (header + detail)
        tablix.Cells.Count(c => c.RowIndex == 0).Should().Be(2, "one header cell per column");
        tablix.Cells.Count(c => c.RowIndex == 1).Should().Be(2, "one detail cell per column");
    }

    [Fact]
    public async Task Tablix_renders_header_data_rows_and_gridlines()
    {
        var report = ReportBuilder.Create("Tabela")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Produtos", Rows)
            .ReportHeader(h => h.Height(60)
                .Tablix(t => t
                    .Column("Produto", "Fields.Nome")
                    .Column("Preço", "{Fields.Preco:C}"))
                .At(0, 0).Size(120, 50))
            .Build();

        var prims = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
        var texts = prims.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        texts.Should().Contain("Produto");                       // column header
        texts.Should().Contain("Preço");
        texts.Should().Contain("Caneta");                        // a data row value
        texts.Should().Contain(t => t.Contains("R$"), "the price column uses the :C currency format");

        prims.OfType<DrawRectanglePrimitive>().Should().NotBeEmpty("the header row is filled");
        // 2 cols + 4 rows (1 header + 3 data) → 3 vertical + 5 horizontal gridlines.
        prims.OfType<DrawLinePrimitive>().Count().Should().BeGreaterThanOrEqualTo(8);
    }
}
