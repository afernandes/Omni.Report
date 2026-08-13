using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Reporting.Golden.Tests;

/// <summary>
/// Reduces an exported SVG to the part of it that is identical on every operating system.
/// </summary>
/// <remarks>
/// <para><b>Why not verify the SVG verbatim.</b> The exporter draws through SkiaSharp, and Skia writes
/// text as per-glyph advances taken from the resolved font:</para>
/// <code>&lt;text font-family="Arial" x="42.551998, 54.106686, 63.005123, …" y="57.036373"&gt;</code>
/// <para>Those advances come from the font file. Windows resolves Arial; a Linux CI runner has no
/// Arial and fontconfig substitutes something metrically different, so every number moves — and so
/// does the family name Skia writes back. A verbatim golden would be green on the author's machine
/// and permanently red in CI. The same reasoning is already recorded in <c>SkiaTestHelpers</c>, which
/// counts ink pixels precisely so it never depends on glyph shapes.</para>
///
/// <para><b>What survives, and why that is enough.</b> Shapes — <c>rect</c>, <c>ellipse</c>,
/// <c>path</c>, <c>linearGradient</c>/<c>stop</c> — are kept with every attribute: their geometry
/// comes from the display list, which is integer-mil arithmetic with no font involved. That is the
/// layer where fills, strokes and gradients silently disappear, so it is the layer worth pinning.
/// For <c>text</c> only the font size/weight and the string itself are kept, which still catches a
/// dropped, duplicated, reordered or re-styled run. Text <em>geometry</em> is not lost from the suite
/// — <see cref="LayoutGoldenTests"/> pins it from the model side, where it is deterministic.</para>
/// </remarks>
internal static partial class SvgShape
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>Attributes of &lt;text&gt; that are font-resolution artefacts, not contract.</summary>
    private static readonly HashSet<string> GlyphMetricAttributes =
        new(StringComparer.Ordinal) { "x", "y", "font-family", "textLength" };

    public static string Summarize(string svg)
    {
        ArgumentNullException.ThrowIfNull(svg);
        var doc = XDocument.Parse(svg);
        var sb = new StringBuilder();
        Walk(doc.Root!, 0, sb);
        return sb.ToString();
    }

    private static void Walk(XElement el, int depth, StringBuilder sb)
    {
        bool isText = el.Name == Svg + "text";
        sb.Append(' ', depth * 2).Append(el.Name.LocalName);

        foreach (var a in el.Attributes()
                            .Where(a => !a.IsNamespaceDeclaration)
                            .Where(a => !(isText && GlyphMetricAttributes.Contains(a.Name.LocalName)))
                            .OrderBy(a => a.Name.LocalName, StringComparer.Ordinal))
        {
            sb.Append(' ').Append(a.Name.LocalName).Append('=')
              .Append(GoldenText.Quote(Normalize(a.Value)));
        }

        if (isText)
        {
            // Skia indents the run inside the element, so Trim() first; the value itself is author
            // text and may carry quotes or newlines, hence GoldenText rather than raw interpolation.
            sb.Append(' ').Append(GoldenText.Quote(el.Value.Trim()));
        }
        sb.AppendLine();

        foreach (var child in el.Elements())
        {
            Walk(child, depth + 1, sb);
        }
    }

    /// <summary>
    /// Re-formats every number inside an attribute value at a fixed precision, leaving everything else
    /// (path commands, separators, <c>url(#id)</c>, colour names) untouched.
    /// </summary>
    /// <remarks>
    /// <para>Skia emits single-precision floats, so the same coordinate prints as <c>0.50400001</c> or
    /// <c>595.29602</c>, and the exact digits shift between Skia versions. Rounding keeps the golden
    /// readable and immune to that noise without hiding a real move.</para>
    ///
    /// <para>The rounding is applied by scanning for numbers rather than by splitting on spaces, because
    /// path data packs a command letter against the next number with no separator: <c>"M28.368
    /// 187.128L368.496"</c> has <c>187.128L368.496</c> as a single space-delimited token. The earlier
    /// version only peeled a <em>leading</em> letter, so that token failed to parse and passed through
    /// unnormalised — which is exactly how the SkiaSharp 4 upgrade produced a golden diff of
    /// <c>187.128</c> vs <c>187.12801</c>: the same coordinate, one extra digit, on the one token the
    /// normaliser could not read.</para>
    /// </remarks>
    private static string Normalize(string value)
        => Number().Replace(value, m =>
            double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d.ToString("0.###", CultureInfo.InvariantCulture)
                : m.Value);

    /// <summary>A decimal number, optionally signed and in scientific notation. The exponent's <c>e</c> is
    /// matched as part of the number so it is never mistaken for a path command — no SVG command uses that
    /// letter, but the pattern is explicit about it rather than relying on that.</summary>
    [GeneratedRegex(@"-?\d+(\.\d+)?([eE][-+]?\d+)?")]
    private static partial Regex Number();
}
