using FluentAssertions;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Rendering;
using Reporting.Rendering.Skia;
using Reporting.Styling;
using SkiaSharp;
using Xunit;

namespace Reporting.Rendering.Tests;

/// <summary>
/// Geometric smoke tests for the Skia primitive renderer, driven entirely through the public
/// <see cref="SkiaRenderingContext"/> API and validated by counting ink pixels (see
/// <see cref="SkiaTestHelpers"/>). Everything here uses solid blocks / paths / images — never
/// text — so the assertions are deterministic across OS and CI (font metrics vary by host).
/// </summary>
public class SkiaRenderPrimitivesTests
{
    private const float Dpi = 96f;

    /// <summary>Maps a page-unit rectangle to its pixel bounds at <see cref="Dpi"/> for region scans.</summary>
    private static SKRectI PxRect(Rectangle r)
        => SKRectI.Create(
            (int)Math.Round(r.X.ToPixels(Dpi)),
            (int)Math.Round(r.Y.ToPixels(Dpi)),
            (int)Math.Round(r.Width.ToPixels(Dpi)),
            (int)Math.Round(r.Height.ToPixels(Dpi)));

    // ---- 4. Nested rectangular clip -------------------------------------------------------

    [Fact]
    public void Nested_clip_confines_ink_to_inner_region()
    {
        // Arrange — a small page so the scan is cheap; clip to an inner rect, then fill a
        // much larger black rectangle. Only the clipped sub-area must receive ink.
        var page = PageSetup.A4Portrait with
        {
            Paper = new PaperSize("Small", Unit.FromMm(40), Unit.FromMm(40)),
            Margins = new Thickness(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero),
        };
        var clip = new Rectangle(10.Mm(), 10.Mm(), 10.Mm(), 10.Mm());
        var bigFill = new Rectangle(0.Mm(), 0.Mm(), 40.Mm(), 40.Mm());

        using var ctx = new SkiaRenderingContext(dpi: Dpi);
        ctx.BeginPage(page);
        // Outer clip (whole page) then an inner, smaller clip — exercises nested Save/Restore.
        ctx.PushClip(bigFill, Unit.Zero);
        ctx.PushClip(clip, Unit.Zero);
        ctx.DrawRectangle(bigFill, pen: null, fill: new BrushStyle(Color.Black));
        ctx.PopClip();
        ctx.PopClip();

        // Act
        ctx.EndPage();
        using var bmp = SKBitmap.Decode(ctx.Pages[0].PngBytes);
        var insideClip = SkiaTestHelpers.CountInkPixels(bmp, PxRect(clip));
        var wholePage = SkiaTestHelpers.CountInkPixels(bmp);

        // Assert — ink fills (essentially) the whole inner clip and nothing escapes it.
        var clipPx = PxRect(clip);
        int clipArea = clipPx.Width * clipPx.Height;
        insideClip.Should().BeGreaterThan((int)(clipArea * 0.9),
            "the fill should cover almost the entire clipped region");
        wholePage.Should().BeLessThanOrEqualTo((int)(clipArea * 1.05),
            "no ink may land outside the inner clip — Save/RestoreToCount must pair correctly");
    }

    [Fact]
    public void Pop_clip_restores_full_canvas_for_subsequent_draws()
    {
        // Arrange — draw inside a clip, pop it, then draw outside the old clip. The second
        // draw must land (proving PopClip truly restored the canvas).
        var page = PageSetup.A4Portrait with
        {
            Paper = new PaperSize("Small", Unit.FromMm(40), Unit.FromMm(40)),
            Margins = new Thickness(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero),
        };
        var clip = new Rectangle(0.Mm(), 0.Mm(), 10.Mm(), 10.Mm());
        var outside = new Rectangle(20.Mm(), 20.Mm(), 10.Mm(), 10.Mm());

        using var ctx = new SkiaRenderingContext(dpi: Dpi);
        ctx.BeginPage(page);
        ctx.PushClip(clip, Unit.Zero);
        ctx.DrawRectangle(clip, pen: null, fill: new BrushStyle(Color.Black));
        ctx.PopClip();
        ctx.DrawRectangle(outside, pen: null, fill: new BrushStyle(Color.Black));

        // Act
        ctx.EndPage();
        using var bmp = SKBitmap.Decode(ctx.Pages[0].PngBytes);
        var outsideInk = SkiaTestHelpers.CountInkPixels(bmp, PxRect(outside));

        // Assert
        outsideInk.Should().BeGreaterThan(0, "drawing after PopClip must not be clipped away");
    }

