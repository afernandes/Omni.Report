using System.Text;
using SkiaSharp;

namespace Reporting.Rendering.Skia;

public static partial class SkiaPrimitiveRenderer
{
    // Keep the original simple-text glyph positioning, while carrying the UTF-8 text and
    // cluster offsets required by PDF's ToUnicode map. A glyph-only blob can lose that map.
    private static void DrawUnicodeText(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        int glyphCount = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            glyphCount++;
        }
        if (glyphCount == 0)
        {
            return;
        }

        using var builder = new SKTextBlobBuilder();
        var buffer = builder.AllocateRawPositionedTextRun(font, glyphCount, Encoding.UTF8.GetByteCount(text));
        Encoding.UTF8.GetBytes(text.AsSpan(), buffer.Text);
        font.GetGlyphs(buffer.Text, SKTextEncoding.Utf8, buffer.Glyphs);
        font.GetGlyphPositions(buffer.Glyphs, buffer.Positions);
        uint byteOffset = 0;
        int index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            buffer.Clusters[index++] = byteOffset;
            byteOffset += (uint)rune.Utf8SequenceLength;
        }
        using var blob = builder.Build();
        canvas.DrawText(blob, x, y, paint);
    }
}
