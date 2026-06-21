using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Paper;
using Reporting.Rendering;

namespace Reporting.Layout;

/// <summary>Walks a <see cref="RenderedReport"/> and replays its primitives on an
/// <see cref="IRenderingContext"/>. Backend-agnostic: works equally well for Skia, GDI, PDF, etc.</summary>
public static class RenderedReportPlayer
{
    public static void Play(RenderedReport report, IRenderingContext context)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);
        foreach (var page in report.Pages)
        {
            // For continuous (thermal) paper, Paper.Height is zero and renderers can't size
            // their surface. Compute the effective height from the primitives themselves —
            // same behaviour SkiaPdfExporter.ComputeContinuousHeightPt provides — and pass
            // a resolved PageSetup to BeginPage so every renderer (raster Skia, GDI, future
            // backends) renders the full content instead of clipping at the placeholder
            // height. Crystal Reports / SSRS / FastReport / Stimulsoft thermal previews all
            // honour the actual receipt extent.
            var pageSetup = page.PageSetup.IsContinuous
                ? ResolveContinuousHeight(page)
                : page.PageSetup;

            context.BeginPage(pageSetup);
            foreach (var primitive in page.Primitives)
            {
                Dispatch(primitive, context);
            }
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
