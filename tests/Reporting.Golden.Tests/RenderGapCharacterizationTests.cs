using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Styling;
using Xunit;

namespace Reporting.Golden.Tests;

/// <summary>
/// Pins two gaps the golden suite exposed on its first run. Both are cases where a property the
/// author set never becomes a <see cref="LayoutPrimitive"/> — so no backend can draw it, and the
/// goldens in <c>Goldens/</c> record the degraded output as today's truth.
/// </summary>
/// <remarks>
/// <para>These are characterization tests, following the repo's existing convention for known gaps
/// (<c>PaginationLimitationCharacterizationTests</c>): they assert what the engine <em>does</em>, not
/// what it should do. Each is written to fail loudly when the gap is closed, at which point it is
/// deleted and the affected goldens are re-accepted.</para>
///
/// <para>Neither is fixed here. Both need a new field on a primitive plus honouring it in Skia, GDI,
/// PDF, SVG and the HTML overlay — a change across every backend that does not belong in the PR that
/// introduces the golden suite.</para>
/// </remarks>
public class RenderGapCharacterizationTests
{
    /// <summary>
    /// <c>Style.Border</c> on a text element is dropped: it reaches no primitive.
    /// </summary>
    /// <remarks>
    /// <c>BandRenderer</c>'s generic element pass emits a background <see cref="DrawRectanglePrimitive"/>
    /// when the style has a fill, but never a stroked one — <c>ResolveBorderPen</c> has exactly one
    /// caller and it runs only for <c>RectangleElement</c> and <c>EllipseElement</c>. A bordered
    /// textbox is ordinary in SSRS-style reports, so this is a real gap and not an exotic edge.
    /// </remarks>
    [Fact]
    public async Task Border_on_a_text_element_reaches_no_primitive()
    {
        var report = ReportBuilder.Create("Borda")
            .Page(p => p.A5().Portrait().Margins(10))
            .ReportHeader(h => h.Height(20)
                .Label("Com borda").At(0, 0).Size(50, 8)
                    .Border(BorderLineStyle.Solid, 1.0, Color.FromHex("#0033AA")))
            .Build();

        var primitives = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();

        primitives.Should().ContainSingle().Which.Should().BeOfType<DrawTextPrimitive>(
            "the border produces no stroked rectangle of its own — see the method remarks");
    }

    /// <summary>
    /// <c>Rectangle().CornerRadius(n)</c> with no children draws square corners.
    /// </summary>
    /// <remarks>
    /// <see cref="DrawRectanglePrimitive"/> has no radius field. The radius survives only as
    /// <see cref="LayoutPrimitive.ClipCornerRadius"/>, which <c>BandRenderer</c> stamps onto the
    /// rectangle's <em>children</em> so their overflow is clipped to a rounded region. A rectangle used
    /// as a plain rounded shape has no children, so the radius reaches nothing — the exported SVG is a
    /// bare <c>&lt;rect&gt;</c> with no <c>rx</c>, which is exactly what <c>Goldens/Formas.svg.verified.txt</c>
    /// shows. Being honoured for containers is why the gap went unnoticed.
    /// </remarks>
    [Fact]
    public async Task Leaf_rounded_rectangle_loses_its_corner_radius()
    {
        var report = ReportBuilder.Create("Raio")
            .Page(p => p.A5().Portrait().Margins(10))
            .ReportHeader(h => h.Height(30)
                .Rectangle().At(0, 0).Size(40, 20).CornerRadius(5).Fill(Color.FromHex("#DDDDDD")))
            .Build();

        var rect = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawRectanglePrimitive>()
            .Should().ContainSingle().Subject;

        rect.Fill!.Color.Should().Be(Color.FromHex("#DDDDDD"), "the fill does survive");
        rect.ClipCornerRadius.Should().Be(Unit.Zero, "but nothing carries the 5 mm radius");
    }

    /// <summary>The complement of the test above: the same radius <em>is</em> honoured as a clip when
    /// there is a child to clip. Kept so a future fix does not trade one behaviour for the other.</summary>
    [Fact]
    public async Task Radius_does_reach_children_when_the_rectangle_is_a_container()
    {
        var report = ReportBuilder.Create("RaioContainer")
            .Page(p => p.A5().Portrait().Margins(10))
            .ReportHeader(h => h.Height(30)
                .Rectangle(inner => inner.Label("dentro").At(2, 2).Size(30, 5))
                    .At(0, 0).Size(40, 20).CornerRadius(5))
            .Build();

        var child = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
            .Should().ContainSingle().Subject;

        child.ClipCornerRadius.Should().Be(Unit.FromMm(5));
    }
}
