using System.Text;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Output.Html;
using Xunit;

namespace Reporting.Output.Tests;

/// <summary>
/// Interactivity rendering: an element's <c>Action</c> (hyperlink) and <c>Bookmark</c> propagate
/// onto its primitives and the HTML exporter emits a clickable overlay link + an anchor target —
/// proving these RDL features render, not just round-trip.
/// </summary>
public class InteractivityTests
{
    [Fact]
    public async Task Html_emits_clickable_link_and_bookmark_anchor()
    {
        var report = ReportBuilder.Create("Interativo")
            .Page(p => p.A4().Portrait().Margins(10))
            .ReportHeader(h => h.Height(30)
                .Label("Anthropic").At(0, 0).Size(50, 8).Hyperlink("https://anthropic.com")
                .Label("Início").At(0, 12).Size(50, 8).Bookmark("inicio"))
            .Build();

        var rendered = await report.PaginateAsync();
        using var ms = new MemoryStream();
        new SvgHtmlExporter().Export(rendered, ms);
        var html = new UTF8Encoding(false).GetString(ms.ToArray());

        html.Should().Contain("href=\"https://anthropic.com\"", "the hyperlink action becomes a clickable overlay");
        html.Should().Contain("id=\"bm-inicio\"", "the bookmark becomes an anchor target");
        html.Should().Contain("class=\"lnk\"");
    }
}
