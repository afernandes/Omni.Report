using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Reporting.Rendering.Skia;
using SkiaSharp;

namespace Reporting.Output.Svg;

/// <summary>Preserves the original Unicode and glyph positions in native SVG text elements.</summary>
internal sealed class SvgTextWriter(MemoryStream stream)
{
    private readonly List<Replacement> _replacements = [];

    public void DrawRun(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        int start = checked((int)stream.Length);
        SkiaPrimitiveRenderer.DrawUnicodeText(canvas, text, x, y, font, paint);
        int length = checked((int)stream.Length) - start;
        if (length == 0)
        {
            return; // Skia culled this run, for example outside a clip or with transparent paint.
        }

        // SKSvgCanvas writes each draw synchronously. Restrict the replacement to THIS draw's
        // bytes: a culled run must not shift a global queue and overwrite the next visible run.
        // Keep Skia's surrounding clip groups, transforms, paint/font attributes and layer order.
        var drawn = Encoding.UTF8.GetString(stream.GetBuffer(), start, length);
        int tagStart = drawn.IndexOf("<text ", StringComparison.Ordinal);
        if (tagStart < 0)
        {
            return;
        }
        int tagEnd = drawn.IndexOf('>', tagStart);
        int closingTag = tagEnd < 0 ? -1 : drawn.IndexOf("</text>", tagEnd, StringComparison.Ordinal);
        if (closingTag < 0)
        {
            throw new InvalidOperationException("The SVG backend did not finish its text element.");
        }
        int elementEnd = closingTag + "</text>".Length;

        // Do not read the native text or its x/y lists. Skia reverse-maps glyph ids to Unicode,
        // which conflates characters sharing a glyph (e.g. NBSP/space) and can return zero on
        // Windows under concurrency, dropping both characters and their positions.
        var element = XElement.Parse(drawn[tagStart..(tagEnd + 1)] + "</text>");
        var glyphs = font.GetGlyphs(Encoding.UTF8.GetBytes(text), SKTextEncoding.Utf8);
        var positions = font.GetGlyphPositions(glyphs);
        var value = new StringBuilder(text.Length);
        var xs = new StringBuilder();
        var ys = new StringBuilder();
        int index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var position = positions[index++];
            // XML 1.0 cannot represent these control characters. Keep the positions of the
            // remaining glyphs rather than shifting them when an invalid character is omitted.
            if (!IsXmlCharacter(rune.Value))
            {
                continue;
            }
            if (value.Length > 0)
            {
                xs.Append(' ');
                ys.Append(' ');
            }
            value.Append(rune.ToString());
            xs.Append((x + position.X).ToString("G9", CultureInfo.InvariantCulture));
            ys.Append((y + position.Y).ToString("G9", CultureInfo.InvariantCulture));
        }
        element.SetAttributeValue("x", xs.ToString());
        element.SetAttributeValue("y", ys.ToString());
        element.SetAttributeValue(XNamespace.Xml + "space", "preserve");
        element.Value = value.ToString();
        _replacements.Add(new Replacement(start + Encoding.UTF8.GetByteCount(drawn.AsSpan(0, tagStart)),
            Encoding.UTF8.GetByteCount(drawn.AsSpan(tagStart, elementEnd - tagStart)),
            element.ToString(SaveOptions.DisableFormatting)));
    }

    // Call after disposing the SVG canvas, so its closing tags have reached the stream.
    public string GetSvg()
    {
        var bytes = stream.GetBuffer();
        var result = new StringBuilder(checked((int)stream.Length));
        int offset = 0;
        foreach (var replacement in _replacements)
        {
            result.Append(Encoding.UTF8.GetString(bytes, offset, replacement.Start - offset));
            result.Append(replacement.Xml);
            offset = replacement.Start + replacement.Length;
        }
        result.Append(Encoding.UTF8.GetString(bytes, offset, checked((int)stream.Length) - offset));
        return result.ToString();
    }

    private static bool IsXmlCharacter(int value) => value is 0x9 or 0xa or 0xd
        or >= 0x20 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000 and <= 0x10ffff;

    private sealed record Replacement(int Start, int Length, string Xml);
}
