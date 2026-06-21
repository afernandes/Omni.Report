using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

public sealed record Item(string Nome, decimal Valor);

/// <summary>
/// Covers the code-first KPI surface (<see cref="BandContent.DataBar"/> / <see cref="BandContent.Gauge"/>)
/// at the model and the rendered-primitive level — proving gauges and data bars draw, not just
/// round-trip. Gauge/DataBar evaluate their value once against the band context (so an aggregate
/// in a report footer shows the report total).
/// </summary>
public class KpiCodeFirstTests
{
    private static readonly Item[] Rows = [new("A", 60m), new("B", 90m)]; // sum = 150

    [Fact]
    public void DataBar_fluent_builds_element_with_value_and_range()
    {
        var def = ReportBuilder.Create("c")
            .DataSource("Itens", Rows)
            .Detail(d => d.Height(6)
                .DataBar("Fields.Valor", "#1D4ED8").At(0, 0).Size(40, 4).Range(0, 200))
            .Build().Definition;

        var bar = def.Detail.Elements.OfType<DataBarElement>().Single();
        bar.ValueExpression.Should().Be("Fields.Valor");
        bar.FillColor.Should().Be("#1D4ED8");
        bar.MinimumExpression.Should().Be("0");
        bar.MaximumExpression.Should().Be("200");
    }

    [Fact]
    public void Gauge_fluent_builds_element_with_kind_and_band()
    {
        var def = ReportBuilder.Create("c")
            .DataSource("Itens", Rows)
            .ReportFooter(f => f.Height(40)
                .Gauge("Sum(Fields.Valor)", GaugeKind.Radial).At(0, 0).Size(60, 35)
                    .Range(0, 200)
                    .GaugeBand(0, 100, "#EF4444")
                    .GaugeBand(100, 200, "#16A34A"))
            .Build().Definition;

        var gauge = def.ReportFooter!.Elements.OfType<GaugeElement>().Single();
        gauge.Kind.Should().Be(GaugeKind.Radial);
        gauge.ValueExpression.Should().Be("Sum(Fields.Valor)");
        gauge.MaximumExpression.Should().Be("200");
        gauge.Ranges.Should().HaveCount(2);
        gauge.Ranges[1].ColorHex.Should().Be("#16A34A");
    }

    [Fact]
    public async Task DataBar_renders_track_and_proportional_fill()
    {
        // One row, value 60 in [0,200] → fill ≈ 30% of the 40mm track.
        var report = ReportBuilder.Create("c")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", new[] { new Item("A", 60m) })
            .Detail(d => d.Height(6)
                .DataBar("Fields.Valor").At(0, 0).Size(40, 4).Range(0, 200))
            .Build();

        var prims = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
        var rects = prims.OfType<DrawRectanglePrimitive>().OrderByDescending(r => r.Bounds.Width.ToMm()).ToList();

        rects.Should().HaveCount(2, "a data bar is a track plus a proportional fill");
        rects[0].Bounds.Width.ToMm().Should().BeApproximately(40, 0.5, "track spans the full element");
        rects[1].Bounds.Width.ToMm().Should().BeApproximately(12, 0.5, "fill is 60/200 of 40mm");
    }

    [Fact]
    public async Task Radial_gauge_renders_rings_needle_and_value_label()
    {
        var prims = await RenderGaugeAsync(GaugeKind.Radial);

        prims.OfType<DrawPolygonPrimitive>().Where(p => p.Closed && p.Fill != null)
            .Should().HaveCountGreaterThan(1, "background ring plus coloured bands");
        prims.OfType<DrawLinePrimitive>().Should().NotBeEmpty("the needle is a line");
        prims.OfType<DrawTextPrimitive>().Select(t => t.Text).Should().Contain("150", "value label = Sum");
    }

    [Fact]
    public async Task Linear_gauge_renders_track_measure_and_value_label()
    {
        var prims = await RenderGaugeAsync(GaugeKind.Linear);

        prims.OfType<DrawRectanglePrimitive>().Count().Should().BeGreaterThan(1, "track + bands + measure");
        prims.OfType<DrawTextPrimitive>().Select(t => t.Text).Should().Contain("150");
    }

    [Theory]
    [InlineData("en-US")] // thousands grouped with a comma
    [InlineData("pt-BR")] // thousands grouped with a dot (also the engine default)
    public async Task Gauge_value_label_uses_the_report_culture(string language)
    {
        // Sum = 1500 (≥ 1000) exercises the grouped value label. Compare against the culture's OWN
        // grouped rendering — ICU (Linux/CI) vs NLS (Windows) disagree on separators, so a literal flakes.
        var label = await RenderGaugeValueLabelAsync(language);
        var culture = System.Globalization.CultureInfo.GetCultureInfo(language);

        label.Should().Be((1500d).ToString("#,0", culture));
    }

    [Fact]
    public async Task Gauge_value_label_without_language_keeps_the_default_pt_BR()
    {
        // Opt-in: absent Language → pt-BR default, so gauges that never declared a culture render
        // exactly as before (no golden break).
        var label = await RenderGaugeValueLabelAsync(language: null);
        var ptBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

        label.Should().Be((1500d).ToString("#,0", ptBr));
    }

    private static async Task<string> RenderGaugeValueLabelAsync(string? language)
    {
        Item[] rows = [new("A", 1000m), new("B", 500m)]; // sum = 1500
        var builder = ReportBuilder.Create("c");
        if (language is not null)
        {
            builder = builder.Language(language);
        }
        var report = builder
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", rows)
            .ReportFooter(f => f.Height(45)
                .Gauge("Sum(Fields.Valor)", GaugeKind.Radial).At(0, 0).Size(80, 40)
                    .Range(0, 2000))
            .Build();

        var prims = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
        // A gauge emits exactly one text primitive: the value label.
        return prims.OfType<DrawTextPrimitive>().Single().Text;
    }

    private static async Task<List<LayoutPrimitive>> RenderGaugeAsync(GaugeKind kind)
    {
        var report = ReportBuilder.Create("c")
            .Page(p => p.A4().Portrait().Margins(15))
            .DataSource("Itens", Rows)
            .ReportFooter(f => f.Height(45)
                .Gauge("Sum(Fields.Valor)", kind).At(0, 0).Size(80, 40)
                    .Range(0, 200)
                    .GaugeBand(0, 100, "#EF4444")
                    .GaugeBand(100, 200, "#16A34A"))
            .Build();

        return (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
    }
}
