using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Styling;
using Xunit;

namespace Reporting.Tests;

public abstract class ContinuousCanvasTests
{
    protected abstract IRenderingContext Create(float dpi, ContinuousPageOptions options);
    protected abstract (int Width, int Height) Size(IRenderingContext context, int index = 0);
    protected abstract bool IsBlack(IRenderingContext context, int x, int y);

    [Fact]
    public void EndPage_ImagemGravada_PreservaPixelsAposLiberarBufferDoChamador()
    {
        using var context = Create(96, new());
        // An 8x8 opaque black PNG keeps the sample away from bicubic transparent edge pixels.
        var data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAABHNCSVQICAgIfAhkiAAAABVJREFUGJVjZGBg+M+ABzDhkxw+CgAMUwEPjlgNSAAAAABJRU5ErkJggg==");
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawImage(data, new Rectangle(10.Mm(), 100.Mm(), 20.Mm(), 10.Mm()), Reporting.Elements.ImageSizing.Stretch);
        Array.Clear(data);
        context.EndPage();
        Assert.InRange(Size(context).Height, (int)110.Mm().ToPixels(), (int)110.Mm().ToPixels() + 3);
        Assert.True(IsBlack(context, (int)15.Mm().ToPixels(), (int)105.Mm().ToPixels()));
    }

