using SkiaSharp;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering.Skia;

public static partial class SkiaPrimitiveRenderer
{

    private readonly record struct TextRun(string Text, SKFont Font);
}
