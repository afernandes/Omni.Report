using System.Buffers.Binary;
using System.Text;
using SkiaSharp;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Rendering.Skia;

namespace Reporting.Output.Pdf;

/// <summary>
/// Stands in, for each typeface a report draws text with, a subset of it holding only the characters the report
/// uses — so the PDF embeds the glyphs on the page instead of the whole font program.
/// </summary>
/// <remarks>
/// <para><b>Why the exporter does this and not Skia.</b> Skia's PDF backend subsets fonts through HarfBuzz — when
/// it is compiled with it. The libSkiaSharp shipped on NuGet is not (its binaries carry no <c>hb_*</c> symbol), so
/// <c>SkPDFSubsetFont</c> is the stub that returns nothing and Skia falls back to embedding the whole font file,
/// while still writing the "ABCDEF+" subset prefix into the font name. A one-page report came out at 1.1 MB, 94% of
/// it two full copies of Arial; a CJK font would have added tens of megabytes.</para>
/// <para><b>How.</b> Skia embeds whatever font program backs the typeface it draws with. So the subsetting happens
/// before drawing: a first pass collects, per typeface, every character drawn with it — through
/// <see cref="SkiaPrimitiveRenderer.CollectTypefaceUsage"/>, which shares the renderer's own per-character decision,
/// fallback fonts included — HarfBuzz cuts a font holding only those characters, and the text is then drawn with a
/// typeface created from that cut. Outlines, advances and metrics are the font's own, so the page lays out the same.</para>
/// <para><b>Never worse than before.</b> A typeface that cannot be subsetted is drawn as it always was and embedded
/// whole: the native library missing, a license that forbids subsetting (OS/2 <c>fsType</c>), a variable-font instance
/// (the cut would come back at the default instance), or a subset that does not report the same family and style
/// and the same advances and line metrics at every size the report uses.</para>
/// </remarks>
internal sealed class PdfFontSubsetter : IDisposable
{
    /// <summary>The OpenType 'OS/2' table tag.</summary>
    private const uint Os2Tag = 0x4F532F32;

    /// <summary>fsType bits under which a subset must not be produced: restricted-license embedding (0x0002 — the
    /// font may not be embedded at all), no-subsetting (0x0100) and bitmap-embedding-only (0x0200).</summary>
    private const ushort FsTypeForbidsSubset = 0x0002 | 0x0100 | 0x0200;

    private readonly Dictionary<TypefaceKey, SKTypeface> _subsets;

    private PdfFontSubsetter(Dictionary<TypefaceKey, SKTypeface> subsets) => _subsets = subsets;

    /// <summary>Collects what <paramref name="report"/> draws and builds a subset for every typeface that allows it.</summary>
    /// <param name="report">The report about to be exported.</param>
    /// <param name="dpi">The DPI the exporter draws at — fonts are sized from it, and the subsets are checked at
    /// those exact sizes.</param>
    /// <param name="cancellationToken">Checked between pages of the collection pass.</param>
    public static PdfFontSubsetter Create(RenderedReport report, float dpi, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        var usage = new Dictionary<TypefaceKey, TypefaceUsage>();
        // The callback runs once per character; reading a typeface's family name is a native call plus a string,
        // so each instance is keyed once and remembered.
        var byInstance = new Dictionary<SKTypeface, TypefaceUsage>(ReferenceEqualityComparer.Instance);
        float size = 0;
        Action<SKTypeface, int> use = (typeface, codepoint) =>
        {
            if (!byInstance.TryGetValue(typeface, out var entry))
            {
                var key = TypefaceKey.Of(typeface);
                if (!usage.TryGetValue(key, out entry))
                {
                    entry = new TypefaceUsage(typeface);
                    usage[key] = entry;
                }
                byInstance[typeface] = entry;
            }
            entry.Codepoints.Add(codepoint);
            entry.Sizes.Add(size);
        };
        foreach (var page in report.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var primitive in page.Primitives)
            {
                if (primitive is DrawTextPrimitive text)
                {
                    // Same expression SkiaPrimitiveRenderer sizes its SKFont with — fallback fonts inherit it.
                    size = (float)text.Style.Font.Size * dpi / 72f;
                    SkiaPrimitiveRenderer.CollectTypefaceUsage(text.Text, text.Style.Font, use);
                }
            }
        }

