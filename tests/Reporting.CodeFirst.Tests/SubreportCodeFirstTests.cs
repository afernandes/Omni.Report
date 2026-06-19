using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

/// <summary>
/// Covers the Subreport element end-to-end: the code-first surface
/// (<see cref="BandContent.SubreportInline"/> / <see cref="BandContent.Subreport"/>) and the
/// paginator actually rendering the child report's primitives offset into the subreport's bounds —
/// proving subreports render, not just round-trip.
/// </summary>
public class SubreportCodeFirstTests
{
    [Fact]
    public async Task Inline_subreport_renders_child_content_offset_into_parent()
    {
        var child = ReportBuilder.Create("Filho")
            .Page(p => p.A4().Portrait())
            .ReportHeader(h => h.Height(10).Label("CONTEUDO-FILHO").At(0, 0).Size(60, 8))
            .Build();

        var parent = ReportBuilder.Create("Pai")
            .Page(p => p.A4().Portrait().Margins(10))
            .ReportHeader(h => h.Height(50)
                .Label("PAI").At(0, 0).Size(50, 6)
                .SubreportInline(child.Definition).At(0, 20).Size(120, 25))
            .Build();

        var prims = (await parent.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
        var texts = prims.OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        texts.Should().Contain("PAI");
        texts.Should().Contain("CONTEUDO-FILHO",
            "the child report must render inside the subreport, not as a placeholder");

        // The child's label is translated to the subreport origin: parent top margin (10mm) +
        // subreport Y (20mm) ≈ 30mm down, parent left margin (10mm) across — proving the child's
        // primitives were offset into place rather than drawn at the child's own (0,0).
        var childText = prims.OfType<DrawTextPrimitive>().First(t => t.Text == "CONTEUDO-FILHO");
        childText.Bounds.Y.Should().BeGreaterThan(Unit.FromMm(25));
        childText.Bounds.X.Should().BeGreaterThanOrEqualTo(Unit.FromMm(10));
    }

    [Fact]
    public async Task Unresolved_subreport_id_renders_nothing_without_a_resolver()
    {
        var parent = ReportBuilder.Create("Pai")
            .Page(p => p.A4().Portrait().Margins(10))
            .ReportHeader(h => h.Height(40)
                .Label("PAI").At(0, 0).Size(50, 6)
                .Subreport("inexistente").At(0, 15).Size(120, 20))
            .Build();

        // No SubreportResolver supplied → the subreport contributes no primitives, and must not crash.
        var prims = (await parent.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();
        prims.OfType<DrawTextPrimitive>().Select(t => t.Text).Should().Contain("PAI");
    }
}
