using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.Services;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Paper;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

public sealed class PreviewPageCacheTests
{
    [Fact]
    public async Task Preview_CemPaginasEmCincoSessoes_RetémNoMaximoTresImagensPorSessao()
    {
        var setup = new PageSetup(new PaperSize("Teste", 12.Mm(), 12.Mm()));
        var report = new RenderedReport("Teste", new EquatableArray<RenderedPage>(
            Enumerable.Range(1, 100).Select(page => new RenderedPage(page, setup, []))));
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            var cache = new PreviewPageCache(report);
            cache.Count.Should().Be(100);
            cache.CachedPageCount.Should().Be(0);
            for (int page = 0; page < cache.Count; page++)
            {
                cache[page].Should().StartWith([137, 80, 78, 71]);
                cache.CachedPageCount.Should().BeLessThanOrEqualTo(3);
            }
            var image = cache[99];
            cache[99].Should().BeSameAs(image);
            cache[0].Should().NotBeEmpty();
            cache.CachedPageCount.Should().Be(3);
        })));
    }
}
