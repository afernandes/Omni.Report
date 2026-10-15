using SkiaSharp;

namespace Reporting.Rendering.Skia;

public static partial class SkiaPrimitiveRenderer
{
    /// <summary>Decodes the codepoint at <paramref name="index"/>, treating a UTF-16 surrogate pair (emoji, CJK
    /// supplementary plane) as one unit. A lone surrogate is returned as itself.</summary>
    private static int ReadCodepoint(string text, int index, out int charsConsumed)
    {
        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            charsConsumed = 2;
            return char.ConvertToUtf32(text[index], text[index + 1]);
        }
        charsConsumed = 1;
        return text[index];
    }

    /// <summary>
    /// Picks the typeface a codepoint is drawn with: the primary font's when it has the glyph, otherwise any
    /// typeface the OS font manager finds for it. Returning null (no system font has the codepoint) falls back
    /// to primary so we render *something* rather than crashing.
    /// </summary>
    private static SKTypeface ResolveTypeface(SKFont primary, int codepoint, Func<SKTypeface, SKTypeface>? substituteTypeface)
    {
        // A consulta de cobertura passou de SKTypeface para SKFont no SkiaSharp 4. Usamos o próprio
        // `primary`, que já é um SKFont sobre esta typeface — cobertura não depende do tamanho, então
        // ele responde a mesma pergunta sem custo nenhum. Criar um SKFont efêmero aqui alocaria um
        // objeto NATIVO POR CARACTERE: este laço roda por codepoint de cada linha, de cada banda, de
        // cada página.
        if (primary.GetGlyph(codepoint) != 0)
        {
            return primary.Typeface;
        }
        var fallback = SKFontManager.Default.MatchCharacter(codepoint);
        if (fallback is null)
        {
            return primary.Typeface;
        }
        return substituteTypeface is null ? fallback : substituteTypeface(fallback);
    }
}
