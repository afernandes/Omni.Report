using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Styling;

namespace Reporting.Samples.CodeFirst.Reports;

/// <summary>
/// Sample 05 — <b>Canvas low-level</b>. Demonstrates drawing a report by talking directly to
/// <see cref="IRenderingContext"/> without going through the fluent CodeFirst builder, the
/// domain model (<c>ReportDefinition</c>), or the layout/paginator. Useful when:
/// <list type="bullet">
///   <item>You need pixel-perfect control over a custom artefact (a certificate, a custom
///         invoice template, a barcode label) that doesn't fit the banded report metaphor.</item>
///   <item>You're building a one-off whose layout is procedurally computed (e.g. seating
///         charts, calendars, schematics) and the banded model would force awkward gymnastics.</item>
///   <item>You're embedding rendering as a primitive inside another renderer (e.g. shipping
///         labels emitted from a barcode service).</item>
/// </list>
///
/// <para>Interfaces touched:</para>
/// <list type="bullet">
///   <item><see cref="IRenderingContext"/> — owns the page lifecycle (BeginPage / EndPage)
///         and the primitive draw calls (DrawText, DrawLine, DrawRectangle, DrawEllipse,
///         DrawImage, DrawPath).</item>
///   <item><see cref="ITextMeasurer"/> — same surface implements this; we use it to right-
///         align number columns by measuring text width before placing it.</item>
///   <item><see cref="IPathBuilder"/> — handed to the DrawPath callback to compose vector
///         paths via MoveTo / LineTo / CubicTo / Arc / Close.</item>
/// </list>
///
/// <para>Output: drives a <see cref="SkiaRenderingContext"/> twice (two pages) and returns
/// the surface so callers can dump PNG-per-page or compose a PDF.</para>
/// </summary>
public static class Sample05_CanvasLowLevel
{
    /// <summary>Renders the demonstration directly into <paramref name="ctx"/>.
    /// Pass any <see cref="IRenderingContext"/> implementation (Skia, GDI, future PDF
    /// backends) — the sample is renderer-agnostic.</summary>
    public static void Render(IRenderingContext ctx, ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(measurer);

        // ─── Page 1 — "Certificate of Conformity" layout ─────────────────────────
        // A4 portrait with comfortable 20mm margins. The page size is the only thing
        // the engine knows about — every primitive below is positioned in mm with
        // explicit coordinates.
        var pageSetup = PageSetup.A4Portrait;
        ctx.BeginPage(pageSetup);

        var pageW = pageSetup.PageWidth;   // 210mm
        var margin = Unit.FromMm(20);

        // Decorative double border around the page
        DrawCertificateBorder(ctx, pageSetup, margin);

        // Centered title — measure it first so we can center precisely
        var titleStyle = new TextStyle(
            new Font("Times New Roman", 28, FontStyle.Bold),
            Color.Black,
            HorizontalAlignment.Center);
        const string title = "CERTIFICADO";
        var titleSize = measurer.Measure(title, titleStyle);
        var titleY = Unit.FromMm(50);
        ctx.DrawText(
            title,
            new Rectangle((pageW - titleSize.Width) / 2, titleY, titleSize.Width, titleSize.Height),
            titleStyle);

        // Subtitle directly below
        var subStyle = new TextStyle(
            new Font("Times New Roman", 14, FontStyle.Italic),
            Color.Gray,
            HorizontalAlignment.Center);
        const string subtitle = "Conclusão de Curso";
        ctx.DrawText(
            subtitle,
            new Rectangle(margin, titleY + titleSize.Height + Unit.FromMm(2),
                          pageW - margin * 2, Unit.FromMm(8)),
            subStyle);

        // Horizontal ornament — a thin gold line with two diamond endpoints rendered via
        // DrawPath (showcases the path builder API).
        DrawOrnament(ctx, midX: pageW / 2, y: Unit.FromMm(78), span: Unit.FromMm(80));

        // Body paragraph — wrapped to the content area; the renderer does word-wrap.
        var bodyStyle = new TextStyle(
            new Font("Georgia", 12),
            Color.Black,
            HorizontalAlignment.Center,
            VerticalAlignment.Top,
            WordWrap: true);
        const string body =
            "Certificamos que ANDERSON FERNANDES concluiu com aproveitamento o curso de " +
            "DESIGN DE RELATÓRIOS BANDADOS, com carga horária total de 40 horas, " +
            "atendendo aos requisitos estabelecidos pelo regulamento da instituição.";
        ctx.DrawText(
            body,
            new Rectangle(margin + Unit.FromMm(15), Unit.FromMm(95),
                          pageW - margin * 2 - Unit.FromMm(30), Unit.FromMm(40)),
            bodyStyle);

        // Two signature lines side-by-side at the bottom, with names below — text right-
        // aligned to its own bounding box (the renderer handles alignment).
        DrawSignatureLines(ctx, pageSetup, measurer);

        ctx.EndPage();

        // ─── Page 2 — "Tabular invoice" layout (computed column positions) ──────
        ctx.BeginPage(pageSetup);

        // Header band — colored rectangle + white text
        ctx.DrawRectangle(
            new Rectangle(margin, margin, pageW - margin * 2, Unit.FromMm(15)),
            pen: null,
            fill: new BrushStyle(Color.FromRgb(0xC2, 0x41, 0x0C))); // accent orange
        ctx.DrawText(
            "FATURA Nº 2026/0042",
            new Rectangle(margin + Unit.FromMm(4), margin + Unit.FromMm(3),
                          pageW - margin * 2 - Unit.FromMm(8), Unit.FromMm(9)),
            new TextStyle(new Font("Arial", 16, FontStyle.Bold), Color.White));

        DrawInvoiceTable(ctx, measurer, top: margin + Unit.FromMm(25),
                         left: margin, right: pageW - margin);

        // Page footer — left side = date, right side = page number (measured for right-align)
        var footerStyle = new TextStyle(new Font("Arial", 9), Color.Gray);
        var pageNo = "Página 2 de 2";
        var pageNoSize = measurer.Measure(pageNo, footerStyle);
        ctx.DrawText("Emitido em 25/05/2026",
            new Rectangle(margin, pageSetup.PageHeight - margin, Unit.FromMm(60), Unit.FromMm(5)),
            footerStyle);
        ctx.DrawText(pageNo,
            new Rectangle(pageW - margin - pageNoSize.Width, pageSetup.PageHeight - margin,
                          pageNoSize.Width, Unit.FromMm(5)),
            footerStyle);

        ctx.EndPage();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────
    private static void DrawCertificateBorder(IRenderingContext ctx, PageSetup setup, Unit margin)
    {
        var pageW = setup.PageWidth;
        var pageH = setup.PageHeight;
        var inset = Unit.FromMm(3);

        // Outer thick line
        ctx.DrawRectangle(
            new Rectangle(margin, margin, pageW - margin * 2, pageH - margin * 2),
            new PenStyle(Color.FromRgb(0x96, 0x6C, 0x29), Unit.FromPoint(1.5)),
            fill: null);

        // Inner thin line, 3mm inside
        ctx.DrawRectangle(
            new Rectangle(margin + inset, margin + inset,
                          pageW - (margin + inset) * 2, pageH - (margin + inset) * 2),
            new PenStyle(Color.FromRgb(0x96, 0x6C, 0x29), Unit.FromPoint(0.5)),
            fill: null);
    }

    private static void DrawOrnament(IRenderingContext ctx, Unit midX, Unit y, Unit span)
    {
        var half = span / 2;
        var gold = new PenStyle(Color.FromRgb(0x96, 0x6C, 0x29), Unit.FromPoint(0.7));
        var goldFill = new BrushStyle(Color.FromRgb(0x96, 0x6C, 0x29));

        // Central straight line with a small gap in the middle for the diamond
        ctx.DrawPath(b =>
        {
            b.MoveTo(new Point(midX - half, y))
             .LineTo(new Point(midX - Unit.FromMm(3), y));
        }, gold, fill: null);
        ctx.DrawPath(b =>
        {
            b.MoveTo(new Point(midX + Unit.FromMm(3), y))
             .LineTo(new Point(midX + half, y));
        }, gold, fill: null);

        // Centre diamond (filled). Showcases that paths can be both stroked and filled.
        ctx.DrawPath(b =>
        {
            b.MoveTo(new Point(midX, y - Unit.FromMm(2)))
             .LineTo(new Point(midX + Unit.FromMm(2.5), y))
             .LineTo(new Point(midX, y + Unit.FromMm(2)))
             .LineTo(new Point(midX - Unit.FromMm(2.5), y))
             .Close();
        }, gold, goldFill);
    }

    private static void DrawSignatureLines(IRenderingContext ctx, PageSetup setup, ITextMeasurer m)
    {
        var pageW = setup.PageWidth;
        var lineY = Unit.FromMm(165);
        var lineW = Unit.FromMm(70);
        var leftX  = Unit.FromMm(30);
        var rightX = pageW - leftX - lineW;
        var pen = new PenStyle(Color.Black, Unit.FromPoint(0.5));

        ctx.DrawLine(new Point(leftX,  lineY), new Point(leftX  + lineW, lineY), pen);
        ctx.DrawLine(new Point(rightX, lineY), new Point(rightX + lineW, lineY), pen);

        var nameStyle = new TextStyle(
            new Font("Arial", 10, FontStyle.Bold),
            Color.Black,
            HorizontalAlignment.Center);
        var titleStyle = new TextStyle(new Font("Arial", 9), Color.Gray, HorizontalAlignment.Center);

        ctx.DrawText("Maria Silva",
            new Rectangle(leftX, lineY + Unit.FromMm(2), lineW, Unit.FromMm(5)), nameStyle);
        ctx.DrawText("Coordenadora do Curso",
            new Rectangle(leftX, lineY + Unit.FromMm(7), lineW, Unit.FromMm(5)), titleStyle);

        ctx.DrawText("João Pereira",
            new Rectangle(rightX, lineY + Unit.FromMm(2), lineW, Unit.FromMm(5)), nameStyle);
        ctx.DrawText("Diretor Geral",
            new Rectangle(rightX, lineY + Unit.FromMm(7), lineW, Unit.FromMm(5)), titleStyle);
    }

    private static void DrawInvoiceTable(IRenderingContext ctx, ITextMeasurer m,
                                         Unit top, Unit left, Unit right)
    {
        // 4 columns: description (flex), quantity (20mm), unit (28mm), total (32mm).
        // Total width is computed by laying right-edges from right to left so the table
        // always aligns to `right` regardless of paper size.
        var totalRight = right;
        var totalLeft  = right - Unit.FromMm(32);
        var unitRight  = totalLeft;
        var unitLeft   = unitRight - Unit.FromMm(28);
        var qtyRight   = unitLeft;
        var qtyLeft    = qtyRight - Unit.FromMm(20);
        var descLeft   = left;
        var descRight  = qtyLeft;

        var rowHeight  = Unit.FromMm(8);
        var headerStyle = new TextStyle(new Font("Arial", 10, FontStyle.Bold), Color.White);
        var cellStyle   = new TextStyle(new Font("Arial", 10), Color.Black);
        var numStyle    = new TextStyle(new Font("Arial", 10), Color.Black,
                                        HorizontalAlignment.Right);
        var headerFill  = new BrushStyle(Color.FromRgb(0x33, 0x33, 0x33));
        var rowStrokeFx = new PenStyle(Color.LightGray, Unit.FromPoint(0.3));

        // Header row
        ctx.DrawRectangle(
            new Rectangle(left, top, right - left, rowHeight),
            pen: null, fill: headerFill);
        ctx.DrawText("Descrição",
            new Rectangle(descLeft + Unit.FromMm(2), top + Unit.FromMm(1),
                          descRight - descLeft - Unit.FromMm(2), rowHeight),
            headerStyle);
        ctx.DrawText("Qtd",
            new Rectangle(qtyLeft, top + Unit.FromMm(1), qtyRight - qtyLeft - Unit.FromMm(2),
                          rowHeight),
            headerStyle with { HorizontalAlignment = HorizontalAlignment.Right });
        ctx.DrawText("Unitário",
            new Rectangle(unitLeft, top + Unit.FromMm(1), unitRight - unitLeft - Unit.FromMm(2),
                          rowHeight),
            headerStyle with { HorizontalAlignment = HorizontalAlignment.Right });
        ctx.DrawText("Total",
            new Rectangle(totalLeft, top + Unit.FromMm(1), totalRight - totalLeft - Unit.FromMm(2),
                          rowHeight),
            headerStyle with { HorizontalAlignment = HorizontalAlignment.Right });

        // Data rows (hard-coded for the demo; real callers would loop their own data here)
        var rows = new (string Desc, int Qty, decimal Unit, decimal Total)[]
        {
            ("Farinha de Trigo 25kg", 12, 148.90m, 1786.80m),
            ("Açúcar Refinado 1kg",   48,   4.20m,  201.60m),
            ("Fermento Biológico 100g", 6, 11.75m,   70.50m),
            ("Embalagem PE 5kg",      30,   0.95m,   28.50m),
        };

        var rowY = top + rowHeight;
        foreach (var r in rows)
        {
            ctx.DrawLine(
                new Point(left, rowY + rowHeight),
                new Point(right, rowY + rowHeight),
                rowStrokeFx);
            ctx.DrawText(r.Desc,
                new Rectangle(descLeft + Unit.FromMm(2), rowY + Unit.FromMm(2),
                              descRight - descLeft - Unit.FromMm(2), rowHeight),
                cellStyle);
            ctx.DrawText(r.Qty.ToString(),
                new Rectangle(qtyLeft, rowY + Unit.FromMm(2),
                              qtyRight - qtyLeft - Unit.FromMm(2), rowHeight),
                numStyle);
            ctx.DrawText(r.Unit.ToString("C"),
                new Rectangle(unitLeft, rowY + Unit.FromMm(2),
                              unitRight - unitLeft - Unit.FromMm(2), rowHeight),
                numStyle);
            ctx.DrawText(r.Total.ToString("C"),
                new Rectangle(totalLeft, rowY + Unit.FromMm(2),
                              totalRight - totalLeft - Unit.FromMm(2), rowHeight),
                numStyle);
            rowY += rowHeight;
        }

        // Total row — bold, with a top border
        var grandTotal = rows.Sum(r => r.Total);
        ctx.DrawLine(new Point(unitLeft, rowY + Unit.FromMm(1)),
                     new Point(totalRight, rowY + Unit.FromMm(1)),
                     new PenStyle(Color.Black, Unit.FromPoint(0.7)));
        ctx.DrawText("TOTAL",
            new Rectangle(unitLeft, rowY + Unit.FromMm(3),
                          unitRight - unitLeft - Unit.FromMm(2), rowHeight),
            new TextStyle(new Font("Arial", 11, FontStyle.Bold), Color.Black,
                          HorizontalAlignment.Right));
        ctx.DrawText(grandTotal.ToString("C"),
            new Rectangle(totalLeft, rowY + Unit.FromMm(3),
                          totalRight - totalLeft - Unit.FromMm(2), rowHeight),
            new TextStyle(new Font("Arial", 11, FontStyle.Bold), Color.Black,
                          HorizontalAlignment.Right));
    }
}
