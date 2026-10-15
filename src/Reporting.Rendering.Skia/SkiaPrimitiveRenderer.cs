using SkiaSharp;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering.Skia;

/// <summary>
/// Stateless drawing primitives that target a caller-owned <see cref="SKCanvas"/>. Used by
/// both <see cref="SkiaRenderingContext"/> (bitmap-backed pages) and the PDF exporter
/// (vector-native pages via <see cref="SKDocument"/>).
/// </summary>
public static partial class SkiaPrimitiveRenderer
{
    public static void DrawText(SKCanvas canvas, string text, Rectangle bounds, TextStyle style, float dpi)
        => DrawText(canvas, text, bounds, style, dpi, null, null);

    /// <summary>Draws text exactly like <see cref="DrawText(SKCanvas, string, Rectangle, TextStyle, float)"/>, but
    /// passes every typeface it resolves — the style's font and any per-character fallback — through
    /// <paramref name="substituteTypeface"/> before drawing with it.</summary>
    /// <remarks>The substitute returns the typeface to draw with in place of the one resolved, or the same
    /// instance to keep it. The replacement must cover the same characters with the same metrics: line breaks
    /// and alignment are computed with it, so a font with different advances moves the text. The PDF exporter
    /// uses this to draw with font subsets, collected beforehand through <see cref="CollectTypefaceUsage"/>.</remarks>
    public static void DrawText(SKCanvas canvas, string text, Rectangle bounds, TextStyle style, float dpi,
        Func<SKTypeface, SKTypeface>? substituteTypeface)
        => DrawText(canvas, text, bounds, style, dpi, null, substituteTypeface);

    internal static void DrawText(SKCanvas canvas, string text, Rectangle bounds, TextStyle style, float dpi, Action<SKRect>? track)
        => DrawText(canvas, text, bounds, style, dpi, track, null);

