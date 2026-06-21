using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Reporting.Paper;
using Reporting.Styling;
using Reporting.Geometry;

// Disambiguate primitives that collide with Reporting.Geometry / Reporting.Styling.
using GdiBitmap = System.Drawing.Bitmap;
using GdiBrush = System.Drawing.SolidBrush;
using GdiFont = System.Drawing.Font;
using GdiGraphics = System.Drawing.Graphics;
using GdiPen = System.Drawing.Pen;
using GdiSize = System.Drawing.SizeF;
using ReportingFont = Reporting.Styling.Font;
using ReportingPoint = Reporting.Geometry.Point;
using ReportingRectangle = Reporting.Geometry.Rectangle;
using ReportingSize = Reporting.Geometry.Size;

namespace Reporting.Rendering.Gdi;

/// <summary>
/// <see cref="IRenderingContext"/> backed by a GDI+ <see cref="GdiGraphics"/> surface. Used
/// by the Windows spooler (one <see cref="GdiGraphics"/> per page comes from
/// <see cref="System.Drawing.Printing.PrintPageEventArgs"/>), but also runs against any
/// <see cref="GdiGraphics"/> source — <c>Graphics.FromImage(bitmap)</c> in tests, for example.
/// </summary>
/// <remarks>
/// Coordinates flow as mils → pixels at the device's DPI; the page's <see cref="GdiGraphics"/>
/// is left at <see cref="GraphicsUnit.Pixel"/> and we convert explicitly. Text is drawn via
/// <c>Graphics.DrawString</c> so it stays as text in the printer/XPS spool (vector,
/// not bitmap).
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class GdiRenderingContext : IRenderingContext, ITextMeasurer
{
    private readonly float _dpi;
    private readonly List<GdiBitmap> _pages = [];
    private GdiGraphics? _graphics;
    private bool _ownsGraphics;
    private PageSetup? _currentPage;

    /// <summary>Creates a context bound to an externally-owned <see cref="GdiGraphics"/>.
    /// Used by the print spooler — the caller (PrintDocument) owns the lifecycle.</summary>
    public GdiRenderingContext(GdiGraphics graphics, float? dpi = null)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        _graphics = graphics;
        _ownsGraphics = false;
        _dpi = dpi ?? graphics.DpiX;
        ConfigureGraphics(_graphics);
    }

    /// <summary>Creates a standalone context that owns a <see cref="GdiBitmap"/> per
    /// <see cref="BeginPage"/> call. Useful for headless rendering and unit tests.</summary>
    public GdiRenderingContext(float dpi = 96)
    {
        _dpi = dpi;
    }

    public IReadOnlyList<GdiBitmap> Pages => _pages;

    public void BeginPage(PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(pageSetup);
        _currentPage = pageSetup;
        if (_graphics is null)
        {
            int widthPx = (int)Math.Ceiling(pageSetup.PageWidth.Px(_dpi));
            int heightPx = pageSetup.IsContinuous
                ? Math.Max(1, widthPx)
                : (int)Math.Ceiling(pageSetup.PageHeight.Px(_dpi));
            var bitmap = new GdiBitmap(widthPx, heightPx);
            bitmap.SetResolution(_dpi, _dpi);
            _graphics = GdiGraphics.FromImage(bitmap);
            _graphics.Clear(System.Drawing.Color.White);
            _ownsGraphics = true;
            ConfigureGraphics(_graphics);
            _pages.Add(bitmap);
        }
    }

    public void EndPage()
    {
        if (_ownsGraphics && _graphics is not null)
        {
            _graphics.Flush();
            _graphics.Dispose();
            _graphics = null;
            _ownsGraphics = false;
        }
        _currentPage = null;
    }

    public void DrawText(string text, ReportingRectangle bounds, TextStyle style)
    {
        EnsureGraphics();
        ArgumentNullException.ThrowIfNull(style);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        using var font = CreateFont(style.Font);
        using var brush = new GdiBrush(style.ForeColor.ToGdiColor());
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = style.HorizontalAlignment.ToStringAlignment(),
            LineAlignment = style.VerticalAlignment.ToStringAlignment(),
            FormatFlags = style.WordWrap
                ? StringFormatFlags.LineLimit
                : StringFormatFlags.NoWrap | StringFormatFlags.LineLimit,
            Trimming = StringTrimming.None,
        };
        _graphics!.DrawString(text, font, brush, bounds.ToRectF(_dpi), format);
    }

    public void DrawLine(ReportingPoint from, ReportingPoint to, PenStyle pen)
    {
        EnsureGraphics();
        ArgumentNullException.ThrowIfNull(pen);
        if (!pen.IsVisible)
        {
            return;
        }
        using var gdiPen = CreatePen(pen);
        _graphics!.DrawLine(gdiPen, from.ToPointF(_dpi), to.ToPointF(_dpi));
    }

    public void DrawRectangle(ReportingRectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsureGraphics();
        var rect = bounds.ToRectF(_dpi);
        if (fill is not null && fill.IsVisible)
        {
            using var brush = new GdiBrush(fill.Color.ToGdiColor());
            _graphics!.FillRectangle(brush, rect);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var gdiPen = CreatePen(pen);
            _graphics!.DrawRectangle(gdiPen, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    public void DrawEllipse(ReportingRectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsureGraphics();
        var rect = bounds.ToRectF(_dpi);
        if (fill is not null && fill.IsVisible)
        {
            using var brush = new GdiBrush(fill.Color.ToGdiColor());
            _graphics!.FillEllipse(brush, rect);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var gdiPen = CreatePen(pen);
            _graphics!.DrawEllipse(gdiPen, rect);
        }
    }

    public void DrawImage(ReadOnlySpan<byte> imageData, ReportingRectangle bounds,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
    {
        EnsureGraphics();
        if (imageData.IsEmpty)
        {
            return;
        }
        var copy = imageData.ToArray();
        using var ms = new MemoryStream(copy);
        using var image = Image.FromStream(ms);
        var p = Reporting.Elements.ImageSizingMath.Compute(sizing, bounds, image.Width, image.Height);
        var dest = p.Dest.ToRectF(_dpi);
        var src = new System.Drawing.RectangleF(
            (float)(p.SrcX * image.Width), (float)(p.SrcY * image.Height),
            (float)(p.SrcW * image.Width), (float)(p.SrcH * image.Height));
        if (p.Clip)
        {
            var saved = _graphics!.Save();
            _graphics.SetClip(bounds.ToRectF(_dpi));
            _graphics.DrawImage(image, dest, src, System.Drawing.GraphicsUnit.Pixel);
            _graphics.Restore(saved);
        }
        else
        {
            _graphics!.DrawImage(image, dest, src, System.Drawing.GraphicsUnit.Pixel);
        }
    }

    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
    {
        EnsureGraphics();
        ArgumentNullException.ThrowIfNull(build);
        var builder = new GdiPathBuilder(_dpi);
        build(builder);
        using var path = builder.Path;
        if (fill is not null && fill.IsVisible)
        {
            using var brush = new GdiBrush(fill.Color.ToGdiColor());
            _graphics!.FillPath(brush, path);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var gdiPen = CreatePen(pen);
            _graphics!.DrawPath(gdiPen, path);
        }
    }

    public ReportingSize MeasureText(string text, TextStyle style, Unit? maxWidth = null)
        => Measure(text, style, maxWidth);

    public ReportingSize Measure(string text, TextStyle style, Unit? maxWidth = null)
    {
        ArgumentNullException.ThrowIfNull(style);
        var graphics = _graphics;
        GdiBitmap? scratch = null;
        if (graphics is null)
        {
            scratch = new GdiBitmap(1, 1);
            scratch.SetResolution(_dpi, _dpi);
            graphics = GdiGraphics.FromImage(scratch);
        }
        try
        {
            using var font = CreateFont(style.Font);
            var layoutArea = maxWidth is null
                ? new GdiSize(float.PositiveInfinity, float.PositiveInfinity)
                : new GdiSize(maxWidth.Value.Px(_dpi), float.PositiveInfinity);
            var measured = graphics.MeasureString(
                text ?? string.Empty,
                font,
                layoutArea,
                StringFormat.GenericTypographic);
            return new ReportingSize(
                Unit.FromPixels(measured.Width, _dpi),
                Unit.FromPixels(measured.Height, _dpi));
        }
        finally
        {
            if (scratch is not null)
            {
                graphics.Dispose();
                scratch.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_ownsGraphics)
        {
            _graphics?.Dispose();
        }
        _graphics = null;
        foreach (var bmp in _pages)
        {
            bmp.Dispose();
        }
        _pages.Clear();
    }

    /// <summary>Encodes the bitmap-backed page at the given index as PNG.</summary>
    public byte[] GetPagePng(int pageIndex)
    {
        using var ms = new MemoryStream();
        _pages[pageIndex].Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private void EnsureGraphics()
    {
        if (_graphics is null)
        {
            throw new InvalidOperationException(
                "No active page or graphics surface. Call BeginPage first, or supply a Graphics in the constructor.");
        }
    }

    private GdiFont CreateFont(ReportingFont font)
        => new(font.Family, (float)font.Size, font.Style.ToGdiFontStyle(), GraphicsUnit.Point);

    private GdiPen CreatePen(PenStyle pen)
    {
        var width = pen.Thickness.Px(_dpi);
        var gdiPen = new GdiPen(pen.Color.ToGdiColor(), width)
        {
            DashStyle = pen.Style.ToDashStyle(),
        };
        return gdiPen;
    }

    private static void ConfigureGraphics(GdiGraphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PageUnit = GraphicsUnit.Pixel;
    }
}
