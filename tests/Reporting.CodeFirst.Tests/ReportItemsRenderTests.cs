using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Layout.Primitives;
using Xunit;

namespace Reporting.CodeFirst.Tests;

/// <summary>
/// End-to-end coverage of RDL <c>ReportItems!Name.Value</c>: a band rendered later (the report footer)
/// reads the value a named text box in an earlier band (the report header) resolved to. This is the
/// classic SSRS pattern (a footer/header echoing a body value).
/// </summary>
public class ReportItemsRenderTests
{
    [Fact]
    public async Task ReportItems_in_footer_echoes_a_named_header_textbox()
    {
        var report = ReportBuilder.Create("RI")
            .Page(p => p.A4().Portrait().Margins(10))
            .DataSource("Vendas", new[] { new VendaRegional("Sul", "Jan", 100m) })
            .ReportHeader(h => h.Height(20)
                .Text("'Capítulo 1'").Name("Titulo").At(0, 0).Size(80, 8))
            .ReportFooter(f => f.Height(20)
                .Text("ReportItems.Titulo").At(0, 0).Size(80, 8))
            .Build();

        var texts = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>().Select(t => t.Text).ToList();

        // Header renders "Capítulo 1"; the footer echoes the same value via ReportItems → it appears twice.
        texts.Should().Contain("Capítulo 1");
        texts.Count(t => t == "Capítulo 1").Should().BeGreaterThanOrEqualTo(2,
            "the footer's ReportItems.Titulo resolves to the header text box's rendered value");
    }
}