    // ---- 5. DrawImage sizing modes --------------------------------------------------------

    [Theory]
    [InlineData(ImageSizing.Stretch)]
    [InlineData(ImageSizing.Fit)]
    [InlineData(ImageSizing.Fill)]
    [InlineData(ImageSizing.Native)]
    public void DrawImage_sizing_modes_do_not_throw_and_paint_ink(ImageSizing sizing)
    {
        // Arrange — a 2x2 solid red image into a non-square rectangle.
        var png = SkiaTestHelpers.SolidColorPng(2, 2, new SKColor(255, 0, 0));
        var bounds = new Rectangle(5.Mm(), 5.Mm(), 60.Mm(), 20.Mm()); // wide, non-square

        using var ctx = new SkiaRenderingContext(dpi: Dpi);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawImage(png, bounds, sizing);

        // Act
        ctx.EndPage();
        using var bmp = SKBitmap.Decode(ctx.Pages[0].PngBytes);
        var ink = SkiaTestHelpers.CountInkPixels(bmp, PxRect(bounds));

        // Assert
        ink.Should().BeGreaterThan(0, "a solid-colour image must paint visible pixels");
    }

    [Fact]
    public void DrawImage_stretch_fills_more_of_bounds_than_fit()
    {
        // Arrange — a square 2x2 image drawn into a WIDE rectangle. Stretch distorts to fill
        // the whole box; Fit preserves the square aspect and leaves white side-bands. So the
        // coloured-pixel coverage inside the bounds must be strictly larger for Stretch.
        var png = SkiaTestHelpers.SolidColorPng(2, 2, new SKColor(0, 0, 255));
        var bounds = new Rectangle(5.Mm(), 5.Mm(), 80.Mm(), 16.Mm()); // 5:1 aspect — exaggerates the gap

        int Coverage(ImageSizing sizing)
        {
            using var ctx = new SkiaRenderingContext(dpi: Dpi);
            ctx.BeginPage(PageSetup.A4Portrait);
            ctx.DrawImage(png, bounds, sizing);
            ctx.EndPage();
            using var bmp = SKBitmap.Decode(ctx.Pages[0].PngBytes);
            return SkiaTestHelpers.CountInkPixels(bmp, PxRect(bounds));
        }

        // Act
        var stretch = Coverage(ImageSizing.Stretch);
        var fit = Coverage(ImageSizing.Fit);

        // Assert — Fit letterboxes (white bands) so it covers clearly less than Stretch.
        stretch.Should().BeGreaterThan(fit,
            "Stretch fills the whole non-square box while Fit leaves white letterbox bands");
        // And Fit must still leave a meaningful white margin inside the (very wide) bounds.
        var boundsPx = PxRect(bounds);
        int boundsArea = boundsPx.Width * boundsPx.Height;
        fit.Should().BeLessThan((int)(boundsArea * 0.6),
            "Fit on a 5:1 box should leave large white side-bands");
    }

    // ---- 6. DrawImage robustness with corrupted bytes -------------------------------------

    [Fact]
    public void DrawImage_with_corrupted_bytes_is_silently_ignored_and_page_still_valid()
    {
        // Arrange — bytes that are not a decodable image; SKImage.FromEncodedData returns null
        // and DrawImage must return silently (no throw, no ink).
        var garbage = new byte[] { 1, 2, 3 };
        var bounds = new Rectangle(5.Mm(), 5.Mm(), 30.Mm(), 30.Mm());

        using var ctx = new SkiaRenderingContext(dpi: Dpi);
        ctx.BeginPage(PageSetup.A4Portrait);

        // Act
        var draw = () => ctx.DrawImage(garbage, bounds, ImageSizing.Fit);
        draw.Should().NotThrow("undecodable image data must be ignored, not crash the page");
        ctx.EndPage();

        // Assert — a valid PNG page is still produced.
        ctx.Pages.Should().HaveCount(1);
        ctx.Pages[0].PngBytes.AsSpan(0, 4).ToArray().Should().Equal([0x89, 0x50, 0x4E, 0x47]);
        SkiaTestHelpers.CountInkPixels(ctx.Pages[0].PngBytes, PxRect(bounds))
            .Should().Be(0, "nothing should be drawn for undecodable image data");
    }

    // ---- 7. SkiaPathBuilder via DrawPath --------------------------------------------------

