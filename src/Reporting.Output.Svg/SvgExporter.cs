using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Output.Pdf;
using Reporting.Rendering.Skia;

namespace Reporting.Output.Svg;

/// <summary>
/// Exports a <see cref="RenderedReport"/> to vector SVG using SkiaSharp's
/// <see cref="SKSvgCanvas"/>. Reuses the same <c>SkiaPrimitiveRenderer</c> that drives the
/// PDF backend, so the SVG is visually identical to the vector PDF — text remains selectable,
/// shapes stay as paths, fills/strokes preserved.
/// </summary>
/// <remarks>
/// <para>Two output modes:</para>
/// <list type="bullet">
/// <item><b><see cref="RenderPage"/></b> — per-page fragment, used to embed inside larger
/// documents (HTML envelopes, PDFs that need a vector overlay, …).</item>
/// <item><b><see cref="Export"/></b> — single composite SVG document with all pages stacked
/// vertically. Suitable as a <c>.svg</c> file.</item>
/// </list>
/// </remarks>
public sealed class SvgExporter : IReportExporter
{
    /// <summary>SVG (like PDF) measures coordinates in points (1pt = 1/72 inch).</summary>
    public const float SvgDpi = 72f;

    private readonly SvgExportOptions _options;

    public SvgExporter(SvgExportOptions? options = null)
    {
        _options = options ?? SvgExportOptions.Default;
    }

    public string Format => "svg";
    public string FileExtension => ".svg";
    public string ContentType => "image/svg+xml";

    /// <summary>Renders a single page to a self-contained SVG fragment ready to embed.</summary>
    /// <remarks>The fragment carries an explicit <c>viewBox</c> and
    /// <c>width="100%" height="100%"</c> — embed it inside any sized container and the
    /// content scales to fit.</remarks>
    public SvgPageFragment RenderPage(RenderedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var widthPt = (float)page.PageSetup.PageWidth.ToPoints();
        var heightPt = (float)(page.PageSetup.IsContinuous
            ? Math.Max((double)page.PageSetup.Margins.Top.ToPoints() + 1, ComputeContinuousHeightPt(page))
            : page.PageSetup.PageHeight.ToPoints());

        using var ms = new MemoryStream();
        var bounds = new SKRect(0, 0, widthPt, heightPt);
        using (var canvas = SKSvgCanvas.Create(bounds, ms))
        {
            DrawPage(canvas, page, widthPt, heightPt);
        }
        var raw = Encoding.UTF8.GetString(ms.ToArray());
        var cleaned = InjectViewBox(StripXmlProlog(raw), widthPt, heightPt);
        return new SvgPageFragment(widthPt, heightPt, cleaned);
    }

    /// <summary>Exports the entire report as a single composite SVG with pages stacked
    /// vertically and separated by <see cref="SvgExportOptions.PageGap"/>.</summary>
    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        // Pre-compute every page's intrinsic size up front so we know the outer viewBox.
        var dims = new List<(float Width, float Height)>(report.Pages.Count);
        float maxWidth = 0f;
        float totalHeight = 0f;
        foreach (var page in report.Pages)
        {
            float w = (float)page.PageSetup.PageWidth.ToPoints();
            float h = (float)(page.PageSetup.IsContinuous
                ? Math.Max((double)page.PageSetup.Margins.Top.ToPoints() + 1, ComputeContinuousHeightPt(page))
                : page.PageSetup.PageHeight.ToPoints());
            dims.Add((w, h));
            if (w > maxWidth) maxWidth = w;
            totalHeight += h;
        }
        if (report.Pages.Count > 1)
        {
            totalHeight += (report.Pages.Count - 1) * _options.PageGap;
        }
        if (maxWidth <= 0) maxWidth = 595.296f;   // safety: A4 portrait
        if (totalHeight <= 0) totalHeight = 841.896f;

        // One SKSvgCanvas for the whole document. Each page is drawn at the right Y offset
        // via canvas.Translate — that way the output is a single coherent coordinate
        // system, not nested <svg> elements.
        using var ms = new MemoryStream();
        var docBounds = new SKRect(0, 0, maxWidth, totalHeight);
        using (var canvas = SKSvgCanvas.Create(docBounds, ms))
        {
            float currentY = 0f;
            for (int i = 0; i < report.Pages.Count; i++)
            {
                var page = report.Pages[i];
                var (w, h) = dims[i];
                canvas.Save();
                canvas.Translate(0, currentY);
                DrawPage(canvas, page, w, h);
                canvas.Restore();
                currentY += h + _options.PageGap;
            }
        }

