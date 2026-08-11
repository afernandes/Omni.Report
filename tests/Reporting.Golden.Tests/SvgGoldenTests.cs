using System.Text;
using Reporting.CodeFirst;
using Reporting.Output.Svg;
using VerifyXunit;
using Xunit;

namespace Reporting.Golden.Tests;

/// <summary>
/// Pins the vector SVG a report exports to — the emission contract, one layer below
/// <see cref="LayoutGoldenTests"/>.
/// </summary>
/// <remarks>
/// <para>The display-list golden proves the paginator placed things correctly; it says nothing about
/// whether the backend then <em>drew</em> them. A fill that never reaches the canvas, a stroke width
/// collapsed to zero or a gradient flattened to its start colour all leave the display list intact.
/// SVG is the backend worth pinning because it is text: a reviewer reads the diff.</para>
///
/// <para><b>Why this is safe cross-platform.</b> The geometry comes from the deterministic display
/// list, and the suite only asserts on the <em>structure</em> the exporter emits — see
/// <see cref="SvgShape"/>. Glyph outlines and font metrics, which genuinely differ between Windows
/// and Linux, are deliberately outside the assertion.</para>
/// </remarks>
public class SvgGoldenTests
{
    private static async Task VerifySvg(Report report, string name)
    {
        var rendered = await report.PaginateAsync();
        using var ms = new MemoryStream();
        new SvgExporter().Export(rendered, ms);
        var svg = Encoding.UTF8.GetString(ms.ToArray());
        await Verifier.Verify(SvgShape.Summarize(svg), "txt").UseFileName(name + ".svg");
    }

    [Fact]
    public Task Bandas() => VerifySvg(GoldenReports.Bandas(), "Bandas");

    [Fact]
    public Task Formas() => VerifySvg(GoldenReports.Formas(), "Formas");
}
