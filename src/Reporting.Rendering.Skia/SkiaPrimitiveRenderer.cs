using SkiaSharp;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Rendering.Skia;

/// <summary>
/// Stateless drawing primitives that target a caller-owned <see cref="SKCanvas"/>. Used by
/// both <see cref="SkiaRenderingContext"/> (bitmap-backed pages) and the PDF exporter
/// (vector-native pages via <see cref="SKDocument"/>).
/// </summary>
public static class SkiaPrimitiveRenderer
{
    public static void DrawText(SKCanvas canvas, string text, Rectangle bounds, TextStyle style, float dpi)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(style);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        using var font = CreateFont(style.Font, dpi);
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
                var runs = BuildTextRuns(lines[i], font, fallbackFonts);

                // Sum every run's width so alignment math respects emoji being slightly
                // wider than letters. Otherwise centered/right-aligned text drifts when
                // it contains an emoji glyph.
                float lineWidth = 0;
                foreach (var run in runs) lineWidth += run.Font.MeasureText(run.Text);

                float xLine = style.HorizontalAlignment switch
                {
                    HorizontalAlignment.Center => bounds.X.Px(dpi) + (bounds.Width.Px(dpi) - lineWidth) / 2f,
                    HorizontalAlignment.Right  => bounds.X.Px(dpi) + bounds.Width.Px(dpi) - lineWidth,
                    _ => bounds.X.Px(dpi),
                };

                float cursor = xLine;
                float baselineY = yStart + i * lineHeight;
                foreach (var run in runs)
                {
                    float runWidth = run.Font.MeasureText(run.Text);
                    canvas.DrawText(run.Text, cursor, baselineY, run.Font, paint);
                    if (decoPaint is not null)
                    {
                        DrawTextDecorations(canvas, decoPaint, run.Font, cursor, baselineY, runWidth, style.Font.Style);
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
    private static void DrawTextDecorations(SKCanvas canvas, SKPaint paint, SKFont font, float x, float baselineY, float width, FontStyle style)
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
            canvas.DrawLine(x, y, x + width, y, paint);
        }
        if ((style & FontStyle.Strikeout) != 0)
        {
            // StrikeoutPosition is a negative offset ABOVE the baseline (Ascent is negative).
            float y = baselineY + (m.StrikeoutPosition ?? m.Ascent * 0.35f);
            paint.StrokeWidth = m.StrikeoutThickness ?? fallbackThickness;
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
    private static List<TextRun> BuildTextRuns(string line, SKFont primary, Dictionary<SKTypeface, SKFont> cache)
    {
        var runs = new List<TextRun>(2); // most lines are 1–2 runs
        if (line.Length == 0) return runs;

        var primaryTypeface = primary.Typeface;
        var sb = new System.Text.StringBuilder(line.Length);
        SKFont currentFont = primary;

        int i = 0;
        while (i < line.Length)
        {
            // Decode one codepoint, handling surrogate pairs (emoji, CJK supplementary plane).
            int codepoint;
            int charsConsumed;
            if (char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
            {
                codepoint = char.ConvertToUtf32(line[i], line[i + 1]);
                charsConsumed = 2;
            }
            else
            {
                codepoint = line[i];
                charsConsumed = 1;
            }

            // Pick the typeface for this codepoint: primary when it has the glyph,
            // otherwise ask the OS font manager for any typeface that does. Returning
            // null (no system font has the codepoint) falls back to primary so we
            // render *something* rather than crashing.
            SKTypeface typeface;
            if (primaryTypeface.GetGlyph(codepoint) != 0)
            {
                typeface = primaryTypeface;
            }
            else
            {
                typeface = SKFontManager.Default.MatchCharacter(codepoint) ?? primaryTypeface;
            }

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

    private readonly record struct TextRun(string Text, SKFont Font);

    public static void DrawLine(SKCanvas canvas, Point from, Point to, PenStyle pen, float dpi)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(pen);
        if (!pen.IsVisible)
        {
            return;
        }
        using var paint = CreateStrokePaint(pen, dpi);
        canvas.DrawLine(from.ToSKPoint(dpi), to.ToSKPoint(dpi), paint);
    }

    public static void DrawRectangle(SKCanvas canvas, Rectangle bounds, PenStyle? pen, BrushStyle? fill, float dpi)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var rect = bounds.ToSKRect(dpi);
        if (fill is not null && fill.IsVisible)
        {
            using var paint = CreateFillPaint(fill, rect);
            canvas.DrawRect(rect, paint);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var paint = CreateStrokePaint(pen, dpi);
            canvas.DrawRect(rect, paint);
        }
    }

    public static void DrawEllipse(SKCanvas canvas, Rectangle bounds, PenStyle? pen, BrushStyle? fill, float dpi)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var rect = bounds.ToSKRect(dpi);
        if (fill is not null && fill.IsVisible)
        {
            using var paint = CreateFillPaint(fill, rect);
            canvas.DrawOval(rect, paint);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var paint = CreateStrokePaint(pen, dpi);
            canvas.DrawOval(rect, paint);
        }
    }

    public static void DrawImage(SKCanvas canvas, ReadOnlySpan<byte> imageData, Rectangle bounds, float dpi,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
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
        int? saved = p.Clip ? canvas.Save() : null;
        if (p.Clip)
        {
            canvas.ClipRect(bounds.ToSKRect(dpi));
        }
        canvas.DrawImage(image, src, dest);
        if (saved is { } s)
        {
            canvas.RestoreToCount(s);
        }
    }

    public static void DrawPath(SKCanvas canvas, Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill, float dpi)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(build);
        var builder = new SkiaPathBuilder(dpi);
        build(builder);
        using var path = builder.Path;
        if (fill is not null && fill.IsVisible)
        {
            using var paint = CreateFillPaint(fill, path.Bounds);
            canvas.DrawPath(path, paint);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var paint = CreateStrokePaint(pen, dpi);
            canvas.DrawPath(path, paint);
        }
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
        BackgroundGradientType.LeftRight     => new SKPoint(r.Left, r.MidY),
        BackgroundGradientType.DiagonalLeft  => new SKPoint(r.Left, r.Top),
        BackgroundGradientType.DiagonalRight => new SKPoint(r.Right, r.Top),
        _                                    => new SKPoint(r.MidX, r.Top), // TopBottom + fallback
    };

    private static SKPoint GradientEnd(BackgroundGradientType kind, SKRect r) => kind switch
    {
        BackgroundGradientType.LeftRight     => new SKPoint(r.Right, r.MidY),
        BackgroundGradientType.DiagonalLeft  => new SKPoint(r.Right, r.Bottom),
        BackgroundGradientType.DiagonalRight => new SKPoint(r.Left, r.Bottom),
        _                                    => new SKPoint(r.MidX, r.Bottom), // TopBottom + fallback
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
    {
        var typeface = SKTypeface.FromFamilyName(font.Family, font.Style.ToSKFontStyle())
                       ?? SKTypeface.Default;
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
