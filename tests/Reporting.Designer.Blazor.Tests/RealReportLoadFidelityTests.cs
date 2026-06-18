using System.Globalization;
using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.ViewModels;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// End-to-end fidelity tests for the .repx load pipeline. These open real .repx files
/// produced by <c>Reporting.Samples.CodeFirst</c> (including Vendas/Espelho/Caixa/NFC-e)
/// and assert:
///  1. Every band's elements survive load.
///  2. Every band height is rendered as a non-zero CSS height — even under pt-BR
///     (regression for the bug where pt-BR formatted "154,35px" as invalid CSS).
///  3. Round-trip (load → save → reload) preserves the count of bands and elements.
/// </summary>
public class RealReportLoadFidelityTests : Bunit.BunitContext
{
    public RealReportLoadFidelityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static readonly string SamplesRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "Reporting.Samples.CodeFirst", "out"));

    public static IEnumerable<object[]> RealSamples()
    {
        yield return ["01-vendas-por-cliente.repx"];
        yield return ["02-espelho-produtos.repx"];
        yield return ["03-relatorio-caixa.repx"];
        yield return ["04-cupom-nfce.repx"];
    }

    [Theory]
    [MemberData(nameof(RealSamples))]
    public void Loading_a_real_repx_preserves_bands_and_elements(string fileName)
    {
        var path = Path.Combine(SamplesRoot, fileName);
        if (!File.Exists(path))
        {
            // Samples are only produced when Reporting.Samples.CodeFirst has run at least once.
            // Skip in CI environments where samples haven't been generated yet.
            return;
        }
        var bytes = File.ReadAllBytes(path);

        var state = new DesignerState();
        state.Load(bytes);

        state.Report.Bands.Should().NotBeEmpty($"o {fileName} deve carregar pelo menos uma banda");
        state.Report.Bands.Sum(b => b.Elements.Count).Should().BeGreaterThan(0,
            $"o {fileName} deve ter pelo menos um elemento");
    }

    [Theory]
    [MemberData(nameof(RealSamples))]
    public void Real_repx_round_trips_without_losing_structure(string fileName)
    {
        var path = Path.Combine(SamplesRoot, fileName);
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);

        var state = new DesignerState();
        state.Load(bytes);
        var originalBandCount    = state.Report.Bands.Count;
        var originalElementCount = state.Report.Bands.Sum(b => b.Elements.Count);

        var saved = state.Save();

        var reloaded = new DesignerState();
        reloaded.Load(saved);
        reloaded.Report.Bands.Count.Should().Be(originalBandCount,
            $"abrir → salvar → reabrir não deveria mudar o número de bandas em {fileName}");
        reloaded.Report.Bands.Sum(b => b.Elements.Count).Should().Be(originalElementCount,
            $"abrir → salvar → reabrir não deveria perder elementos em {fileName}");
    }

    [Theory]
    [MemberData(nameof(RealSamples))]
    public void Bands_render_with_non_zero_height_under_pt_BR(string fileName)
    {
        // Regression: the BandCanvas formatted band height with the current culture.
        // In pt-BR, "154.35px" became "154,35px" → invalid CSS → all bands collapsed to
        // height 0 and elements stacked on top of each other (visible in the MAUI thermal cupom).
        var path = Path.Combine(SamplesRoot, fileName);
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

            var state = new DesignerState();
            state.Load(bytes);

            var cut = Render<ReportDesigner>(p => p.Add(d => d.InitialState, state));

            // Every .band must have an inline style with a height that uses dot decimals.
            var bandDivs = cut.FindAll(".band[data-band-kind]");
            bandDivs.Should().NotBeEmpty();
            foreach (var div in bandDivs)
            {
                var style = div.GetAttribute("style") ?? string.Empty;
                style.Should().Contain("height:", "every band needs an explicit pixel height");
                style.Should().NotContain(",", "the height value must use invariant culture (dot decimal), not pt-BR comma");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Long_content_is_clipped_within_element_bounds()
    {
        // Industry standard (Crystal/SSRS/FastReport/Stimulsoft): the designer is WYSIWYG
        // on geometry — text that would exceed the declared bounds is clipped in design view.
        // Our implementation: every .el has an .el-content child carrying overflow:hidden via
        // the scoped designer stylesheet. We verify that wrapper exists.
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.TextBox, "overflow-test")
        {
            Expression = "Tributos totais aproximados conforme a Lei 12.741/2012 — texto muito longo para a caixa",
            X = Reporting.Geometry.Unit.FromMm(5),
            Y = Reporting.Geometry.Unit.FromMm(1),
            Width = Reporting.Geometry.Unit.FromMm(20),  // small box
            Height = Reporting.Geometry.Unit.FromMm(5),
        });

        var cut = Render<ReportDesigner>(p => p.Add(d => d.InitialState, state));

        // Every text-bearing element wraps its content in an .el-content div.
        var contentWrappers = cut.FindAll(".el .el-content");
        contentWrappers.Should().NotBeEmpty("every element must wrap its body in .el-content for clipping");

        // The element itself should carry a title with the full text — so users can read
        // clipped content via tooltip (same UX as SSRS / Crystal).
        var el = cut.Find("[data-element-id='overflow-test']");
        el.GetAttribute("title").Should().Contain("Tributos totais",
            "a valid expression's full text is exposed via the title so clipped content stays readable");
    }

    [Fact]
    public void Invalid_expression_surfaces_an_error_in_the_element_title()
    {
        // The canvas prioritises a validation message over the overflow title when an
        // expression references an unknown field — surfaced as the `has-expr-error` class plus
        // an explanatory tooltip, so the user sees the problem without entering Preview.
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        detail.AddElement(new ElementViewModel(DesignerElementKind.TextBox, "bad-expr")
        {
            Expression = "{Sum(Fields.CampoInexistente)}",
            X = Reporting.Geometry.Unit.FromMm(5),
            Y = Reporting.Geometry.Unit.FromMm(1),
            Width = Reporting.Geometry.Unit.FromMm(40),
            Height = Reporting.Geometry.Unit.FromMm(6),
        });

        var cut = Render<ReportDesigner>(p => p.Add(d => d.InitialState, state));

        var el = cut.Find("[data-element-id='bad-expr']");
        el.GetAttribute("title").Should().Contain("Campo desconhecido");
        el.GetAttribute("class").Should().Contain("has-expr-error");
    }

    [Fact]
    public void Page_width_adapts_to_thermal80_paper()
    {
        var path = Path.Combine(SamplesRoot, "04-cupom-nfce.repx");
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);

        var state = new DesignerState();
        state.Load(bytes);

        // Thermal80 paper is 80mm wide.
        state.Report.PageSetup.PageWidth.ToMm().Should().BeApproximately(80, 0.5);

        var cut = Render<ReportDesigner>(p => p.Add(d => d.InitialState, state));
        var page = cut.Find(".page");
        var style = page.GetAttribute("style") ?? string.Empty;
        // 80mm * (720/210) ≈ 274.29px — must NOT be the default 720px (A4) anymore.
        style.Should().Contain("width:", "page must carry an inline width matching PageSetup");
        // Sanity: should be way below A4's 720px.
        style.Should().NotContain("720");
    }

    [Fact]
    public void Preview_page_width_matches_thermal_paper_not_A4()
    {
        // Industry standard (Crystal/SSRS/FastReport/Stimulsoft): preview shows the paper
        // rectangle at its physical size — a thermal 80mm receipt is a narrow strip in the
        // viewer, not a wide A4 sheet with the receipt floating inside.
        var path = Path.Combine(SamplesRoot, "04-cupom-nfce.repx");
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);

        // We render PreviewMode directly with a stub PNG to keep the test deterministic
        // without going through the paginator.
        IReadOnlyList<byte[]> pages = new List<byte[]>
        {
            new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // PNG magic — content doesn't matter, only width does
        };
        var cut = Render<Reporting.Designer.Blazor.Components.PreviewMode>(p => p
            .Add(c => c.Pages, pages)
            .Add(c => c.CurrentPage, 0)
            .Add(c => c.PaperWidthMm, 80.0));

        var preview = cut.Find(".preview-page");
        var style = preview.GetAttribute("style") ?? string.Empty;
        // 80mm * 720/210 ≈ 274.29 — must be present, NOT the A4 720.
        style.Should().Contain("width:");
        style.Should().Contain("274"); // narrow thermal width
        style.Should().NotContain("720"); // not A4-wide
    }
}
