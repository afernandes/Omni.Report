using Reporting.CodeFirst;
using VerifyXunit;
using Xunit;

namespace Reporting.Golden.Tests;

/// <summary>
/// Pins the paginator's output — the display list — for a catalogue of representative reports.
/// </summary>
/// <remarks>
/// <para>Before this suite the render tests asserted <em>ink pixel counts per region</em>: they prove
/// something was drawn roughly where expected, but a shifted baseline, a lost gradient, a dropped
/// border or an alignment flip all keep the count identical. Nothing pinned the actual layout.</para>
///
/// <para>To regenerate after an intentional change: run the suite, inspect the <c>*.received.txt</c>
/// files that appear next to the goldens, and if the diff is what you meant, replace the
/// <c>*.verified.txt</c> with it. <b>Read the diff</b> — accepting a golden without reading it turns
/// the suite into a rubber stamp.</para>
/// </remarks>
public class LayoutGoldenTests
{
    private static async Task VerifyLayout(Report report, string name)
        => await Verifier.Verify(DisplayList.Format(await report.PaginateAsync()), "txt")
                         .UseFileName(name);

    [Fact]
    public Task Bandas() => VerifyLayout(GoldenReports.Bandas(), "Bandas");

    [Fact]
    public Task Estilos() => VerifyLayout(GoldenReports.Estilos(), "Estilos");

    [Fact]
    public Task Formas() => VerifyLayout(GoldenReports.Formas(), "Formas");

    [Fact]
    public Task Multipagina() => VerifyLayout(GoldenReports.Multipagina(), "Multipagina");
}