    /// <summary>Reports, for each character <see cref="DrawText(SKCanvas, string, Rectangle, TextStyle, float)"/>
    /// would draw from <paramref name="text"/>, the typeface it would draw that character with — the font's own
    /// typeface, or the fallback found for a character it lacks.</summary>
    /// <remarks>Shares the per-character decision with <c>DrawText</c> instead of re-implementing it, so a caller
    /// that prepares typefaces ahead of drawing (the PDF exporter's font subsets) cannot miss one the renderer
    /// later reaches for. Line breaks are not reported: <c>DrawText</c> splits on them and never draws them.</remarks>
    /// <param name="text">The text, as a <c>DrawText</c> call would receive it.</param>
    /// <param name="font">The font the text is styled with. Its size is irrelevant to coverage.</param>
    /// <param name="use">Called once per drawn character with the typeface and the Unicode scalar value drawn — an
    /// unpaired surrogate is reported as U+FFFD, the replacement character it is drawn as.</param>
    public static void CollectTypefaceUsage(string text, Reporting.Styling.Font font, Action<SKTypeface, int> use)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(use);
        if (text.Length == 0)
        {
            return;
        }
        using var primary = CreateFont(font, 72f, null);
        foreach (var hardLine in text.Split('\n'))
        {
            var line = hardLine.TrimEnd('\r');
            int i = 0;
            while (i < line.Length)
            {
                int codepoint = ReadCodepoint(line, i, out int charsConsumed);
                // The typeface is chosen by the codepoint as written, exactly like BuildTextRuns does. But the glyph
                // drawn for a lone surrogate is U+FFFD — DrawUnicodeText encodes the run as UTF-8, which replaces
                // it — so that is the character the typeface has to provide.
                int drawn = System.Text.Rune.IsValid(codepoint) ? codepoint : System.Text.Rune.ReplacementChar.Value;
                use(ResolveTypeface(primary, codepoint, null), drawn);
                i += charsConsumed;
            }
        }
    }

    private static void DrawText(SKCanvas canvas, string text, Rectangle bounds, TextStyle style, float dpi,
        Action<SKRect>? track, Func<SKTypeface, SKTypeface>? substituteTypeface)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(style);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        using var font = CreateFont(style.Font, dpi, substituteTypeface);
        using var paint = new SKPaint
        {
            Color = style.ForeColor.ToSKColor(),
            IsAntialias = true,
        };
        var lines = WrapLines(text, font, bounds.Width.Px(dpi), style.WordWrap);
        var metrics = font.Metrics;
        float lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
        float totalHeight = lineHeight * lines.Count;
        float yStart = style.VerticalAlignment switch
        {
            VerticalAlignment.Middle => bounds.Y.Px(dpi) + (bounds.Height.Px(dpi) - totalHeight) / 2f - metrics.Ascent,
            VerticalAlignment.Bottom => bounds.Y.Px(dpi) + bounds.Height.Px(dpi) - totalHeight - metrics.Ascent,
            _ => bounds.Y.Px(dpi) - metrics.Ascent,
        };

        // Cache of fallback fonts for this draw call. Keyed by typeface so we only ever
        // create one SKFont per (emoji-)typeface even if a line has dozens of emoji runs.
        // The PRIMARY font is owned by the using statement above — do NOT add it to the
        // disposal list, or it'd be double-disposed.
        var fallbackFonts = new Dictionary<SKTypeface, SKFont>();
        // Skia typefaces carry no underline/strikeout — those are drawn as lines from font metrics. One
        // reusable stroke paint for the whole call (null when the style has neither decoration).
        bool decorate = (style.Font.Style & (FontStyle.Underline | FontStyle.Strikeout)) != 0;
        using var decoPaint = decorate
            ? new SKPaint { Color = style.ForeColor.ToSKColor(), IsAntialias = true, Style = SKPaintStyle.Stroke }
            : null;
        try
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var runs = BuildTextRuns(lines[i], font, fallbackFonts, substituteTypeface);

                // Sum every run's width so alignment math respects emoji being slightly
                // wider than letters. Otherwise centered/right-aligned text drifts when
                // it contains an emoji glyph.
                float lineWidth = 0;
                foreach (var run in runs) lineWidth += run.Font.MeasureText(run.Text);

                float xLine = style.HorizontalAlignment switch
                {
                    HorizontalAlignment.Center => bounds.X.Px(dpi) + (bounds.Width.Px(dpi) - lineWidth) / 2f,
                    HorizontalAlignment.Right => bounds.X.Px(dpi) + bounds.Width.Px(dpi) - lineWidth,
                    _ => bounds.X.Px(dpi),
                };

                float cursor = xLine;
                float baselineY = yStart + i * lineHeight;
                foreach (var run in runs)
                {
                    float runWidth = run.Font.MeasureText(run.Text);
                    if (track is not null && paint.Color.Alpha > 0)
                    {
                        run.Font.MeasureText(run.Text, out var ink);
                        ink.Offset(cursor, baselineY);
                        if (!ink.IsEmpty) TrackBounds(track, ink, paint);
                    }
                    DrawUnicodeText(canvas, run.Text, cursor, baselineY, run.Font, paint);
                    if (decoPaint is not null)
                    {
                        DrawTextDecorations(canvas, decoPaint, run.Font, cursor, baselineY, runWidth, style.Font.Style, track);
                    }
                    cursor += runWidth;
                }
            }
        }
        finally
        {
            foreach (var kv in fallbackFonts) kv.Value.Dispose();
        }
    }

    /// <summary>Draws underline / strikethrough lines for a run — Skia typefaces don't carry them, so they
    /// are stroked from the run's own font metrics (per-run, so a fallback/emoji run is decorated at its own
    /// width). Positions/thicknesses use the metric hints when present, with size-relative fallbacks.</summary>
    private static void DrawTextDecorations(SKCanvas canvas, SKPaint paint, SKFont font, float x, float baselineY, float width, FontStyle style, Action<SKRect>? track)
    {
        if (width <= 0)
        {
            return;
        }
        var m = font.Metrics;
        float fallbackThickness = Math.Max(1f, font.Size / 14f);
        if ((style & FontStyle.Underline) != 0)
        {
            // UnderlinePosition is a positive offset BELOW the baseline.
            float y = baselineY + (m.UnderlinePosition ?? m.Descent * 0.5f);
            paint.StrokeWidth = m.UnderlineThickness ?? fallbackThickness;
            TrackBounds(track, new SKRect(x, y, x + width, y), paint);
            canvas.DrawLine(x, y, x + width, y, paint);
        }
        if ((style & FontStyle.Strikeout) != 0)
        {
            // StrikeoutPosition is a negative offset ABOVE the baseline (Ascent is negative).
            float y = baselineY + (m.StrikeoutPosition ?? m.Ascent * 0.35f);
            paint.StrokeWidth = m.StrikeoutThickness ?? fallbackThickness;
            TrackBounds(track, new SKRect(x, y, x + width, y), paint);
            canvas.DrawLine(x, y, x + width, y, paint);
        }
    }

    /// <summary>
    /// Splits a line of text into runs where every run uses ONE typeface — the primary
    /// font when it has the codepoint, otherwise a fallback typeface found via
    /// <see cref="SKFontManager.MatchCharacter(int)"/>. This is the mechanism that makes
    /// emojis render with the system emoji font (Segoe UI Emoji / Apple Color Emoji /
    /// Noto Color Emoji) instead of showing as tofu/squares.
    /// </summary>
    /// <param name="line">The line of text to split. Handles UTF-16 surrogate pairs so
    /// codepoints above U+FFFF (most emojis, e.g. 👤 U+1F464) are treated as one unit.</param>
    /// <param name="primary">The user's chosen font. Always owns its typeface — never disposed here.</param>
    /// <param name="cache">Per-typeface SKFont cache. Filled lazily as new fallbacks are needed;
    /// the caller disposes everything in <c>finally</c>.</param>
    /// <param name="substituteTypeface">Optional replacement applied to each fallback typeface (the primary
    /// font arrives already substituted).</param>
    private static List<TextRun> BuildTextRuns(string line, SKFont primary, Dictionary<SKTypeface, SKFont> cache,
        Func<SKTypeface, SKTypeface>? substituteTypeface)
    {
        var runs = new List<TextRun>(2); // most lines are 1–2 runs
        if (line.Length == 0) return runs;

        var primaryTypeface = primary.Typeface;
        var sb = new System.Text.StringBuilder(line.Length);
        SKFont currentFont = primary;

        int i = 0;
        while (i < line.Length)
        {
            int codepoint = ReadCodepoint(line, i, out int charsConsumed);
            var typeface = ResolveTypeface(primary, codepoint, substituteTypeface);

            // Resolve to an SKFont — primary uses the caller-owned font; fallbacks live
            // in the cache so we don't recreate one per character.
            SKFont font;
            if (typeface == primaryTypeface)
            {
                font = primary;
            }
            else if (!cache.TryGetValue(typeface, out var cached))
            {
                font = new SKFont(typeface, primary.Size);
                cache[typeface] = font;
            }
            else
            {
                font = cached;
            }

            // Flush the previous run when the font changes — every run must be one font.
            if (font != currentFont && sb.Length > 0)
            {
                runs.Add(new TextRun(sb.ToString(), currentFont));
                sb.Clear();
            }
            currentFont = font;
            sb.Append(line, i, charsConsumed);
            i += charsConsumed;
        }

        if (sb.Length > 0)
        {
            runs.Add(new TextRun(sb.ToString(), currentFont));
        }
        return runs;
    }

    public static void DrawLine(SKCanvas canvas, Point from, Point to, PenStyle pen, float dpi)
        => DrawLine(canvas, from, to, pen, dpi, null);

    internal static void DrawLine(SKCanvas canvas, Point from, Point to, PenStyle pen, float dpi, Action<SKRect>? track)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(pen);
        if (!pen.IsVisible)
        {
            return;
        }
        using var paint = CreateStrokePaint(pen, dpi);
        TrackBounds(track, new SKRect(Math.Min(from.X.Px(dpi), to.X.Px(dpi)), Math.Min(from.Y.Px(dpi), to.Y.Px(dpi)),
            Math.Max(from.X.Px(dpi), to.X.Px(dpi)), Math.Max(from.Y.Px(dpi), to.Y.Px(dpi))), paint);
        canvas.DrawLine(from.ToSKPoint(dpi), to.ToSKPoint(dpi), paint);
    }

    public static void DrawRectangle(SKCanvas canvas, Rectangle bounds, PenStyle? pen, BrushStyle? fill, float dpi)
        => DrawRectangle(canvas, bounds, pen, fill, dpi, null);

    /// <summary>Draws a rectangle whose corners are rounded by <paramref name="cornerRadius"/>; zero draws square
    /// corners, exactly like the overload without a radius. The radius is capped at half the shorter side so a
    /// large value degrades to a pill shape instead of an inverted path.</summary>
    public static void DrawRectangle(SKCanvas canvas, Rectangle bounds, Unit cornerRadius, PenStyle? pen, BrushStyle? fill, float dpi)
        => DrawRectangle(canvas, bounds, pen, fill, dpi, null, cornerRadius);

    internal static void DrawRectangle(SKCanvas canvas, Rectangle bounds, PenStyle? pen, BrushStyle? fill, float dpi,
        Action<SKRect>? track, Unit cornerRadius = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var rect = bounds.ToSKRect(dpi);
        float radius = cornerRadius > Unit.Zero
            ? Math.Min((float)cornerRadius.ToPixels(dpi), Math.Min(rect.Width, rect.Height) / 2f)
            : 0f;
        if (fill is not null && fill.IsVisible)
        {
            using var paint = CreateFillPaint(fill, rect);
            TrackBounds(track, rect, paint, includeTransparent: fill is { HasGradient: true });
            DrawRect(canvas, rect, radius, paint);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var paint = CreateStrokePaint(pen, dpi);
            TrackBounds(track, rect, paint);
            DrawRect(canvas, rect, radius, paint);
        }

        static void DrawRect(SKCanvas canvas, SKRect rect, float radius, SKPaint paint)
        {
            if (radius > 0f) canvas.DrawRoundRect(rect, radius, radius, paint);
            else canvas.DrawRect(rect, paint);
        }
    }

    public static void DrawEllipse(SKCanvas canvas, Rectangle bounds, PenStyle? pen, BrushStyle? fill, float dpi)
        => DrawEllipse(canvas, bounds, pen, fill, dpi, null);

    internal static void DrawEllipse(SKCanvas canvas, Rectangle bounds, PenStyle? pen, BrushStyle? fill, float dpi, Action<SKRect>? track)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var rect = bounds.ToSKRect(dpi);
        if (fill is not null && fill.IsVisible)
        {
            using var paint = CreateFillPaint(fill, rect);
            TrackBounds(track, rect, paint, includeTransparent: fill is { HasGradient: true });
            canvas.DrawOval(rect, paint);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var paint = CreateStrokePaint(pen, dpi);
            TrackBounds(track, rect, paint);
            canvas.DrawOval(rect, paint);
        }
    }

    public static void DrawImage(SKCanvas canvas, ReadOnlySpan<byte> imageData, Rectangle bounds, float dpi,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
        => DrawImage(canvas, imageData, bounds, dpi, sizing, null);

    internal static void DrawImage(SKCanvas canvas, ReadOnlySpan<byte> imageData, Rectangle bounds, float dpi,
        Reporting.Elements.ImageSizing sizing, Action<SKRect>? track)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (imageData.IsEmpty)
        {
            return;
        }
        using var data = SKData.CreateCopy(imageData);
        using var image = SKImage.FromEncodedData(data);
        if (image is null)
        {
            return;
        }
        var p = Reporting.Elements.ImageSizingMath.Compute(sizing, bounds, image.Width, image.Height);
        var dest = p.Dest.ToSKRect(dpi);
        var src = SKRect.Create(
            (float)(p.SrcX * image.Width), (float)(p.SrcY * image.Height),
            (float)(p.SrcW * image.Width), (float)(p.SrcH * image.Height));
        var ink = dest;
        if (p.Clip) ink.Intersect(bounds.ToSKRect(dpi));
        track?.Invoke(ink);
        int? saved = p.Clip ? canvas.Save() : null;
        if (p.Clip)
        {
            canvas.ClipRect(bounds.ToSKRect(dpi));
        }
        // SKSamplingOptions.Default reproduz a amostragem que a sobrecarga antiga usava. Trocar por
        // filtragem linear melhoraria imagem reduzida, mas mudaria pixel — seria decisão de qualidade, não
        // de migração, e escaparia à revisão deste PR.
        canvas.DrawImage(image, src, dest, SKSamplingOptions.Default, paint: null);
        if (saved is { } s)
        {
            canvas.RestoreToCount(s);
        }
    }

    public static void DrawPath(SKCanvas canvas, Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill, float dpi)
        => DrawPath(canvas, build, pen, fill, dpi, null);

    internal static void DrawPath(SKCanvas canvas, Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill, float dpi, Action<SKRect>? track)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(build);
        using var builder = new SkiaPathBuilder(dpi);
        build(builder);
        using var path = builder.DetachPath();
        if (fill is not null && fill.IsVisible)
        {
            using var paint = CreateFillPaint(fill, path.Bounds);
            if (track is not null)
            {
                using var outline = paint.GetFillPath(path);
                if (!outline.IsEmpty && (paint.Color.Alpha > 0 || paint.Shader is not null))
                    TrackBounds(track, outline.TightBounds, null, includeTransparent: true);
            }
            canvas.DrawPath(path, paint);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var paint = CreateStrokePaint(pen, dpi);
            if (track is not null)
            {
                using var outline = paint.GetFillPath(path);
                if (!outline.IsEmpty && (paint.Color.Alpha > 0 || paint.Shader is not null))
                    TrackBounds(track, outline.TightBounds, null, includeTransparent: true);
            }
            canvas.DrawPath(path, paint);
        }
    }

    private static void TrackBounds(Action<SKRect>? track, SKRect bounds, SKPaint? paint, bool includeTransparent = false)
    {
        if (track is null || (!includeTransparent && paint is not null && paint.Color.Alpha == 0)) return;
        float padding = paint?.Style == SKPaintStyle.Stroke ? paint.StrokeWidth / 2 : 0;
        // One device unit covers raster antialiasing / glyph hinting at the page edge.
        bounds.Inflate(padding + 1, padding + 1);
        track(bounds);
    }

    /// <summary>Builds the fill paint for a primitive: a solid colour, or a two-colour gradient shader when the
    /// brush carries one. <paramref name="bounds"/> (already in pixels) anchors the gradient's start/end points;
    /// <see cref="BackgroundGradientType.Center"/> is a radial blend, the rest are linear.</summary>
    private static SKPaint CreateFillPaint(BrushStyle fill, SKRect bounds)
    {
        var paint = new SKPaint { Color = fill.Color.ToSKColor(), Style = SKPaintStyle.Fill, IsAntialias = true };
        if (fill.HasGradient && fill.GradientEndColor is { } end)
        {
            var colors = new[] { fill.Color.ToSKColor(), end.ToSKColor() };
            paint.Shader = fill.Gradient == BackgroundGradientType.Center
                ? SKShader.CreateRadialGradient(
                    new SKPoint(bounds.MidX, bounds.MidY), Math.Max(bounds.Width, bounds.Height) / 2f,
                    colors, null, SKShaderTileMode.Clamp)
                : SKShader.CreateLinearGradient(
                    GradientStart(fill.Gradient, bounds), GradientEnd(fill.Gradient, bounds),
                    colors, null, SKShaderTileMode.Clamp);
        }
        return paint;
    }

    private static SKPoint GradientStart(BackgroundGradientType kind, SKRect r) => kind switch
    {
        BackgroundGradientType.LeftRight => new SKPoint(r.Left, r.MidY),
        BackgroundGradientType.DiagonalLeft => new SKPoint(r.Left, r.Top),
        BackgroundGradientType.DiagonalRight => new SKPoint(r.Right, r.Top),
        _ => new SKPoint(r.MidX, r.Top), // TopBottom + fallback
    };

    private static SKPoint GradientEnd(BackgroundGradientType kind, SKRect r) => kind switch
    {
        BackgroundGradientType.LeftRight => new SKPoint(r.Right, r.MidY),
        BackgroundGradientType.DiagonalLeft => new SKPoint(r.Right, r.Bottom),
        BackgroundGradientType.DiagonalRight => new SKPoint(r.Left, r.Bottom),
        _ => new SKPoint(r.MidX, r.Bottom), // TopBottom + fallback
    };

    /// <summary>Applies <paramref name="clip"/> (if any) to the canvas and returns the save-count to restore
    /// after the primitive is drawn (null = nothing pushed). Shared by every SKCanvas-based replay loop
    /// (PDF/PNG/SVG exporters) so container-rectangle children are cut consistently.</summary>
    public static int? BeginClip(SKCanvas canvas, Rectangle? clip, Unit cornerRadius, float dpi)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (clip is not { } c)
        {
            return null;
        }
        int saved = canvas.Save();
        ApplyClip(canvas, c, cornerRadius, dpi);
        return saved;
    }

    /// <summary>Clips the canvas to <paramref name="bounds"/>, rounding the corners by
    /// <paramref name="cornerRadius"/> when it's positive (a container <c>Rectangle</c> with CornerRadius).
    /// The caller is responsible for the surrounding Save/Restore.</summary>
    public static void ApplyClip(SKCanvas canvas, Rectangle bounds, Unit cornerRadius, float dpi)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var rect = bounds.ToSKRect(dpi);
        if (cornerRadius > Unit.Zero)
        {
            float r = (float)cornerRadius.ToPixels(dpi);
            using var rrect = new SKRoundRect(rect, r, r);
            canvas.ClipRoundRect(rrect, antialias: true);
        }
        else
        {
            canvas.ClipRect(rect);
        }
    }

    /// <summary>Restores the canvas to the count returned by <see cref="BeginClip"/> (no-op when null).</summary>
    public static void EndClip(SKCanvas canvas, int? saved)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (saved is { } s)
        {
            canvas.RestoreToCount(s);
        }
    }

    public static Size MeasureText(string text, TextStyle style, Unit? maxWidth, float dpi)
    {
        ArgumentNullException.ThrowIfNull(style);
        using var font = CreateFont(style.Font, dpi);
        var metrics = font.Metrics;
        float lineHeightPx = metrics.Descent - metrics.Ascent + metrics.Leading;
        if (string.IsNullOrEmpty(text))
        {
            return new Size(Unit.Zero, Unit.FromPixels(lineHeightPx, dpi));
        }
        var maxWidthPx = maxWidth?.Px(dpi);
        var lines = WrapLines(text, font, maxWidthPx ?? float.PositiveInfinity, style.WordWrap);
        float widthPx = lines.Max(l => font.MeasureText(l));
        float heightPx = lineHeightPx * lines.Count;
        return new Size(Unit.FromPixels(widthPx, dpi), Unit.FromPixels(heightPx, dpi));
    }

    internal static SKFont CreateFont(Reporting.Styling.Font font, float dpi)
        => CreateFont(font, dpi, null);

    private static SKFont CreateFont(Reporting.Styling.Font font, float dpi, Func<SKTypeface, SKTypeface>? substituteTypeface)
    {
        var typeface = SKTypeface.FromFamilyName(font.Family, font.Style.ToSKFontStyle())
                       ?? SKTypeface.Default;
        if (substituteTypeface is not null)
        {
            typeface = substituteTypeface(typeface);
        }
        return new SKFont(typeface, (float)font.Size * dpi / 72f);
    }

    internal static SKPaint CreateStrokePaint(PenStyle pen, float dpi)
    {
        var strokeWidth = pen.Thickness.Px(dpi);
        var paint = new SKPaint
        {
            Color = pen.Color.ToSKColor(),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
        };
        var dash = pen.Style.ToSKDashEffect(strokeWidth);
        if (dash is not null)
        {
            paint.PathEffect = dash;
        }
        return paint;
    }

    /// <summary>Greedy word-wrap. Lines that exceed <paramref name="maxWidthPx"/> are broken at
    /// whitespace boundaries. Single words longer than the line are rendered without breaking.</summary>
    internal static List<string> WrapLines(string text, SKFont font, float maxWidthPx, bool wordWrap)
    {
        var lines = new List<string>();
        foreach (var hard in text.Split('\n'))
        {
            var trimmed = hard.TrimEnd('\r');
            if (!wordWrap || float.IsInfinity(maxWidthPx) || font.MeasureText(trimmed) <= maxWidthPx)
            {
                lines.Add(trimmed);
                continue;
            }
            var words = trimmed.Split(' ');
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureText(candidate) <= maxWidthPx)
                {
                    current = candidate;
                }
                else
                {
                    if (current.Length > 0)
                    {
                        lines.Add(current);
                    }
                    current = word;
                }
            }
            if (current.Length > 0)
            {
                lines.Add(current);
            }
        }
        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }
        return lines;
    }
}
