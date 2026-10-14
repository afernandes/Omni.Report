using System.Collections;
using System.Reflection;
using FluentAssertions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.Layout.Tests;

public sealed class PropertyPathCacheTests
{
    [Fact]
    public async Task PaginateAsync_MilharesDeCaminhosInvalidos_LimitaCacheEPreservaBindingValido()
    {
        var bindings = Enumerable.Range(0, 4_000).ToDictionary(index => "Invalid" + index, _ => "1");
        bindings.Add("Bounds.Width", "10");
        var definition = TestData.GroupedReport() with
        {
            Groups = [],
            Detail = new DetailBand(6.Mm(), EquatableArray.Create<ReportElement>(new LabelElement
            {
                Text = "Teste",
                Bounds = new(0.Mm(), 0.Mm(), 50.Mm(), 6.Mm()),
                PropertyExpressions = new(bindings)
            }))
        };
        var report = await new ReportPaginator().PaginateAsync(TestData.BuildRequest(definition, TestData.ThreeRows()));
        report.Pages.SelectMany(page => page.Primitives).OfType<DrawTextPrimitive>()
            .Should().OnlyContain(text => text.Bounds.Width == 10.Mm());
        var binder = typeof(ReportPaginator).Assembly.GetType("Reporting.Layout.Internal.PropertyPathBinder")!;
        var cache = binder.GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var entries = (IDictionary)cache.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
        entries.Count.Should().BeLessThanOrEqualTo(1024);
    }
}
