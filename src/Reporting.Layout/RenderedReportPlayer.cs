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
        using var operation = Reporting.Diagnostics.ReportTelemetry.Start("render");
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
        operation.Complete();
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
                case DrawRectanglePrimitive { CornerRadius.Mils: > 0 } r:
                    // IRenderingContext has no rounded-rectangle call; a path keeps every backend (GDI, ESC/POS
                    // raster, host implementations) honouring the radius without widening the public contract.
                    context.DrawPath(builder => BuildRoundedRectangle(builder, r.Bounds, r.CornerRadius), r.Pen, r.Fill);
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

    // Control-point distance that makes a cubic Bezier approximate a quarter circle (4/3·(√2−1)).
    private const double QuarterCircleKappa = 0.5522847498;

    /// <summary>Traces a rectangle with quarter-circle corners clockwise from the top edge, as ONE connected
    /// contour. The radius is capped at half the shorter side, matching the Skia exporters.</summary>
    /// <remarks>Corners are cubic curves, not <see cref="IPathBuilder.Arc"/>: the backends implement Arc as an
    /// independent inscribed arc (Skia starts a new contour; GDI does not move its current point), so mixing it with
    /// LineTo fragments the fill and draws stray segments.</remarks>
    private static void BuildRoundedRectangle(IPathBuilder builder, Rectangle bounds, Unit radius)
    {
        var shorter = bounds.Width < bounds.Height ? bounds.Width : bounds.Height;
        var r = radius * 2 > shorter ? shorter / 2 : radius;
        var k = r * QuarterCircleKappa;
        Unit left = bounds.X, top = bounds.Y, right = bounds.X + bounds.Width, bottom = bounds.Y + bounds.Height;
        builder.MoveTo(new Point(left + r, top))
            .LineTo(new Point(right - r, top))
            .CubicTo(new Point(right - r + k, top), new Point(right, top + r - k), new Point(right, top + r))
            .LineTo(new Point(right, bottom - r))
            .CubicTo(new Point(right, bottom - r + k), new Point(right - r + k, bottom), new Point(right - r, bottom))
            .LineTo(new Point(left + r, bottom))
            .CubicTo(new Point(left + r - k, bottom), new Point(left, bottom - r + k), new Point(left, bottom - r))
            .LineTo(new Point(left, top + r))
            .CubicTo(new Point(left, top + r - k), new Point(left + r - k, top), new Point(left + r, top))
            .Close();
    }
}
