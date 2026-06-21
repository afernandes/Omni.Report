using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record PosItem(string Nome, decimal Valor);

/// <summary>End-to-end: positional functions resolve during render — RowNumber advances per detail row.</summary>
public class PositionalRenderTests
{
    [Fact]
    public async Task RowNumber_advances_per_detail_row()
    {
        var itens = new[] { new PosItem("A", 10m), new PosItem("B", 20m), new PosItem("C", 30m) };

        var report = ReportBuilder.Create("Pos")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Itens", itens)
            .Detail(d => d.Height(6)
                .Text("{RowNumber()}").At(0, 0).Size(20, 6)
                .Text("{Fields.Nome}").At(20, 0).Size(40, 6))
            .Build();

        var texts = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        // One RowNumber per row: 1, 2, 3 — paired with A, B, C.
        texts.Should().Contain("1").And.Contain("2").And.Contain("3");
        texts.Should().Contain("A").And.Contain("B").And.Contain("C");
    }
}
