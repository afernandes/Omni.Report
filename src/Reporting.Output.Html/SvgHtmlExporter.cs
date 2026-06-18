using System.Globalization;
using System.Net;
using System.Text;
using Reporting.Layout;
using Reporting.Output.Pdf;
using Reporting.Output.Svg;

namespace Reporting.Output.Html;

/// <summary>
/// Wraps the vector <see cref="SvgExporter"/> in a self-contained HTML document. Each page
/// becomes a <c>&lt;section class="page"&gt;</c> sized in physical millimetres and containing
/// the page's <c>&lt;svg&gt;</c> fragment. Text in the SVG remains selectable; CSS
/// <c>@media print</c> rules size the browser print preview to the report's page setup so
/// Ctrl+P produces a page-for-page PDF without rescaling.
/// </summary>
/// <remarks>
/// <para>This exporter is intentionally thin — the heavy lifting (Skia primitive replay,
/// <c>viewBox</c> injection, multi-page composition) lives in <see cref="SvgExporter"/>.
/// Swap the inner SVG exporter and you swap the wire format; the HTML chrome stays the
/// same.</para>
/// </remarks>
public sealed class SvgHtmlExporter : IReportExporter
{
    private readonly HtmlExportOptions _options;
    private readonly SvgExporter _svg;

    public SvgHtmlExporter(HtmlExportOptions? options = null, SvgExporter? svgExporter = null)
    {
        _options = options ?? HtmlExportOptions.Default;
        _svg = svgExporter ?? new SvgExporter(new SvgExportOptions
        {
            PageBackgroundColor = _options.PageBackground,
            IncludeBackground = true,
        });
    }

    public string Format => "html";
    public string FileExtension => ".html";
    public string ContentType => "text/html; charset=utf-8";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var title = _options.Title ?? report.Name;

        // Render each page to a self-contained SVG fragment via the SVG exporter. The
        // fragment already carries viewBox + width="100%" / height="100%", so embedding is
        // just dropping it into a CSS-sized container.
        var pages = new List<SvgPageFragment>(report.Pages.Count);
        foreach (var page in report.Pages)
        {
            pages.Add(_svg.RenderPage(page));
        }

        // Print rule uses the first page's size — mixed-size reports still render fine on
        // screen (each <svg> carries its own intrinsic dimensions), but @page is per-doc.
        var firstWidthMm = pages.Count > 0 ? PointsToMm(pages[0].WidthPt) : 210.0;
        var firstHeightMm = pages.Count > 0 ? PointsToMm(pages[0].HeightPt) : 297.0;

        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 8192, leaveOpen: true);
        writer.NewLine = "\n";

        writer.Write("<!doctype html>\n<html lang=\"");
        writer.Write(WebUtility.HtmlEncode(_options.Language));
        writer.Write("\">\n<head>\n  <meta charset=\"utf-8\">\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n  <title>");
        writer.Write(WebUtility.HtmlEncode(title));
        writer.Write("</title>\n  <style>");
        writer.Write(BuildStyles(firstWidthMm, firstHeightMm));
        writer.Write("</style>\n</head>\n<body>\n  <main class=\"report\" aria-label=\"");
        writer.Write(WebUtility.HtmlEncode(title));
        writer.Write("\">\n");

        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            var widthMm = PointsToMm(p.WidthPt).ToString("0.###", CultureInfo.InvariantCulture);
            var heightMm = PointsToMm(p.HeightPt).ToString("0.###", CultureInfo.InvariantCulture);
            writer.Write("    <section class=\"page\" role=\"document\" aria-label=\"Página ");
            writer.Write((i + 1).ToString(CultureInfo.InvariantCulture));
            writer.Write(" de ");
            writer.Write(pages.Count.ToString(CultureInfo.InvariantCulture));
            writer.Write("\" style=\"width:");
            writer.Write(widthMm);
            writer.Write("mm;height:");
            writer.Write(heightMm);
            writer.Write("mm;\">\n      ");
            writer.Write(p.SvgMarkup);
            writer.Write("\n    </section>\n");
        }

        writer.Write("  </main>\n</body>\n</html>\n");
        writer.Flush();
    }

    private string BuildStyles(double firstWidthMm, double firstHeightMm)
    {
        var sb = new StringBuilder(1024);
        sb.Append("html,body{margin:0;padding:0;}");
        sb.Append("body{background:").Append(_options.BodyBackground)
          .Append(";font-family:system-ui,-apple-system,'Segoe UI',sans-serif;color:#1F2937;}");
        sb.Append(".report{display:flex;flex-direction:column;align-items:center;gap:16px;padding:24px;}");
        sb.Append(".page{display:block;background:").Append(_options.PageBackground)
          .Append(";box-sizing:border-box;");
        if (_options.DropShadow)
        {
            sb.Append("box-shadow:0 2px 8px rgba(0,0,0,.08),0 0 0 1px rgba(0,0,0,.04);");
        }
        sb.Append("}");
        sb.Append(".page svg{display:block;width:100%;height:100%;}");

        if (_options.EmitPrintRules)
        {
            sb.Append("@media print{");
            sb.Append("body{background:#fff;}");
            sb.Append(".report{gap:0;padding:0;}");
            sb.Append(".page{box-shadow:none;page-break-after:always;break-after:page;}");
            sb.Append(".page:last-child{page-break-after:auto;break-after:auto;}");
            sb.Append("@page{size:")
              .Append(firstWidthMm.ToString("0.###", CultureInfo.InvariantCulture))
              .Append("mm ")
              .Append(firstHeightMm.ToString("0.###", CultureInfo.InvariantCulture))
              .Append("mm;margin:0;}");
            sb.Append("}");
        }
        return sb.ToString();
    }

    private static double PointsToMm(double pt) => pt * 25.4 / 72.0;
}