        var raw = Encoding.UTF8.GetString(ms.ToArray());
        var withViewBox = InjectViewBox(StripXmlProlog(raw), maxWidth, totalHeight);
        // Standalone .svg gets the XML prolog back; embedded HTML usage strips it again.
        var bytes = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + withViewBox);
        output.Write(bytes, 0, bytes.Length);
    }

    private void DrawPage(SKCanvas canvas, RenderedPage page, float widthPt, float heightPt)
    {
        if (_options.IncludeBackground)
        {
            var bgColor = ParseHexColor(_options.PageBackgroundColor);
            using var bg = new SKPaint { Color = bgColor, Style = SKPaintStyle.Fill };
            canvas.DrawRect(0, 0, widthPt, heightPt, bg);
        }
        foreach (var primitive in page.Primitives)
        {
            Replay(canvas, primitive);
        }
    }

    private static SKColor ParseHexColor(string value)
    {
        var trimmed = value.AsSpan().TrimStart('#');
        if (SKColor.TryParse(trimmed.ToString(), out var c))
        {
            return c;
        }
        return SKColors.White;
    }

    private static void Replay(SKCanvas canvas, LayoutPrimitive primitive)
    {
        var clip = SkiaPrimitiveRenderer.BeginClip(canvas, primitive.ClipBounds, primitive.ClipCornerRadius, SvgDpi);
        switch (primitive)
        {
            case DrawTextPrimitive t:
                SkiaPrimitiveRenderer.DrawText(canvas, t.Text, t.Bounds, t.Style, SvgDpi);
                break;
            case DrawLinePrimitive l:
                SkiaPrimitiveRenderer.DrawLine(canvas, l.From, l.To, l.Pen, SvgDpi);
                break;
            case DrawRectanglePrimitive r:
                SkiaPrimitiveRenderer.DrawRectangle(canvas, r.Bounds, r.Pen, r.Fill, SvgDpi);
                break;
            case DrawEllipsePrimitive e:
                SkiaPrimitiveRenderer.DrawEllipse(canvas, e.Bounds, e.Pen, e.Fill, SvgDpi);
                break;
            case DrawImagePrimitive i:
                if (i.Data.Count > 0)
                {
                    var copy = new byte[i.Data.Count];
                    for (int k = 0; k < copy.Length; k++)
                    {
                        copy[k] = i.Data[k];
                    }
                    SkiaPrimitiveRenderer.DrawImage(canvas, copy, i.Bounds, SvgDpi, i.Sizing);
                }
                break;
            case DrawPolygonPrimitive poly:
                SkiaPrimitiveRenderer.DrawPath(canvas, poly.BuildPath, poly.Pen, poly.Fill, SvgDpi);
                break;
        }
        SkiaPrimitiveRenderer.EndClip(canvas, clip);
    }

    /// <summary>Removes the leading <c>&lt;?xml …?&gt;</c> declaration. Required when embedding
    /// SVG inside an HTML5 document (the prolog isn't allowed there). The standalone-SVG
    /// path adds it back.</summary>
    private static string StripXmlProlog(string xml)
    {
        var trimmed = xml.AsSpan().TrimStart();
        if (trimmed.StartsWith("<?xml"))
        {
            var end = trimmed.IndexOf("?>", StringComparison.Ordinal);
            if (end >= 0)
            {
                return trimmed[(end + 2)..].TrimStart().ToString();
            }
        }
        return trimmed.ToString();
    }

    /// <summary>SkiaSharp's <see cref="SKSvgCanvas"/> emits <c>&lt;svg width="W" height="H"&gt;</c>
    /// without a <c>viewBox</c>. Without one, the SVG's internal coordinate system stays
    /// locked at <c>0..W × 0..H</c> while CSS resizes the outer box — content draws in the
    /// top-left of an enlarged canvas instead of scaling. We rewrite the opening tag to add
    /// an explicit <c>viewBox</c> and let CSS drive the rendered size.</summary>
    private static string InjectViewBox(string svg, float widthPt, float heightPt)
    {
        var viewBox = string.Format(
            CultureInfo.InvariantCulture,
            "viewBox=\"0 0 {0:0.###} {1:0.###}\"",
            widthPt, heightPt);
        return SvgOpeningTagRegex.Replace(
            svg,
            m => $"<svg{m.Groups["pre"].Value} {viewBox} width=\"100%\" height=\"100%\" preserveAspectRatio=\"xMidYMid meet\"{m.Groups["post"].Value}>",
            count: 1);
    }

    private static readonly Regex SvgOpeningTagRegex = new(
        @"<svg(?<pre>[^>]*?)\s+width=""[^""]+""\s+height=""[^""]+""(?<post>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>For thermal-receipt-style continuous paper, the effective page height equals
    /// the bottom of the lowest primitive on the page plus the bottom margin.</summary>
    private static double ComputeContinuousHeightPt(RenderedPage page)
    {
        Unit maxBottom = Unit.Zero;
        foreach (var p in page.Primitives)
        {
            if (p.Bounds.Bottom > maxBottom)
            {
                maxBottom = p.Bounds.Bottom;
            }
        }
        return (maxBottom + page.PageSetup.Margins.Bottom).ToPoints();
    }
}
