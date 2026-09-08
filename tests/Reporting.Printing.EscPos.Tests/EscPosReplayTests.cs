using FluentAssertions;
using Reporting.Common;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Image;
using Reporting.Tests;
using SkiaSharp;
using Xunit;

namespace Reporting.Printing.EscPos.Tests;

public sealed class EscPosReplayTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void RenderPageToEscPos_PrimitivasConhecidas_CorrespondeAoRasterDeReferencia(int kind)
    {
        var page = PrintingReplayFixture.Page(PrintingReplayFixture.Primitive(kind));
        var bytes = EscPosPrinter.RenderPageToEscPos(page, new EscPosPrinterOptions { ForcedDotWidth = 208 });
        AssertMatchesReference(page, bytes);
        bytes.Skip(8).SkipLast(1).Should().Contain(b => b != 0, "cada primitiva precisa produzir tinta");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderPageToEscPos_ClipRetangularOuArredondado_RecortaERestauraParaProximaPrimitiva(bool rounded)
    {
        var page = PrintingReplayFixture.ClippedPage(rounded);
        var bytes = EscPosPrinter.RenderPageToEscPos(page, new EscPosPrinterOptions { ForcedDotWidth = 208 });
        AssertMatchesReference(page, bytes);
        IsBlack(bytes, 70, 70).Should().BeTrue();
        IsBlack(bytes, 150, 70).Should().BeFalse();
        IsBlack(bytes, 175, 175).Should().BeTrue("o clip anterior não pode afetar a próxima primitiva");
        if (rounded) IsBlack(bytes, 23, 23).Should().BeFalse();
        else IsBlack(bytes, 23, 23).Should().BeTrue();
    }

    [Fact]
    public void Replay_NovaPrimitivaExigeCobertura_CobreTodosOsTiposDoLayout()
    {
        var types = typeof(LayoutPrimitive).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(LayoutPrimitive).IsAssignableFrom(t));
        types.Should().BeEquivalentTo(Enumerable.Range(0, 6).Select(i => PrintingReplayFixture.Primitive(i).GetType()));
    }

    [Fact]
    public async Task PrintAsync_PoligonoComClip_EnviaRasterCorretoAoTransporte()
    {
        var page = PrintingReplayFixture.ClippedPage(true);
        using var buffer = new MemoryStream();
        var printer = new EscPosPrinter(new StreamEscPosTransport(buffer, leaveOpen: true),
            new EscPosPrinterOptions { ForcedDotWidth = 208 });
        var result = await printer.PrintAsync(new RenderedReport("Polígono", EquatableArray.Create(page)), new PrintOptions("esc-pos"));
        result.Succeeded.Should().BeTrue();
        result.PagesPrinted.Should().Be(1);
        var bytes = buffer.ToArray();
        bytes.Take(2).Should().Equal(0x1B, 0x40);
        AssertMatchesReference(page, bytes[2..^3]);
    }

    [Fact]
    public void RenderPageToEscPos_PrimitivaDesconhecida_FalhaExplicitamente()
    {
        var page = PrintingReplayFixture.Page(new UnsupportedPrintPrimitive { Bounds = PrintingReplayFixture.Bounds });
        Action act = () => EscPosPrinter.RenderPageToEscPos(page, new EscPosPrinterOptions { ForcedDotWidth = 208 });
        act.Should().Throw<NotSupportedException>();
    }

    private static bool IsBlack(byte[] bytes, int x, int y)
    {
        int rowBytes = bytes[4] | (bytes[5] << 8);
        return (bytes[8 + y * rowBytes + x / 8] & (0x80 >> (x % 8))) != 0;
    }

    private static void AssertMatchesReference(RenderedPage page, byte[] bytes)
    {
        bytes.Take(4).Should().Equal(0x1D, 0x76, 0x30, 0);
        int height = bytes[6] | (bytes[7] << 8);
        height.Should().Be(203);
        using var reference = SKBitmap.Decode(RegionRasterizer.RenderRegionPng(page.Primitives, PrintingReplayFixture.Bounds, EscPosPrinter.ThermalDpi));
        var mismatches = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < 208; x++)
            {
                var color = x < reference.Width ? reference.GetPixel(x, y) : SKColors.White;
                var expected = (299 * color.Red + 587 * color.Green + 114 * color.Blue) / 1000 < 128;
                if (IsBlack(bytes, x, y) != expected) mismatches++;
            }
        }
        mismatches.Should().Be(0);
    }
}
