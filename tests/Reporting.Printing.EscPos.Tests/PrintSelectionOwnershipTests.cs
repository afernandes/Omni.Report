using FluentAssertions;
using Reporting.Common;
using Reporting.Layout;
using Reporting.Tests;
using Xunit;
namespace Reporting.Printing.EscPos.Tests;

public sealed class PrintSelectionOwnershipTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Enumerate_IntervaloComDuasCopias_RespeitaAgrupamento(bool collate)
    {
        var indices = PrintPageSelection.Enumerate(4, new PrintOptions("esc-pos") { PageRange = (2, 3), Copies = 2, Collate = collate });
        indices.Should().Equal(collate ? [1, 2, 1, 2] : [1, 1, 2, 2]);
    }
    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 2)]
    [InlineData(1, 5)]
    public void Enumerate_IntervaloInvalido_RejeitaAntesDoTransporte(int from, int to)
    {
        var action = () => PrintPageSelection.Enumerate(4, new PrintOptions("esc-pos") { PageRange = (from, to) });
        action.Should().Throw<ArgumentOutOfRangeException>();
    }
    [Fact]
    public async Task PrintAsync_DoisJobsNoTransporteEmprestado_MantemTransporteAberto()
    {
        using var stream = new MemoryStream();
        await using var transport = new StreamEscPosTransport(stream);
        var printer = new EscPosPrinter(transport, new EscPosPrinterOptions { ForcedDotWidth = 208 });
        var page = PrintingReplayFixture.Page(PrintingReplayFixture.Primitive(0));
        var report = new RenderedReport("Dois jobs", EquatableArray.Create(page, page, page));
        var options = new PrintOptions("esc-pos") { PageRange = (2, 2), Copies = 2 };
        var first = await printer.PrintAsync(report, options);
        var second = await printer.PrintAsync(report, options);
        first.Succeeded.Should().BeTrue(); second.Succeeded.Should().BeTrue();
        first.PagesPrinted.Should().Be(2); second.PagesPrinted.Should().Be(2);
        stream.CanWrite.Should().BeTrue();
    }
    [Fact]
    public async Task PrintAsync_TransporteCriadoPelaFactory_DescartaNoFimDoJob()
    {
        var stream = new MemoryStream();
        var printer = new EscPosPrinter(_ => Task.FromResult<IEscPosTransport>(new StreamEscPosTransport(stream)));
        var result = await printer.PrintAsync(new RenderedReport("Vazio", EquatableArray<RenderedPage>.Empty), new PrintOptions("esc-pos"));
        result.Succeeded.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
    }
}
