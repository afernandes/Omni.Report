using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record MesVenda(string Mes, decimal Total);

/// <summary>
/// Covers the code-first chart surface (<see cref="BandContent.Chart"/> /
/// <see cref="BandContent.Series"/>) at two levels: the fluent API building the
/// <see cref="ChartElement"/> model, and the paginator actually emitting drawing primitives
/// (bars, polylines, pie wedges, labels) — proving charts render, not just round-trip.
/// </summary>
public class ChartCodeFirstTests
{
    private static readonly MesVenda[] Rows =
    [
        new("Jan", 100m),
        new("Fev", 250m),
        new("Mar", 175m),
    ];

    [Fact]
    public void Chart_fluent_api_builds_chart_element_with_series()
    {
        var def = ReportBuilder.Create("c")
            .DataSource("Vendas", Rows)
            .ReportHeader(h => h.Height(80)
                .Chart(ChartKind.Pie, "Distribuição")
                    .At(0, 0).Size(100, 70)
                    .Series("Receita", "Fields.Mes", "Fields.Total")
                    .Series("Meta", "Fields.Mes", "Fields.Total")
                    .Legend(false))
            .Build().Definition;

        var chart = def.ReportHeader!.Elements.OfType<ChartElement>().Single();
        chart.Kind.Should().Be(ChartKind.Pie);
        chart.Title.Should().Be("Distribuição");
        chart.ShowLegend.Should().BeFalse();
        chart.Series.Should().HaveCount(2);
        chart.Series[0].Name.Should().Be("Receita");
        chart.Series[0].CategoryExpression.Should().Be("Fields.Mes");
        chart.Series[0].ValueExpression.Should().Be("Fields.Total");
    }

    [Fact]
    public async Task Bar_chart_emits_bars_title_and_axis_labels()
    {
        var prims = await RenderChartAsync(ChartKind.Bar);

        // 3 categories × 1 series = 3 bars (plus a legend swatch).
        prims.OfType<DrawRectanglePrimitive>().Count().Should().BeGreaterThan(2);

        var texts = prims.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();
        texts.Should().Contain("Vendas");      // title
        texts.Should().Contain("Jan");         // x-axis category label
        texts.Should().Contain("Receita");     // legend entry
    }

    [Fact]
    public async Task Line_chart_emits_open_polyline_through_every_category()
    {
        var prims = await RenderChartAsync(ChartKind.Line);

        var polylines = prims.OfType<DrawPolygonPrimitive>().Where(p => !p.Closed).ToList();
        polylines.Should().ContainSingle("one open polyline per series");
        polylines[0].Points.Count.Should().Be(3, "one vertex per category");
        polylines[0].Fill.Should().BeNull("a line series is stroked, not filled");
        polylines[0].Pen.Should().NotBeNull();
    }

    [Fact]
    public async Task Pie_chart_emits_one_closed_filled_wedge_per_category()
    {
        var prims = await RenderChartAsync(ChartKind.Pie);

        var wedges = prims.OfType<DrawPolygonPrimitive>().Where(p => p.Closed).ToList();
        wedges.Should().HaveCount(3, "one wedge per category");
        wedges.Should().OnlyContain(w => w.Fill != null);
        wedges.Should().OnlyContain(w => w.Points.Count > 3, "wedges approximate an arc with many vertices");
    }

    [Fact]
    public async Task Area_chart_fills_below_the_line()
    {
        var prims = await RenderChartAsync(ChartKind.Area);
        // An area series = a translucent closed fill polygon + the stroked open polyline on top.
        prims.OfType<DrawPolygonPrimitive>().Should().Contain(p => p.Closed && p.Fill != null,
            "the area below the line is filled");
        prims.OfType<DrawPolygonPrimitive>().Should().Contain(p => !p.Closed, "the line is stroked on top");
    }

    [Fact]
    public async Task Scatter_chart_emits_one_marker_per_point()
    {
        var prims = await RenderChartAsync(ChartKind.Scatter);
        // 3 categories × 1 series = 3 ellipse markers.
        prims.OfType<DrawEllipsePrimitive>().Count().Should().Be(3);
        // No data bars (the only rectangle, if any, is the legend swatch).
        prims.OfType<DrawRectanglePrimitive>().Count().Should().BeLessThan(2);
    }

    [Fact]
    public async Task Radar_chart_emits_a_closed_web_and_axis_labels()
    {
        var prims = await RenderChartAsync(ChartKind.Radar);
        // The series web is a closed, filled polygon (grid rings are closed but unfilled).
        prims.OfType<DrawPolygonPrimitive>().Should().Contain(p => p.Closed && p.Fill != null,
            "the series is drawn as a closed web");
        prims.OfType<DrawTextPrimitive>().Select(t => t.Text).Should().Contain("Jan", "category axis label");
    }

    private static async Task<List<LayoutPrimitive>> RenderChartAsync(ChartKind kind)
    {
        var report = ReportBuilder.Create("c")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Vendas", Rows)
            .ReportHeader(h => h.Height(80)
                .Chart(kind, "Vendas")
                    .At(0, 0).Size(170, 75)
                    .Series("Receita", "Fields.Mes", "Fields.Total"))
            .Build();

        var rendered = await report.PaginateAsync();
        return rendered.Pages.SelectMany(p => p.Primitives).ToList();
    }
}
