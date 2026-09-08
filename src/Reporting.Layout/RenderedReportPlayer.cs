using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering;

namespace Reporting.Layout;

/// <summary>Walks a <see cref="RenderedReport"/> and replays its primitives on an
/// <see cref="IRenderingContext"/>. Backend-agnostic: works equally well for Skia, GDI, PDF, etc.</summary>
public static class RenderedReportPlayer
{
    /// <summary>Replays every page of <paramref name="report"/> onto <paramref name="context"/>, opening and
    /// closing each page around its primitives. This is the single place page framing is decided, so every
    /// backend gets the same structure without repeating it.</summary>
    public static void Play(RenderedReport report, IRenderingContext context)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        foreach (var page in report.Pages)
        {
            PlayPage(page, context);
        }
    }

    /// <summary>Replays one page, balancing page framing and each primitive's clipping even on failure.</summary>
    public static void PlayPage(RenderedPage page, IRenderingContext context)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(context);
        // Resolve continuous paper before page framing so owned raster surfaces have
        // enough space for all content. Borrowed canvases retain their caller-defined size.
        var pageSetup = page.PageSetup.IsContinuous
            ? ResolveContinuousHeight(page)
            : page.PageSetup;

        context.BeginPage(pageSetup);
        try
        {
            foreach (var primitive in page.Primitives)
            {
                Dispatch(primitive, context);
            }
        }
        finally
        {
            context.EndPage();
        }
    }

    /// <summary>Returns a new <see cref="PageSetup"/> with <c>Paper.Height</c> replaced by the
    /// bottom of the lowest primitive plus the configured bottom margin (minimum 1 mil so the
    /// surface is never zero-height).</summary>
    private static PageSetup ResolveContinuousHeight(RenderedPage page)
    {
        Unit maxBottom = Unit.Zero;
        foreach (var p in page.Primitives)
        {
            if (p.Bounds.Bottom > maxBottom)
            {
                maxBottom = p.Bounds.Bottom;
            }
        }
        var effectiveHeight = maxBottom + page.PageSetup.Margins.Bottom;
        if (effectiveHeight <= Unit.Zero)
        {
            effectiveHeight = Unit.FromMm(1);
        }
        var paper = page.PageSetup.Paper with { Height = effectiveHeight };
        return page.PageSetup with { Paper = paper };
    }

    private static void Dispatch(LayoutPrimitive primitive, IRenderingContext context)
    {
        // Container clip: constrain this primitive to its rectangle (set for container-rect children).
        // Push/Pop are no-ops on backends that don't clip, so unclipped output is unchanged.
        bool clipped = primitive.ClipBounds is not null;
        if (clipped)
        {
            context.PushClip(primitive.ClipBounds!.Value, primitive.ClipCornerRadius);
        }
        try
        {
        switch (primitive)
        {
            case DrawTextPrimitive t:
                context.DrawText(t.Text, t.Bounds, t.Style);
                break;
            case DrawLinePrimitive l:
                context.DrawLine(l.From, l.To, l.Pen);
                break;
            case DrawRectanglePrimitive r:
                context.DrawRectangle(r.Bounds, r.Pen, r.Fill);
                break;
            case DrawEllipsePrimitive e:
                context.DrawEllipse(e.Bounds, e.Pen, e.Fill);
                break;
            case DrawImagePrimitive i:
                ReadOnlySpan<byte> span;
                if (i.Data.Count == 0)
                {
                    span = ReadOnlySpan<byte>.Empty;
                }
                else
                {
                    var copy = new byte[i.Data.Count];
                    for (int k = 0; k < copy.Length; k++)
                    {
                        copy[k] = i.Data[k];
                    }
                    span = copy;
                }
                context.DrawImage(span, i.Bounds, i.Sizing);
                break;
            case DrawPolygonPrimitive poly:
                context.DrawPath(poly.BuildPath, poly.Pen, poly.Fill);
                break;
            default:
                throw new NotSupportedException($"Primitiva de layout não suportada: {primitive.GetType().FullName}.");
        }
        }
        finally
        {
            // Always balance the clip — a draw that throws must not leak the clip onto later primitives.
            if (clipped)
            {
                context.PopClip();
            }
        }
    }
}
