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
/// Layout-level gradient coverage: a gradient <see cref="Style"/> on a band element — and a conditional format
/// over it — must emit a <see cref="DrawRectanglePrimitive"/> whose <see cref="BrushStyle"/> carries the gradient.
/// This is the Style → StyleResolver → BrushStyle path that the serializer round-trip and Skia pixel tests don't
/// exercise (and where a regression would silently flatten the fill to a solid colour).
/// </summary>
public class GradientFillRenderTests
{
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);

    private static async Task<BrushStyle> FirstVisibleFill(TextBoxElement el)
    {
        var detail = new DetailBand(Unit.FromMm(20), EquatableArray.Create<ReportElement>(el));
        var def = new ReportDefinition("c", PageSetup.A4Portrait, detail);
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(def, [new Venda("c", "p", 1m)]));
        return report.Pages[0].Primitives.OfType<DrawRectanglePrimitive>()
            .Select(p => p.Fill).First(f => f is { IsVisible: true })!;
    }

    [Fact]
    public async Task A_gradient_style_emits_a_gradient_fill_primitive()
    {
        var fill = await FirstVisibleFill(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 10.Mm()),
            Expression = "x",
            Style = new Style(BackColor: Red, BackColorEnd: Blue, BackgroundGradient: BackgroundGradientType.TopBottom),
        });
        fill.HasGradient.Should().BeTrue("the band fill must carry the gradient through to the renderer");
        fill.Color.Should().Be(Red);
        fill.GradientEndColor.Should().Be(Blue);
        fill.Gradient.Should().Be(BackgroundGradientType.TopBottom);
    }

    [Fact]
    public async Task A_conditional_format_solid_backcolor_clears_the_base_gradient()
    {
        // Base is a gradient; a conditional format that fires sets a solid red. The rendered fill must be SOLID red,
        // not red→oldEnd — proving StyleResolver.Merge treats the background fill as a coherent unit.
        var fill = await FirstVisibleFill(new TextBoxElement
        {
            Id = "tb",
            Bounds = new Rectangle(0.Mm(), 0.Mm(), 50.Mm(), 10.Mm()),
            Expression = "x",
            Style = new Style(BackColor: Blue, BackColorEnd: Red, BackgroundGradient: BackgroundGradientType.TopBottom),
            ConditionalFormats = EquatableArray.Create(
                new ConditionalFormat("Fields.Total >= 1", new Style(BackColor: Red))),
        });
        fill.HasGradient.Should().BeFalse("a solid conditional-format override clears the base gradient");
        fill.Color.Should().Be(Red);
    }
}