    [Fact]
    public void DrawPath_CallbackFalha_NaoGravaCaminhoParcial()
    {
        using var context = Create(96, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        Assert.Throws<InvalidOperationException>(() => context.DrawPath(path =>
        {
            path.MoveTo(new Point(10.Mm(), 150.Mm())).LineTo(new Point(20.Mm(), 160.Mm()));
            throw new InvalidOperationException("callback");
        }, PenStyle.Default, null));
        context.EndPage();
        Assert.Equal(1, Size(context).Height);
    }

    [Fact]
    public void EndPage_CurvaComTraco_ConsideraExtremosDoCaminho()
    {
        using var context = Create(96, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawPath(path => path.MoveTo(new Point(10.Mm(), 100.Mm()))
            .CubicTo(new Point(10.Mm(), 120.Mm()), new Point(40.Mm(), 120.Mm()), new Point(40.Mm(), 100.Mm())),
            new PenStyle(Color.Black, 4.Mm()), null);
        context.EndPage();
        Assert.InRange(Size(context).Height, (int)117.Mm().ToPixels(), (int)125.Mm().ToPixels());
        Assert.True(IsBlack(context, (int)25.Mm().ToPixels(), (int)115.Mm().ToPixels()));
    }

    [Fact]
    public void EndPage_PrimitivasTransparentes_NaoAumentaAltura()
    {
        using var context = Create(96, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        var pen = new PenStyle(Color.Transparent, 2.Mm());
        context.DrawRectangle(new Rectangle(10.Mm(), 100.Mm(), 10.Mm(), 10.Mm()), pen, BrushStyle.Transparent);
        context.DrawPath(path => path.MoveTo(new Point(10.Mm(), 150.Mm())).LineTo(new Point(20.Mm(), 160.Mm())), pen, null);
        context.DrawText("invisible", new Rectangle(10.Mm(), 180.Mm(), 50.Mm(), 10.Mm()), TextStyle.Default.WithColor(Color.Transparent));
        context.EndPage();
        Assert.Equal(1, Size(context).Height);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(203)]
    public void EndPage_Thermal80Conteudo110mm_PreservaConteudoEAltura(float dpi)
    {
        using var context = Create(dpi, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal80, Margins: new Thickness(0.Mm(), 3.Mm(), 0.Mm(), 5.Mm())));
        context.DrawRectangle(new Rectangle(10.Mm(), 100.Mm(), 20.Mm(), 10.Mm()), null, BrushStyle.Black);
        context.EndPage();
        var size = Size(context);
        Assert.Equal((int)Math.Ceiling(PaperSize.Thermal80.Width.ToPixels(dpi)), size.Width);
        Assert.InRange(size.Height, (int)115.Mm().ToPixels(dpi), (int)115.Mm().ToPixels(dpi) + 3);
        Assert.True(IsBlack(context, (int)15.Mm().ToPixels(dpi), (int)105.Mm().ToPixels(dpi)));
        Assert.False(IsBlack(context, (int)15.Mm().ToPixels(dpi), size.Height - 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndPage_RecortesAninhadosERestauracao_ConsideraSomenteAreaVisivel(bool rounded)
    {
        using var context = Create(96, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.PushClip(new Rectangle(10.Mm(), 90.Mm(), 20.Mm(), 20.Mm()), rounded ? 8.Mm() : Unit.Zero);
        context.PushClip(new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 105.Mm()), Unit.Zero);
        context.DrawRectangle(new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 300.Mm()), null, BrushStyle.Black);
        context.PopClip();
        context.PopClip();
        context.DrawRectangle(new Rectangle(40.Mm(), 115.Mm(), 10.Mm(), 5.Mm()), null, BrushStyle.Black);
        context.EndPage();
        Assert.InRange(Size(context).Height, (int)120.Mm().ToPixels(), (int)120.Mm().ToPixels() + 3);
        Assert.True(IsBlack(context, (int)20.Mm().ToPixels(), (int)100.Mm().ToPixels()));
        Assert.False(IsBlack(context, (int)20.Mm().ToPixels(), (int)108.Mm().ToPixels()));
        Assert.True(IsBlack(context, (int)45.Mm().ToPixels(), (int)117.Mm().ToPixels()));
    }

    [Fact]
    public void EndPage_PaginaVazia_ReservaMargensEUmPixel()
    {
        using var context = Create(96, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal58, Margins: Thickness.Uniform(5.Mm())));
        context.EndPage();
        Assert.Equal((int)Math.Ceiling(2 * 5.Mm().ToPixels() + 1), Size(context).Height);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EndPage_PrimitivasAbaixoDaLarguraDoRolo_PreservaTinta(int primitive)
    {
        using var context = Create(96, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        var bounds = new Rectangle(10.Mm(), 100.Mm(), 30.Mm(), 10.Mm());
        int calls = 0;
        switch (primitive)
        {
            case 0:
                context.DrawLine(new Point(10.Mm(), 105.Mm()), new Point(40.Mm(), 105.Mm()), new PenStyle(Color.Black, 4.Mm()));
                break;
            case 1:
                context.DrawEllipse(bounds, null, BrushStyle.Black);
                break;
            case 2:
                context.DrawPath(path =>
                {
                    calls++;
                    path.MoveTo(new Point(10.Mm(), 100.Mm())).LineTo(new Point(40.Mm(), 100.Mm()))
                        .LineTo(new Point(25.Mm(), 110.Mm())).Close();
                }, null, BrushStyle.Black);
                break;
            default:
                context.DrawText("MMMMMMMM", bounds, new TextStyle(new Font("Arial", 18), Color.Black));
                break;
        }
        context.EndPage();
        Assert.InRange(Size(context).Height, (int)103.Mm().ToPixels(), (int)115.Mm().ToPixels());
        bool ink = false;
        for (int y = (int)100.Mm().ToPixels(); y < Size(context).Height && !ink; y += 2)
        {
            for (int x = (int)10.Mm().ToPixels(); x < (int)40.Mm().ToPixels() && !ink; x += 2)
                ink = IsBlack(context, x, y);
        }
        Assert.True(ink);
        Assert.Equal(primitive == 2 ? 1 : 0, calls);
    }

    [Fact]
    public void EndPage_DesenhoForaDaLarguraOuRecorte_NaoAumentaAltura()
    {
        using var context = Create(96, new() { MaxHeight = 200.Mm() });
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawRectangle(new Rectangle(90.Mm(), 900.Mm(), 10.Mm(), 10.Mm()), null, BrushStyle.Black);
        context.PushClip(new Rectangle(0.Mm(), 0.Mm(), 80.Mm(), 30.Mm()), Unit.Zero);
        context.DrawRectangle(new Rectangle(0.Mm(), 800.Mm(), 10.Mm(), 10.Mm()), null, BrushStyle.Black);
        context.PopClip();
        context.EndPage();
        Assert.Equal(1, Size(context).Height);
    }

    [Fact]
    public void DrawRectangle_AlturaAcimaDoLimite_RejeitaAntesDeGravar()
    {
        using var context = Create(96, new() { MaxHeight = 100.Mm() });
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        var error = Assert.Throws<InvalidOperationException>(() =>
            context.DrawRectangle(new Rectangle(0.Mm(), 110.Mm(), 10.Mm(), 10.Mm()), null, BrushStyle.Black));
        Assert.Contains("MaxHeight", error.Message);
        context.EndPage();
        Assert.Equal(1, Size(context).Height);
    }

    [Fact]
    public void EndPage_OrcamentoExcedido_LiberaPaginaEPermiteNovaExecucao()
    {
        using var context = Create(96, new() { MaxRasterPixels = 1000 });
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawRectangle(new Rectangle(10.Mm(), 100.Mm(), 10.Mm(), 10.Mm()), null, BrushStyle.Black);
        Assert.Contains("MaxRasterPixels", Assert.Throws<InvalidOperationException>(context.EndPage).Message);
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.EndPage();
        Assert.Equal(1, Size(context).Height);
    }

    [Fact]
    public void DrawRectangle_OperacoesAcimaDoLimite_RejeitaNovaOperacao()
    {
        using var context = Create(96, new() { MaxOperations = 1 });
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawRectangle(new Rectangle(1.Mm(), 1.Mm(), 5.Mm(), 5.Mm()), null, BrushStyle.Black);
        Assert.Contains("MaxOperations", Assert.Throws<InvalidOperationException>(() =>
            context.DrawRectangle(new Rectangle(1.Mm(), 1.Mm(), 5.Mm(), 5.Mm()), null, BrushStyle.Black)).Message);
        context.EndPage();
    }

    [Fact]
    public void BeginPage_PaginasConsecutivas_ReiniciaAlturaERecortes()
    {
        using var context = Create(96, new());
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.DrawRectangle(new Rectangle(10.Mm(), 100.Mm(), 10.Mm(), 10.Mm()), null, BrushStyle.Black);
        context.PushClip(new Rectangle(0.Mm(), 0.Mm(), 1.Mm(), 1.Mm()), Unit.Zero);
        context.BeginPage(new PageSetup(PaperSize.Thermal80));
        context.EndPage();
        Assert.True(Size(context, 0).Height > 300);
        Assert.Equal(1, Size(context, 1).Height);
    }
}
