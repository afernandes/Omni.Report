using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Styling;
using Xunit;

namespace Reporting.Layout.Tests;

/// <summary>
/// End-to-end resolution of named/reusable styles at render: an element's <see cref="Style.BasedOn"/> inherits the
/// report's named style as a base, the element's inline style overrides it, and a named style can itself chain to
/// another. Verified through the emitted primitives (background fill + text colour).
/// </summary>
public class NamedStylesRenderTests
{
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);
    private static readonly Color Green = Color.FromRgb(0, 128, 0);

    private static async Task<(BrushStyle? Fill, TextStyle Text)> RenderOne(TextBoxElement el, params (string Name, Style Style)[] named)
    {
        var detail = new DetailBand(Unit.FromMm(20), EquatableArray.Create<ReportElement>(el));
        var def = new ReportDefinition("c", PageSetup.A4Portrait, detail)
        {
            NamedStyles = new EquatableDictionary<string, Style>(named.ToDictionary(n => n.Name, n => n.Style)),
        };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, [new Venda("c", "p", 1m)]));
        var prims = report.Pages[0].Primitives;
        var fill = prims.OfType<DrawRectanglePrimitive>().Select(p => p.Fill).FirstOrDefault(f => f is { IsVisible: true });
        var text = prims.OfType<DrawTextPrimitive>().First().Style;
        return (fill, text);
    }

    private static TextBoxElement Box(Style style) => new()
    {
        Id = "tb",
        Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 10.Mm()),
        Expression = "x",
        Style = style,
    };

    [Fact]
    public async Task An_element_inherits_a_named_style()
    {
        var (fill, text) = await RenderOne(
            Box(new Style(BasedOn: "titulo")),
            ("titulo", new Style(ForeColor: Red, BackColor: Blue)));
        fill!.Color.Should().Be(Blue, "the background comes from the named style");
        text.ForeColor.Should().Be(Red, "the text colour comes from the named style");
    }

    [Fact]
    public async Task Inline_style_overrides_the_named_base()
    {
        var (fill, text) = await RenderOne(
            Box(new Style(ForeColor: Green, BasedOn: "titulo")),
            ("titulo", new Style(ForeColor: Red, BackColor: Blue)));
        text.ForeColor.Should().Be(Green, "the element's own ForeColor wins over the named base");
        fill!.Color.Should().Be(Blue, "the un-overridden BackColor still comes from the base");
    }

    [Fact]
    public async Task A_named_style_alignment_is_inherited()
    {
        // Alignment is non-nullable, so MergeNamedBase must special-case it — else a "centered" named style never
        // passes its alignment to an element that doesn't re-state it.
        var (_, text) = await RenderOne(
            Box(new Style(BasedOn: "centered")),
            ("centered", new Style(HorizontalAlignment: HorizontalAlignment.Center)));
        text.HorizontalAlignment.Should().Be(HorizontalAlignment.Center, "the named style's alignment is inherited");
    }

    [Fact]
    public async Task Inline_alignment_overrides_the_named_base()
    {
        var (_, text) = await RenderOne(
            Box(new Style(HorizontalAlignment: HorizontalAlignment.Right, BasedOn: "centered")),
            ("centered", new Style(HorizontalAlignment: HorizontalAlignment.Center)));
        text.HorizontalAlignment.Should().Be(HorizontalAlignment.Right, "the element's explicit alignment wins");
    }

    [Fact]
    public async Task A_named_style_inherits_from_another_named_style()
    {
        // 'filho' is based on 'pai'; the element is based on 'filho'. pai → BackColor, filho → ForeColor.
        var (fill, text) = await RenderOne(
            Box(new Style(BasedOn: "filho")),
            ("pai", new Style(BackColor: Blue)),
            ("filho", new Style(ForeColor: Red, BasedOn: "pai")));
        fill!.Color.Should().Be(Blue, "inherited through the chain from 'pai'");
        text.ForeColor.Should().Be(Red, "from 'filho'");
    }
}
