using SkiaSharp;
using Reporting.Geometry;
using Reporting.Layout;
using Reporting.Layout.Primitives;
using Reporting.Rendering.Skia;

namespace Reporting.Output.Pdf;

/// <summary>
/// Vector-native PDF exporter using SkiaSharp's <see cref="SKDocument"/>. Replays each
/// <see cref="LayoutPrimitive"/> directly onto the PDF page's <see cref="SKCanvas"/> — text
/// stays selectable and shapes remain vector, unlike <c>SkiaRenderingContext.ToPdfBytes()</c>
/// which rasterizes each page first.
/// </summary>
public sealed class SkiaPdfExporter : IReportExporter
{
    /// <summary>PDFs measure coordinates in PostScript points (1pt = 1/72 inch).</summary>
    public const float PdfDpi = 72f;

    private readonly PdfExportOptions _options;

    public SkiaPdfExporter(PdfExportOptions? options = null)
    {
        _options = options ?? PdfExportOptions.Default;
    }

    public string Format => "pdf";
    public string FileExtension => ".pdf";
    public string ContentType => "application/pdf";

    public void Export(RenderedReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var metadata = new SKDocumentPdfMetadata
        {
            Title = _options.Title ?? report.Name,
            Author = _options.Author ?? string.Empty,
            Subject = _options.Subject ?? string.Empty,
            Keywords = _options.Keywords ?? string.Empty,
            Producer = _options.Producer,
            Creator = _options.Creator ?? _options.Producer,
            Creation = _options.CreationDate ?? DateTime.Now,
            Modified = _options.CreationDate ?? DateTime.Now,
        };

        using var document = SKDocument.CreatePdf(output, metadata);
        if (document is null)
        {
            throw new InvalidOperationException("Failed to create a PDF document — Skia returned null.");
        }

        foreach (var page in report.Pages)
        {
            var widthPt = page.PageSetup.PageWidth.ToPoints();
            var heightPt = page.PageSetup.IsContinuous
                ? Math.Max((double)page.PageSetup.Margins.Top.ToPoints() + 1, ComputeContinuousHeightPt(page))
                : page.PageSetup.PageHeight.ToPoints();

            using var canvas = document.BeginPage((float)widthPt, (float)heightPt);
            canvas.Clear(SKColors.White);
            foreach (var primitive in page.Primitives)
            {
                Replay(canvas, primitive);
            }
            document.EndPage();
        }
        document.Close();
    }

    private static void Replay(SKCanvas canvas, LayoutPrimitive primitive)
    {
        switch (primitive)
        {
            case DrawTextPrimitive t:
                SkiaPrimitiveRenderer.DrawText(canvas, t.Text, t.Bounds, t.Style, PdfDpi);
                break;
            case DrawLinePrimitive l:
                SkiaPrimitiveRenderer.DrawLine(canvas, l.From, l.To, l.Pen, PdfDpi);
                break;
            case DrawRectanglePrimitive r:
                SkiaPrimitiveRenderer.DrawRectangle(canvas, r.Bounds, r.Pen, r.Fill, PdfDpi);
                break;
            case DrawEllipsePrimitive e:
                SkiaPrimitiveRenderer.DrawEllipse(canvas, e.Bounds, e.Pen, e.Fill, PdfDpi);
                break;
            case DrawImagePrimitive i:
                if (i.Data.Count > 0)
                {
                    var copy = new byte[i.Data.Count];
                    for (int k = 0; k < copy.Length; k++)
                    {
                        copy[k] = i.Data[k];
                    }
                    SkiaPrimitiveRenderer.DrawImage(canvas, copy, i.Bounds, PdfDpi, i.Sizing);
                }
                break;
            case DrawPolygonPrimitive poly:
                SkiaPrimitiveRenderer.DrawPath(canvas, poly.BuildPath, poly.Pen, poly.Fill, PdfDpi);
                break;
        }
    }

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
