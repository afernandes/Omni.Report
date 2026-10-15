using FluentAssertions;
using Reporting.CodeFirst;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Styling;
using Xunit;

namespace Reporting.Golden.Tests;

/// <summary>
/// Pins the two gaps the golden suite exposed on its first run — <c>Style.Border</c> on text and the corner radius
/// of a leaf rectangle — now that both reach a primitive every backend draws.
/// </summary>
public class ElementBorderAndRadiusTests
{
    [Fact]
    public async Task PaginateAsync_LabelComBordaUniforme_EmiteRetanguloComTracoSobreOTexto()
    {
        var report = ReportBuilder.Create("Borda")
            .Page(p => p.A5().Portrait().Margins(10))
            .ReportHeader(h => h.Height(20)
                .Label("Com borda").At(0, 0).Size(50, 8)
                    .Border(BorderLineStyle.Solid, 1.0, Color.FromHex("#0033AA")))
            .Build();

        var primitives = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();

        var text = primitives.OfType<DrawTextPrimitive>().Should().ContainSingle().Subject;
        var border = primitives.OfType<DrawRectanglePrimitive>().Should().ContainSingle().Subject;
        border.Pen!.Color.Should().Be(Color.FromHex("#0033AA"));
        border.Fill.Should().BeNull("a border is a stroke, not a background");
        border.Bounds.Should().Be(text.Bounds);
        primitives.IndexOf(border).Should().BeGreaterThan(primitives.IndexOf(text), "the border is drawn on top of the content");
    }

    [Fact]
    public async Task PaginateAsync_LabelComBordaSoEmbaixo_EmiteUmaLinhaNaBase()
    {
        var bottomOnly = new Border(BorderSide.None, BorderSide.None, BorderSide.None,
            new BorderSide(BorderLineStyle.Dashed, Unit.FromPoint(1), Color.FromHex("#AA0000")));
        var report = ReportBuilder.Create("BordaInferior")
            .Page(p => p.A5().Portrait().Margins(10))
            .ReportHeader(h => h.Height(20)
                .Label("Sublinhado").At(0, 0).Size(50, 8).Border(bottomOnly))
            .Build();

        var primitives = (await report.PaginateAsync()).Pages.SelectMany(p => p.Primitives).ToList();

        var text = primitives.OfType<DrawTextPrimitive>().Single();
        var line = primitives.OfType<DrawLinePrimitive>().Should().ContainSingle().Subject;
        line.Pen.Color.Should().Be(Color.FromHex("#AA0000"));
        line.Pen.Style.Should().Be(BorderLineStyle.Dashed);
        line.From.Y.Should().Be(text.Bounds.Bottom);
        line.To.Y.Should().Be(text.Bounds.Bottom);
        primitives.OfType<DrawRectanglePrimitive>().Should().BeEmpty("only the bottom side is visible");
    }

    [Fact]
    public async Task PaginateAsync_RetanguloArredondadoSemFilhos_PreservaORaio()
    {
        var report = ReportBuilder.Create("Raio")
            .Page(p => p.A5().Portrait().Margins(10))
            .ReportHeader(h => h.Height(30)
                .Rectangle().At(0, 0).Size(40, 20).CornerRadius(5).Fill(Color.FromHex("#DDDDDD")))
            .Build();

        var rect = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawRectanglePrimitive>()
            .Should().ContainSingle().Subject;

        rect.Fill!.Color.Should().Be(Color.FromHex("#DDDDDD"));
        rect.CornerRadius.Should().Be(Unit.FromMm(5));
    }

    /// <summary>The radius must keep reaching a container's children as a clip — the fix for leaf rectangles must
    /// not trade one behaviour for the other.</summary>
    [Fact]
    public async Task PaginateAsync_RetanguloArredondadoContainer_AplicaORaioNoRecorteDosFilhos()
    {
        var report = ReportBuilder.Create("RaioContainer")
            .Page(p => p.A5().Portrait().Margins(10))
            .ReportHeader(h => h.Height(30)
                .Rectangle(inner => inner.Label("dentro").At(2, 2).Size(30, 5))
                    .At(0, 0).Size(40, 20).CornerRadius(5))
            .Build();

        var child = (await report.PaginateAsync()).Pages
            .SelectMany(p => p.Primitives).OfType<DrawTextPrimitive>()
            .Should().ContainSingle().Subject;

        child.ClipCornerRadius.Should().Be(Unit.FromMm(5));
    }
}
