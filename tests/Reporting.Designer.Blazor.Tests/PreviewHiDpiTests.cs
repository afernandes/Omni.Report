using System.Buffers.Binary;
using Bunit;
using FluentAssertions;
using Reporting.Common;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Designer.Blazor.Services;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Paper;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The preview is a raster; at 96 DPI it blurs on a 2x screen and when zoomed. These pin that it is rasterized at
/// 96 × device pixel ratio × zoom, within the pixel budget, and re-rasterized when the zoom changes.
/// </summary>
public sealed class PreviewHiDpiTests : BunitContext
{
    private static RenderedReport A4(int pages = 1) => new("Teste", new EquatableArray<RenderedPage>(
        Enumerable.Range(1, pages).Select(page => new RenderedPage(page, PageSetup.A4Portrait, []))));

    [Fact]
    public void Cache_EscalaDois_RasterizaComODobroDePixels()
    {
        var cache = new PreviewPageCache(A4());
        var normal = WidthOf(cache[0]);

        cache.SetScale(2).Should().BeTrue();

        WidthOf(cache[0]).Should().BeInRange(normal * 2 - 2, normal * 2 + 2);
    }

    [Fact]
    public void Cache_MudarAEscala_DescartaAsImagensJaGeradas_MasNaoParaAMesmaEscala()
    {
        var cache = new PreviewPageCache(A4(3));
        _ = cache[0];
        _ = cache[1];

        cache.SetScale(1.1).Should().BeTrue("1.1 snaps to the next quarter, 1.25");
        cache.CachedPageCount.Should().Be(0);
        _ = cache[0];
        cache.SetScale(1.2).Should().BeFalse("1.2 also snaps to 1.25: a small zoom step reuses the images");
        cache.CachedPageCount.Should().Be(1);
    }

    [Fact]
    public void Cache_PaginaGrandeEmEscalaAlta_ReduzAEscalaEmVezDeFalhar()
    {
        var huge = new PageSetup(new PaperSize("Grande", 900.Mm(), 900.Mm()));
        var cache = new PreviewPageCache(new RenderedReport("Grande", new EquatableArray<RenderedPage>(
            [new RenderedPage(1, huge, [])])));
        cache.SetScale(4);

        var png = cache[0];

        ((long)WidthOf(png) * HeightOf(png)).Should().BeLessThanOrEqualTo(16_000_000);
    }

    [Theory]
    [InlineData(254, 508)]   // 10 × 20 in: at 4× the square-root estimate gives 2829 × 5657 = 16,003,653 px
    [InlineData(216, 356)]   // Legal
    public void Cache_PaginaRetangularEmEscalaAlta_FicaDentroDoOrcamento(int widthMm, int heightMm)
    {
        var setup = new PageSetup(new PaperSize("Retangular", widthMm.Mm(), heightMm.Mm()));
        var cache = new PreviewPageCache(new RenderedReport("R", new EquatableArray<RenderedPage>(
            [new RenderedPage(1, setup, [])])));
        cache.SetScale(4);

        var png = cache[0];

        ((long)WidthOf(png) * HeightOf(png)).Should().BeLessThanOrEqualTo(16_000_000);
    }

    [Fact]
    public async Task Preview_TelaComRazaoDois_RasterizaPelaRazao()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<double>("omniDesigner.devicePixelRatio").SetResult(2);
        var state = new DesignerState();
        state.Parameters.Clear();
        state.DataSources.Clear();
        state.DataSources.Add(new DesignerDataSource("Dados", []));
        state.PreviewDataRegistry = new DataSourceRegistry();
        state.PreviewDataRegistry.Register(new EnumerableDataSource<string>("Dados", ["A"]));
        var designer = Render<ReportDesigner>(p => p.Add(x => x.InitialState, state));

        await designer.InvokeAsync(() => state.CommandPaletteOpen = true);
        await designer.Find(".cmdp-item[data-command-id='view.preview']").ClickAsync(new());

        designer.WaitForAssertion(() =>
        {
            var src = designer.Find(".preview-page img").GetAttribute("src")!;
            var png = Convert.FromBase64String(src["data:image/png;base64,".Length..]);
            WidthOf(png).Should().BeGreaterThan(1500, "an A4 page is 794 px wide at 96 DPI; a 2x screen needs ~1588");
        }, TimeSpan.FromSeconds(10));
    }

    // PNG IHDR: width and height are big-endian at bytes 16 and 20.
    private static int WidthOf(byte[] png) => BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
    private static int HeightOf(byte[] png) => BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
}