        var subsets = new Dictionary<TypefaceKey, SKTypeface>();
        foreach (var (key, entry) in usage)
        {
            var subset = TrySubset(entry);
            if (subset is not null)
            {
                subsets[key] = subset;
            }
        }
        return new PdfFontSubsetter(subsets);
    }

    /// <summary>The typeface to draw with in place of <paramref name="typeface"/>: its subset when one was built,
    /// otherwise the typeface itself.</summary>
    public SKTypeface Substitute(SKTypeface typeface)
        => _subsets.TryGetValue(TypefaceKey.Of(typeface), out var subset) ? subset : typeface;

    public void Dispose()
    {
        foreach (var subset in _subsets.Values)
        {
            subset.Dispose();
        }
        _subsets.Clear();
    }

    private static SKTypeface? TrySubset(TypefaceUsage usage)
    {
        var original = usage.Original;
        if (original.VariationDesignPositionCount > 0 || !LicenseAllowsSubsetting(original))
        {
            return null;
        }

        byte[]? subsetFile;
        using (var stream = original.OpenStream(out int faceIndex))
        {
            if (stream is null)
            {
                return null;
            }
            using var file = SKData.Create(stream);
            if (file is null)
            {
                return null;
            }
            subsetFile = HarfBuzzSubsetter.Subset(file.Data, file.Size, faceIndex, usage.Codepoints);
        }
        if (subsetFile is null)
        {
            return null;
        }

        using var data = SKData.CreateCopy(subsetFile);
        var subset = SKTypeface.FromData(data);
        if (subset is null)
        {
            return null;
        }
        if (!DrawsLike(original, subset, usage))
        {
            subset.Dispose();
            return null;
        }
        return subset;
    }

    /// <summary>Fonts without an OS/2 table (some Apple TrueType fonts) declare no restriction.</summary>
    private static bool LicenseAllowsSubsetting(SKTypeface typeface)
    {
        if (!typeface.TryGetTableData(Os2Tag, out var os2) || os2 is null || os2.Length < 10)
        {
            return true;
        }
        ushort fsType = BinaryPrimitives.ReadUInt16BigEndian(os2.AsSpan(8, 2));
        return (fsType & FsTypeForbidsSubset) == 0;
    }

    /// <summary>
    /// The subset is only a substitute if text drawn with it lands where the layout engine measured it with the
    /// original. That is the case by construction — same outlines, same hmtx — but a font manager can attach state
    /// to a system typeface that a typeface built from bytes does not get (DirectWrite's simulated bold, a
    /// fontconfig hinting setting), so it is checked rather than assumed: same family and style, and the same
    /// advance for every collected character and the same line metrics at every size the report uses.
    /// </summary>
    private static bool DrawsLike(SKTypeface original, SKTypeface subset, TypefaceUsage usage)
    {
        if (TypefaceKey.Of(original) != TypefaceKey.Of(subset))
        {
            return false;
        }
        var text = new StringBuilder(usage.Codepoints.Count * 2);
        foreach (var codepoint in usage.Codepoints)
        {
            // A lone surrogate is collected as itself (the renderer draws it as .notdef) but is not a scalar value.
            if (Rune.IsValid(codepoint))
            {
                text.Append(char.ConvertFromUtf32(codepoint));
            }
        }
        var characters = text.ToString();
        foreach (var size in usage.Sizes)
        {
            using var expected = new SKFont(original, size);
            using var actual = new SKFont(subset, size);
            if (!expected.GetGlyphWidths(characters).AsSpan().SequenceEqual(actual.GetGlyphWidths(characters)))
            {
                return false;
            }
            SKFontMetrics a = expected.Metrics, b = actual.Metrics;
            if (a.Ascent != b.Ascent || a.Descent != b.Descent || a.Leading != b.Leading
                || a.UnderlinePosition != b.UnderlinePosition || a.UnderlineThickness != b.UnderlineThickness
                || a.StrikeoutPosition != b.StrikeoutPosition || a.StrikeoutThickness != b.StrikeoutThickness)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>What identifies a typeface across lookups. The renderer resolves typefaces by family and style on
    /// every draw call, so instance identity is not stable; family plus style is what those lookups match on.</summary>
    private readonly record struct TypefaceKey(string Family, int Weight, int Width, SKFontStyleSlant Slant)
    {
        public static TypefaceKey Of(SKTypeface typeface)
            => new(typeface.FamilyName, typeface.FontWeight, typeface.FontWidth, typeface.FontSlant);
    }

    private sealed class TypefaceUsage(SKTypeface original)
    {
        public SKTypeface Original { get; } = original;
        public HashSet<int> Codepoints { get; } = [];
        public HashSet<float> Sizes { get; } = [];
    }
}
