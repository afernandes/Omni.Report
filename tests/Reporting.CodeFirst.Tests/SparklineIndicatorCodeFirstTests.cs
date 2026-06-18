using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

/// <summary>
/// Covers the code-first sparkline and indicator surface (model + rendered primitives).
/// Sparklines iterate their data source (one point per row); indicators evaluate a single
/// value against state ranges and pick an icon (arrow/shape/rating) and a semantic colour.
/// </summary>
public class SparklineIndicatorCodeFirstTests
{
    private static readonly Item[] Rows = [new("A", 60m), new("B", 90m), new("C", 30m), new("D", 120m)]; // sum 300

    [Fact]
    public void Sparkline_fluent_builds_element()
    {
        var def = ReportBuilder.Create("c")
            .DataSource("Itens", Rows)
            .ReportHeader(h => h.Height(30)
                .Sparkline("Fields.Valor", SparklineKind.Area, "Itens").At(0, 0).Size(60, 20))
            .Build().Definition;

        var spark = def.ReportHeader!.Elements.OfType<SparklineElement>().Single();
        spark.Kind.Should().Be(SparklineKind.Area);
        spark.ValueExpression.Should().Be("Fields.Valor");
        spark.DataSetName.Should().Be("Itens");
    }

    [Fact]
    public void Indicator_fluent_builds_element_with_states()
    {
        var def = ReportBuilder.Create("c")
            .DataSource("Itens", Rows)
            .ReportFooter(f => f.Height(15)
                .Indicator("Sum(Fields.Valor)", IndicatorKind.Shape).At(0, 0).Size(10, 10)
                    .State(0, 50).State(50, 100))
            .Build().Definition;

        var ind = def.ReportFooter!.Elements.OfType<IndicatorElement>().Single();
        ind.Kind.Should().Be(IndicatorKind.Shape);
        ind.States.Should().HaveCount(2);
    }

    [Fact]
    public async Task Line_sparkline_emits_one_polyline_point_per_row()
    {
        var prims = await RenderSparkAsync(SparklineKind.Line);
        var polylines = prims.OfType<DrawPolygonPrimitive>().Where(p => !p.Closed).ToList();
        polylines.Should().ContainSingle();
        polylines[0].Points.Count.Should().Be(4);
    }

    [Fact]
    public async Task Column_sparkline_emits_one_bar_per_row()
    {
        var prims = await RenderSparkAsync(SparklineKind.Column);
        prims.OfType<DrawRectanglePrimitive>().Count().Should().Be(4);
    }

    [Fact]
    public async Task Area_sparkline_emits_closed_filled_polygon()
    {
        var prims = await RenderSparkAsync(SparklineKind.Area);
        var area = prims.OfType<DrawPolygonPrimitive>().Where(p => p.Closed && p.Fill != null).ToList();
        area.Should().ContainSingle();
        area[0].Points.Count.Should().Be(6, "4 data points + 2 baseline corners");
    }

    [Fact]
    public async Task Directional_indicator_emits_an_arrow_polygon()
    {
        // sum = 300 → matches the top state (200-400) → upward arrow (a closed triangle).
        var prims = await RenderIndicatorAsync(IndicatorKind.DirectionalArrow);
        prims.OfType<DrawPolygonPrimitive>().Where(p => p.Closed).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Rating_indicator_emits_one_bar_per_state()
    {
        var prims = await RenderIndicatorAsync(IndicatorKind.RatingBar);
        prims.OfType<DrawRectanglePrimitive>().Count().Should().Be(3);
    }

    [Fact]
    public async Task Shape_indicator_emits_a_filled_circle()
    {
        var prims = await RenderIndicatorAsync(IndicatorKind.Shape);
        prims.OfType<DrawEllipsePrimitive>().Should().ContainSingle();
    }

    private static async Task<List<LayoutPrimitive>> RenderSparkAsync(SparklineKind kind)
    {
        var report = ReportBuilder.Create("c")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Rows)
            .ReportHeader(h => h.Height(30)
                .Sparkline("Fields.Valor", kind).At(0, 0).Size(60, 20))
            .Build();
        return (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
    }

    private static async Task<List<LayoutPrimitive>> RenderIndicatorAsync(IndicatorKind kind)
    {
        var report = ReportBuilder.Create("c")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Rows)
            .ReportFooter(f => f.Height(15)
                .Indicator("Sum(Fields.Valor)", kind).At(0, 0).Size(12, 12)
                    .State(0, 100).State(100, 200).State(200, 400))
            .Build();
        return (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
    }
}
