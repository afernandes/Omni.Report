using System.Buffers.Binary;
using FluentAssertions;
using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Image;
using Reporting.Output.Pdf;
using Reporting.Paper;
using SkiaSharp;
using Xunit;

namespace Reporting.Output.Tests;

public sealed class ImageRasterBudgetTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void ExportPages_RelatorioMultipagina_GravaPngsValidosEDescartaCadaDestino(int pages)
    {
        var outputs = new List<ExportTestStream>();
        new PngImageExporter().ExportPages(Report(pages), index =>
        {
            index.Should().Be(outputs.Count + 1);
            outputs.All(s => s.Disposed).Should().BeTrue();
            var output = new ExportTestStream();
            outputs.Add(output);
            return output;
        });
        outputs.Should().HaveCount(pages).And.OnlyContain(s => s.Disposed);
        foreach (var output in outputs)
        {
            using var bitmap = SKBitmap.Decode(output.Buffer.ToArray());
            bitmap.Width.Should().Be(96);
            bitmap.Height.Should().Be(96);
            bitmap.GetPixel(0, 0).Should().Be(SKColors.White);
        }
    }

    [Fact]
    public void Export_PngVerticalA4CemPaginas_RejeitaAntesDeEscrever()
    {
        var report = new RenderedReport("Grande", new EquatableArray<RenderedPage>(
            Enumerable.Range(1, 100).Select(i => new RenderedPage(i, PageSetup.A4Portrait, EquatableArray<LayoutPrimitive>.Empty))));
        using var output = new MemoryStream();
        Action act = () => new PngImageExporter().Export(report, output);
        act.Should().Throw<InvalidOperationException>().WithMessage("*ExportPages*");
        output.Length.Should().Be(0);
    }

    [Fact]
    public void Export_LimiteExplicito_PreservaDimensoesDoPngVertical()
    {
        var report = Report(2);
        using var output = new MemoryStream();
        Action rejected = () => new PngImageExporter(new ImageRasterizationOptions { MaxRasterPixels = 96 * 199 }).Export(report, output);
        rejected.Should().Throw<InvalidOperationException>();
        output.Length.Should().Be(0);
        new PngImageExporter(new ImageRasterizationOptions { MaxRasterPixels = 96 * 200 }).Export(report, output);
        using var bitmap = SKBitmap.Decode(output.ToArray());
        bitmap.Width.Should().Be(96);
        bitmap.Height.Should().Be(200);
        output.CanWrite.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_DimensoesOuGapComOverflow_RejeitaSemAlocar(bool tiff)
    {
        using var output = new MemoryStream();
        Action act = tiff
            ? () => new TiffImageExporter(dpi: float.MaxValue).Export(Report(1), output)
            : () => new PngImageExporter(pageGapPx: int.MaxValue).Export(Report(3), output);
        if (tiff) act.Should().Throw<ArgumentOutOfRangeException>();
        else act.Should().Throw<InvalidOperationException>();
        output.Length.Should().Be(0);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_DpiNaoFinito_Rejeita(float dpi)
    {
        Action png = () => new PngImageExporter(dpi);
        Action tiff = () => new TiffImageExporter(dpi);
        png.Should().Throw<ArgumentOutOfRangeException>();
        tiff.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExportPages_FalhaNoDestino_DescartaStreamEPermiteNovaExportacao()
    {
        var exporter = new PngImageExporter();
        var broken = new ExportTestStream { FailWrites = true };
        Action act = () => exporter.ExportPages(Report(2), _ => broken);
        act.Should().Throw<IOException>();
        broken.Disposed.Should().BeTrue();
        exporter.RenderPages(Report(1)).Should().HaveCount(1);
    }

    [Fact]
    public void ExportPages_CancelamentoDepoisDeAbrirDestino_DescartaSemEscrever()
    {
        using var cancellation = new CancellationTokenSource();
        var output = new ExportTestStream();
        Action act = () => new PngImageExporter().ExportPages(Report(2), _ =>
        {
            cancellation.Cancel();
            return output;
        }, cancellation.Token);
        act.Should().Throw<OperationCanceledException>();
        output.Disposed.Should().BeTrue();
        output.Buffer.ToArray().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportAsync_TokenCancelado_NaoEscreveNemFechaDestino(bool tiff)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new ExportTestStream();
        IReportExporter exporter = tiff ? new TiffImageExporter() : new PngImageExporter();
        Func<Task> act = () => exporter.ExportAsync(Report(2), output, cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        output.Buffer.Length.Should().Be(0);
        output.Disposed.Should().BeFalse();
    }

    [Fact]
    public void Export_TiffNaoSeekable_AlinhaDiretoriosEPreservaPixels()
    {
        using var output = new ExportTestStream();
        new TiffImageExporter(dpi: 3).Export(Report(3), output);
        output.Disposed.Should().BeFalse();
        var bytes = output.Buffer.ToArray();
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        int pages = 0;
        while (offset != 0)
        {
            (offset % 2).Should().Be(0);
            var directory = bytes.AsSpan((int)offset);
            BinaryPrimitives.ReadUInt16LittleEndian(directory).Should().Be(9);
            var strip = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(2 + 5 * 12 + 8));
            bytes.Skip((int)strip).Take(27).Should().OnlyContain(b => b == 255);
            offset = BinaryPrimitives.ReadUInt32LittleEndian(directory.Slice(110));
            pages++;
        }
        pages.Should().Be(3);
    }

    [Fact]
    public async Task ExportAsync_TiffCanceladoDuranteEscrita_InterrompeSemFecharDestino()
    {
        using var cancellation = new CancellationTokenSource();
        using var output = new ExportTestStream { OnWrite = cancellation.Cancel };
        Func<Task> act = () => new TiffImageExporter().ExportAsync(Report(2), output, cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        output.Disposed.Should().BeFalse();
        output.Buffer.Length.Should().BeLessThan(96 * 96 * 3);
    }

    [Fact]
    public void Export_TiffMaiorQueQuatroGiB_RejeitaAntesDoCabecalho()
    {
        var report = Report(1500, 1000);
        using var output = new MemoryStream();
        Action act = () => new TiffImageExporter(dpi: 1000).Export(report, output);
        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bits*");
        output.Length.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_RelatorioVazio_ProduzImagemBrancaDeUmPixel(bool tiff)
    {
        IReportExporter exporter = tiff ? new TiffImageExporter() : new PngImageExporter();
        var bytes = exporter.ExportToBytes(Report(0));
        if (tiff)
        {
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(18)).Should().Be(1);
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(30)).Should().Be(1);
            bytes.Skip(128).Take(3).Should().Equal(255, 255, 255);
        }
        else
        {
            using var bitmap = SKBitmap.Decode(bytes);
            bitmap.Width.Should().Be(1);
            bitmap.Height.Should().Be(1);
            bitmap.GetPixel(0, 0).Should().Be(SKColors.White);
        }
    }

    [Fact]
    public void Export_TiffLarguraMaiorQueUshort_PreservaDimensaoEmTagLong()
    {
        var page = new RenderedPage(1, new PageSetup(new PaperSize("Largo", new Unit(70000), new Unit(1)),
            Margins: Thickness.Uniform(Unit.Zero)), EquatableArray<LayoutPrimitive>.Empty);
        var bytes = new TiffImageExporter(dpi: 1000).ExportToBytes(new RenderedReport("Largo", EquatableArray.Create(page)));
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12)).Should().Be(4);
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(18)).Should().Be(70000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_PapelContinuoComOverflowDeUnit_RejeitaDimensaoReal(bool tiff)
    {
        var page = new RenderedPage(1, new PageSetup(new PaperSize("Rolo", new Unit(1000), Unit.Zero),
            Margins: Thickness.Uniform(Unit.Zero)), EquatableArray.Create<LayoutPrimitive>(new DrawTextPrimitive
            {
                Text = "Conteúdo",
                Style = Reporting.Rendering.TextStyle.Default,
                Bounds = new Rectangle(Unit.Zero, new Unit(int.MaxValue), new Unit(1000), new Unit(int.MaxValue)),
            }));
        var report = new RenderedReport("Rolo", EquatableArray.Create(page));
        using var output = new MemoryStream();
        IReportExporter exporter = tiff ? new TiffImageExporter() : new PngImageExporter();
        Action act = () => exporter.Export(report, output);
        act.Should().Throw<InvalidOperationException>();
        output.Length.Should().Be(0);
    }

    private static RenderedReport Report(int pages, int mils = 1000) => new("Imagem",
        new EquatableArray<RenderedPage>(Enumerable.Range(1, pages).Select(i =>
            new RenderedPage(i, new PageSetup(new PaperSize("Quadrado", new Unit(mils), new Unit(mils)),
                Margins: Thickness.Uniform(Unit.Zero)), EquatableArray<LayoutPrimitive>.Empty))));
}
