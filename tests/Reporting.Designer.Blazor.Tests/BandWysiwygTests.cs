using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Locks the design-vs-render WYSIWYG contract: a band in the canvas must be inset
/// horizontally by the same <see cref="PageSetup.Margins"/> the renderer uses for its
/// origin. Without this, an element placed at X=0 in the designer appears at the page's
/// edge but renders at <c>Margins.Left</c> in the PDF — and an element at X near the page
/// width visibly extends past the right margin in the design but gets clipped in render.
/// </summary>
public class BandWysiwygTests : BunitContext
{
    public BandWysiwygTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Page_padding_mirrors_pagesetup_margins_on_all_four_sides()
    {
        // Build a tiny report with explicit 10mm margins so the test's expected px values
        // are deterministic regardless of platform DPI. The .page element should carry
        // inline padding on all four sides matching the margins — that's what insets the
        // bands inside the safe content area and keeps the design WYSIWYG with the renderer.
        var vm = new ReportDefinitionViewModel("wysiwyg")
        {
            PageSetup = PageSetup.A4Portrait with { Margins = Thickness.Uniform(Unit.FromMm(10)) },
        };
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        vm.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(10)));

        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));

        var page = cut.Find("#designerPage");
        var style = page.GetAttribute("style") ?? string.Empty;
        // 10mm × 3.43px/mm ≈ 34.3px — the padding shorthand should carry that number.
        // We accept any 34.xx value to absorb ratio rounding.
        style.Should().Contain("padding:34", "page padding-top mirrors margins.Top in px");
        // All four sides must carry the same number (uniform 10mm margins). Match the
        // 4-value padding shorthand with any 34.xx value so we survive tiny px-per-mm
        // rounding drift across platforms.
        style.Should().MatchRegex(@"padding:34\.\d+px 34\.\d+px 34\.\d+px 34\.\d+px",
            "all four sides of the page padding must mirror the margins");
        style.Should().Contain("box-sizing:border-box",
            "border-box keeps the paper at page-width even with padding");
    }

    [Fact]
    public void Page_padding_tracks_pagesetup_margin_changes()
    {
        // Wider margins → bigger padding numbers. Proves the inline style is derived
        // from PageSetup, not hardcoded.
        var vm = new ReportDefinitionViewModel("wide-margins")
        {
            PageSetup = PageSetup.A4Portrait with { Margins = Thickness.Uniform(Unit.FromMm(25)) },
        };
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        vm.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(10)));

        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));
        var page = cut.Find("#designerPage");
        var style = page.GetAttribute("style") ?? string.Empty;
        // 25mm at 3.43px/mm ≈ 85.71px — assertion stays loose to survive small ratio drift.
        style.Should().MatchRegex(@"padding:(8[0-9]|9[0-9])");
    }

    [Fact]
    public void Band_strip_is_pushed_outside_the_page_via_inline_left()
    {
        // The band-strip carries band labels (RH/PH/DT/...). It must sit FULLY OUTSIDE
        // the white paper so the paper itself shows clean. Inline left = -(38 + marginLeft px).
        var vm = new ReportDefinitionViewModel("strip-outside")
        {
            PageSetup = PageSetup.A4Portrait with { Margins = Thickness.Uniform(Unit.FromMm(10)) },
        };
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        vm.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(10)));

        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));
        var strip = cut.Find(".band-strip");
        var style = strip.GetAttribute("style") ?? string.Empty;
        // 38px (strip width) + 34.29px (10mm) = 72.29px to the left of the band's edge.
        // We accept any value ≥ 70 so the test is resilient to small px-per-mm rounding.
        style.Should().MatchRegex(@"left:-(7[0-9]|8[0-9])",
            "strip must clear strip-width + marginLeft to sit outside the paper");
    }

    [Fact]
    public void An_image_with_inline_bytes_renders_a_real_img_on_the_canvas()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG signature
        var vm = new ReportDefinitionViewModel("img");
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(20));
        band.AddElement(new ElementViewModel(DesignerElementKind.Image, "i1")
        {
            InlineImageData = png,
            ImageSizing = ImageSizing.Fit,
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(30), Unit.FromMm(20)),
        });
        vm.AddBand(band);

        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));

        var img = cut.Find("img");
        (img.GetAttribute("src") ?? "").Should().StartWith("data:image/png;base64,", "embedded image renders WYSIWYG");
        (img.GetAttribute("style") ?? "").Should().Contain("object-fit:contain", "ImageSizing.Fit → contain");
    }

    [Fact]
    public void An_image_without_bytes_falls_back_to_the_placeholder()
    {
        var vm = new ReportDefinitionViewModel("noimg");
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(20));
        band.AddElement(new ElementViewModel(DesignerElementKind.Image, "i2")
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(30), Unit.FromMm(20)),
        });
        vm.AddBand(band);

        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));

        cut.FindAll("img").Should().BeEmpty("no resolvable source → no <img>");
        cut.Markup.Should().Contain("Imagem", "the placeholder caption is shown instead");
    }

    private IRenderedComponent<BandCanvas> RenderViz(DesignerElementKind kind, Action<ElementViewModel>? configure = null)
    {
        var vm = new ReportDefinitionViewModel(kind.ToString());
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(40));
        var el = new ElementViewModel(kind, "e1")
        {
            Bounds = new Rectangle(Unit.Zero, Unit.Zero, Unit.FromMm(50), Unit.FromMm(40)),
        };
        configure?.Invoke(el);
        band.AddElement(el);
        vm.AddBand(band);
        return Render<BandCanvas>(p => p.Add(c => c.Report, vm));
    }

    [Fact]
    public void A_bar_chart_renders_a_representative_svg_not_a_placeholder()
    {
        var cut = RenderViz(DesignerElementKind.Chart, e => { e.ChartKind = ChartKind.Bar; e.ChartTitle = "Vendas"; });
        cut.FindAll("svg").Should().NotBeEmpty("the chart shows a design-time SVG");
        cut.FindAll("rect").Count.Should().BeGreaterThan(3, "the bar-chart preview draws sample bars");
        cut.Markup.Should().Contain("Vendas", "the chart title shows in the preview");
        cut.Markup.Should().NotContain("📊", "no dashed placeholder caption");
    }

    [Fact]
    public void A_radial_gauge_renders_an_arc_preview()
    {
        var cut = RenderViz(DesignerElementKind.Gauge, e => e.GaugeType = GaugeKind.Radial);
        cut.FindAll("svg").Should().NotBeEmpty();
        cut.FindAll("path").Should().NotBeEmpty("the radial gauge preview draws an arc path");
    }

    [Fact]
    public void A_databar_renders_a_filled_bar_preview()
    {
        var cut = RenderViz(DesignerElementKind.DataBar);
        cut.FindAll("svg").Should().NotBeEmpty();
        cut.FindAll("rect").Count.Should().BeGreaterThanOrEqualTo(2, "track + fill");
    }

    [Fact]
    public void A_matrix_tablix_renders_a_structural_grid()
    {
        var cut = RenderViz(DesignerElementKind.Tablix, e => { e.SetTablixMatrix(true); e.TablixCorner = "Região"; });
        cut.FindAll("table").Should().NotBeEmpty("the tablix shows a structural grid, not a dashed box");
        cut.Markup.Should().Contain("Região", "the corner label appears in the grid");
        cut.Markup.Should().NotContain("border:1px dashed", "no placeholder box");
    }

    [Fact]
    public void A_flat_tablix_renders_header_cells()
    {
        var cut = RenderViz(DesignerElementKind.Tablix, e => e.AddTablixColumn());
        cut.FindAll("table").Should().NotBeEmpty();
        cut.FindAll("th").Should().NotBeEmpty("a flat table preview has column headers");
    }
}