    [Fact]
    public void DrawPath_with_all_segment_kinds_paints_within_expected_bounds()
    {
        // Arrange — a closed path using MoveTo/LineTo/CubicTo/QuadraticTo, filled solid. We
        // assert ink lands inside the path's bounding box and the surrounding margin stays white,
        // which also exercises DPI scaling in ToSKPoint.
        var page = PageSetup.A4Portrait with
        {
            Paper = new PaperSize("Small", Unit.FromMm(50), Unit.FromMm(50)),
            Margins = new Thickness(Unit.Zero, Unit.Zero, Unit.Zero, Unit.Zero),
        };
        // Keep all points inside [10mm,40mm] on both axes → bounding box is well within the page.
        var bbox = new Rectangle(10.Mm(), 10.Mm(), 30.Mm(), 30.Mm());

        using var ctx = new SkiaRenderingContext(dpi: Dpi);
        ctx.BeginPage(page);
        ctx.DrawPath(
            p => p.MoveTo(new Point(10.Mm(), 10.Mm()))
                  .LineTo(new Point(40.Mm(), 10.Mm()))
                  .CubicTo(new Point(40.Mm(), 25.Mm()), new Point(30.Mm(), 40.Mm()), new Point(25.Mm(), 40.Mm()))
                  .QuadraticTo(new Point(10.Mm(), 40.Mm()), new Point(10.Mm(), 25.Mm()))
                  .Close(),
            pen: null,
            fill: new BrushStyle(Color.Black));

        // Act
        ctx.EndPage();
        using var bmp = SKBitmap.Decode(ctx.Pages[0].PngBytes);
        var insideBbox = SkiaTestHelpers.CountInkPixels(bmp, PxRect(bbox));
        var wholePage = SkiaTestHelpers.CountInkPixels(bmp);
        // Inflate the bbox by a couple of pixels to absorb anti-aliasing bleed at the path edges
        // (the path's extreme points sit exactly on the bbox boundary, so AA spills ~1px).
        var inflated = PxRect(bbox);
        inflated.Inflate(2, 2);
        var insideInflated = SkiaTestHelpers.CountInkPixels(bmp, inflated);

        // Assert — substantial ink in the bbox, and none outside the (slightly inflated) bbox.
        insideBbox.Should().BeGreaterThan(500, "the filled path must paint a sizeable area");
        insideInflated.Should().Be(wholePage,
            "no ink may fall outside the path's bounding box (allowing a 2px anti-alias margin)");
    }

    // ---- 8. Invisible pen -----------------------------------------------------------------

    [Fact]
    public void Invisible_pen_draws_no_outline()
    {
        // Arrange — an invisible pen (zero thickness ⇒ IsVisible == false) with no fill must
        // leave the page blank, identical to never drawing anything.
        var bounds = new Rectangle(5.Mm(), 5.Mm(), 40.Mm(), 20.Mm());
        var invisiblePen = new PenStyle(Color.Black, Unit.Zero);
        invisiblePen.IsVisible.Should().BeFalse();

        using var ctx = new SkiaRenderingContext(dpi: Dpi);
        ctx.BeginPage(PageSetup.A4Portrait);
        ctx.DrawRectangle(bounds, pen: invisiblePen, fill: null);

        // Act
        ctx.EndPage();
        var ink = SkiaTestHelpers.CountInkPixels(ctx.Pages[0].PngBytes);

        // Assert
        ink.Should().Be(0, "an invisible pen with no fill must produce no ink");
    }

    [Fact]
    public void Pen_plus_brush_produces_more_ink_than_brush_alone()
    {
        // Arrange — same rectangle, once filled only, once filled + outlined. The outline adds
        // a stroked border, so total ink must increase.
        var bounds = new Rectangle(5.Mm(), 5.Mm(), 40.Mm(), 20.Mm());
        var fill = new BrushStyle(Color.LightGray);
        var pen = new PenStyle(Color.Black, 2.Pt()); // clearly visible stroke

        int Ink(PenStyle? p, BrushStyle? f)
        {
            using var ctx = new SkiaRenderingContext(dpi: Dpi);
            ctx.BeginPage(PageSetup.A4Portrait);
            ctx.DrawRectangle(bounds, p, f);
            ctx.EndPage();
            return SkiaTestHelpers.CountInkPixels(ctx.Pages[0].PngBytes);
        }

        // Act
        var brushOnly = Ink(null, fill);
        var penAndBrush = Ink(pen, fill);

        // Assert
        brushOnly.Should().BeGreaterThan(0, "the fill itself paints the interior");
        penAndBrush.Should().BeGreaterThan(brushOnly, "the stroked outline adds ink on top of the fill");
    }
}
