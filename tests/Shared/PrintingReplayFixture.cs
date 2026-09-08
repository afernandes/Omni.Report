using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Styling;
using SkiaSharp;

namespace Reporting.Tests;

internal static class PrintingReplayFixture
{
    internal static Rectangle Bounds => new(Unit.Zero, Unit.Zero, new Unit(1000), new Unit(1000));

    internal static LayoutPrimitive Primitive(int kind) => kind switch
    {
        0 => new DrawTextPrimitive { Text = "Teste", Style = TextStyle.Default, Bounds = Bounds },
        1 => new DrawLinePrimitive { From = new Point(new Unit(100), new Unit(100)),
            To = new Point(new Unit(900), new Unit(900)), Pen = new PenStyle(Color.Black, new Unit(20)), Bounds = Bounds },
        2 => new DrawRectanglePrimitive { Fill = new BrushStyle(Color.Black), Bounds = Bounds },
        3 => new DrawEllipsePrimitive { Fill = new BrushStyle(Color.Black), Bounds = Bounds },
        4 => new DrawImagePrimitive { Data = new EquatableArray<byte>(BlackImage()), Bounds = Bounds },
        5 => new DrawPolygonPrimitive
        {
            Points = EquatableArray.Create(new Point(Unit.Zero, Unit.Zero), new Point(new Unit(1000), Unit.Zero),
                new Point(new Unit(1000), new Unit(1000)), new Point(Unit.Zero, new Unit(1000))),
            Fill = new BrushStyle(Color.Black), Bounds = Bounds,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static RenderedPage Page(params LayoutPrimitive[] primitives) => new(1,
        new PageSetup(new PaperSize("Teste", new Unit(1000), new Unit(1000)), Margins: Thickness.Uniform(Unit.Zero)),
        new EquatableArray<LayoutPrimitive>(primitives));

    internal static RenderedPage ClippedPage(bool rounded) => Page(
        Primitive(5) with
        {
            ClipBounds = new Rectangle(new Unit(100), new Unit(100), new Unit(500), new Unit(500)),
            ClipCornerRadius = rounded ? new Unit(250) : Unit.Zero,
        },
        new DrawRectanglePrimitive
        {
            Bounds = new Rectangle(new Unit(800), new Unit(800), new Unit(150), new Unit(150)),
            Fill = new BrushStyle(Color.Black),
        });

    private static byte[] BlackImage()
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Black);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
