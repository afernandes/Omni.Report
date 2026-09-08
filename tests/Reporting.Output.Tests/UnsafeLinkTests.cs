using System.Text;
using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Elements;
using Reporting.Output.Html;
using Xunit;
namespace Reporting.Output.Tests;

public sealed class UnsafeLinkTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("data:text/html,teste")]
    [InlineData("file:///c:/segredo")]
    [InlineData("//externo.test/arquivo")]
    public async Task Export_LinkAtivoNaoPermitido_NaoEmiteAncora(string link)
    {
        var report = ReportBuilder.Create("Link").ReportHeader(b => b.Height(10).Label("Abrir").At(0, 0).Size(50, 8).Hyperlink(link)).Build();
        var rendered = await report.PaginateAsync();
        using var stream = new MemoryStream();
        new SvgHtmlExporter().Export(rendered, stream);
        Encoding.UTF8.GetString(stream.ToArray()).Should().NotContain("class=\"lnk\"");
        ReportLinkPolicy.IsAllowed(link).Should().BeFalse();
    }
    [Theory]
    [InlineData("https://example.test/path")]
    [InlineData("mailto:relatorios@example.test")]
    [InlineData("#bm-inicio")]
    [InlineData("/reports/1")]
    public void IsAllowed_LinkNavegavelPermitido_PreservaNavegacao(string link) => ReportLinkPolicy.IsAllowed(link).Should().BeTrue();
}
