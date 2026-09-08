using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Styling;
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
    private readonly Stack<GraphicsState> _clipStack = new();
    private GdiGraphics? _graphics;
    private bool _ownsGraphics;
    private PageSetup? _currentPage;
    private ContinuousPageBounds? _bounds;
    private readonly ContinuousPageOptions _continuousOptions = new();
    private Metafile? _recording;
    private MemoryStream? _recordingStream;

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
    public GdiRenderingContext(float dpi = 96) : this(dpi, new ContinuousPageOptions()) { }

    /// <summary>Creates a standalone bitmap context with explicit limits for continuous pages.</summary>
    public GdiRenderingContext(float dpi, ContinuousPageOptions continuousOptions)
    {
        ArgumentNullException.ThrowIfNull(continuousOptions);
        continuousOptions.Validate();
        if (!float.IsFinite(dpi) || dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi));
        _continuousOptions = continuousOptions;
        _dpi = dpi;
    }

    public IReadOnlyList<GdiBitmap> Pages => _pages;

    public void BeginPage(PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(pageSetup);
        if (_currentPage is not null) EndPage();
        _currentPage = pageSetup;
        if (_graphics is null && pageSetup.IsContinuous)
        {
            BeginContinuousPage(pageSetup);
            return;
        }
        if (_graphics is null)
        {
            int widthPx = (int)Math.Ceiling(pageSetup.PageWidth.Px(_dpi));
            int heightPx = (int)Math.Ceiling(pageSetup.PageHeight.Px(_dpi));
            var bitmap = new GdiBitmap(widthPx, heightPx);
            bitmap.SetResolution(_dpi, _dpi);
            _graphics = GdiGraphics.FromImage(bitmap);
            _graphics.Clear(System.Drawing.Color.White);
            _ownsGraphics = true;
            ConfigureGraphics(_graphics);
            _pages.Add(bitmap);
        }
    }

    private void BeginContinuousPage(PageSetup setup)
    {
        _bounds = new ContinuousPageBounds(setup, _dpi, _continuousOptions);
        using var reference = new GdiBitmap(1, 1);
        reference.SetResolution(_dpi, _dpi);
        using var graphics = GdiGraphics.FromImage(reference);
        var hdc = graphics.GetHdc();
        try
        {
            _recordingStream = new MemoryStream();
            _recording = new Metafile(_recordingStream, hdc,
                new RectangleF(0, 0, setup.PageWidth.Px(_dpi), (float)_bounds.MaxHeight),
                MetafileFrameUnit.Pixel, EmfType.EmfPlusOnly);
            _graphics = GdiGraphics.FromImage(_recording);
            _ownsGraphics = true;
            ConfigureGraphics(_graphics);
        }
        catch
        {
            if (_ownsGraphics) _graphics?.Dispose();
            _graphics = null;
            _ownsGraphics = false;
            ReleaseRecording();
            _currentPage = null;
            throw;
        }
        finally { graphics.ReleaseHdc(hdc); }
    }

    public void EndPage()
    {
        try
        {
            while (_clipStack.Count > 0) PopClip();
            if (_ownsGraphics && _graphics is not null)
            {
                _graphics.Flush();
                _graphics.Dispose();
                _graphics = null;
                _ownsGraphics = false;
            }
            if (_recording is not null)
            {
                var size = _bounds!.RasterSize();
                var bitmap = new GdiBitmap(size.Width, size.Height);
                try
                {
                    bitmap.SetResolution(_dpi, _dpi);
                    using var graphics = GdiGraphics.FromImage(bitmap);
                    ConfigureGraphics(graphics);
                    graphics.Clear(System.Drawing.Color.White);
                    // Source is the physical frame, not the metafile's automatically computed ink bounds.
                    var unit = GraphicsUnit.Pixel;
                    var frame = _recording.GetBounds(ref unit);
                    graphics.DrawImage(_recording,
                        new RectangleF(0, 0, _currentPage!.PageWidth.Px(_dpi), (float)_bounds.MaxHeight),
                        frame, unit);
                    _pages.Add(bitmap);
                }
                catch { bitmap.Dispose(); throw; }
            }
        }
        finally
        {
            if (_recording is not null && _ownsGraphics)
            {
                _graphics?.Dispose();
                _graphics = null;
                _ownsGraphics = false;
            }
            ReleaseRecording();
            _currentPage = null;
        }
    }

    private void ReleaseRecording()
    {
        _recording?.Dispose();
        _recording = null;
        _recordingStream?.Dispose();
        _recordingStream = null;
        _bounds = null;
    }

    private void Track(RectangleF rect, float stroke = 0)
    {
        if (_bounds is null) return;
        rect.Inflate(stroke / 2 + 1, stroke / 2 + 1);
        _bounds.Include(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private void TrackPath(GraphicsPath path, GdiPen? pen = null)
    {
        if (_bounds is null || path.PointCount == 0) return;
        using var outline = (GraphicsPath)path.Clone();
        // GetBounds(pen) expands by the miter limit even when the curve has no such join.
        // Widen measures the actual stroke; flattening bounds curves to 0.1 device units.
        if (pen is not null) outline.Widen(pen, null, 0.1f);
        else outline.Flatten(null, 0.1f);
        Track(outline.GetBounds());
    }

    public void DrawText(string text, ReportingRectangle bounds, TextStyle style)
    {
        EnsureGraphics();
        _bounds?.CountOperation();
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
        if (_bounds is not null && style.ForeColor.A > 0)
        {
            format.SetMeasurableCharacterRanges([new CharacterRange(0, text.Length)]);
            var regions = _graphics!.MeasureCharacterRanges(text, font, bounds.ToRectF(_dpi), format);
            try
            {
                foreach (var region in regions) Track(region.GetBounds(_graphics));
            }
            finally { foreach (var region in regions) region.Dispose(); }
        }
        _graphics!.DrawString(text, font, brush, bounds.ToRectF(_dpi), format);
    }

    public void DrawLine(ReportingPoint from, ReportingPoint to, PenStyle pen)
    {
        EnsureGraphics();
        _bounds?.CountOperation();
        ArgumentNullException.ThrowIfNull(pen);
        if (!pen.IsVisible)
        {
            return;
        }
        using var gdiPen = CreatePen(pen);
        if (pen.Color.A > 0)
        {
            Track(RectangleF.FromLTRB(Math.Min(from.X.Px(_dpi), to.X.Px(_dpi)), Math.Min(from.Y.Px(_dpi), to.Y.Px(_dpi)),
                Math.Max(from.X.Px(_dpi), to.X.Px(_dpi)), Math.Max(from.Y.Px(_dpi), to.Y.Px(_dpi))), gdiPen.Width);
        }

        _graphics!.DrawLine(gdiPen, from.ToPointF(_dpi), to.ToPointF(_dpi));
    }

    public void DrawRectangle(ReportingRectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsureGraphics();
        _bounds?.CountOperation();
        var rect = bounds.ToRectF(_dpi);
        if (fill is not null && fill.IsVisible)
        {
            using var brush = CreateFillBrush(fill, rect);
            Track(rect);
            _graphics!.FillRectangle(brush, rect);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var gdiPen = CreatePen(pen);
            if (pen.Color.A > 0) Track(rect, gdiPen.Width);
            _graphics!.DrawRectangle(gdiPen, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    public void DrawEllipse(ReportingRectangle bounds, PenStyle? pen, BrushStyle? fill)
    {
        EnsureGraphics();
        _bounds?.CountOperation();
        var rect = bounds.ToRectF(_dpi);
        if (fill is not null && fill.IsVisible)
        {
            using var brush = CreateFillBrush(fill, rect);
            Track(rect);
            _graphics!.FillEllipse(brush, rect);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var gdiPen = CreatePen(pen);
            if (pen.Color.A > 0) Track(rect, gdiPen.Width);
            _graphics!.DrawEllipse(gdiPen, rect);
        }
    }

    /// <summary>Builds the fill brush for a primitive: a solid colour, or a two-colour gradient when the brush
    /// carries one. Mirrors <c>SkiaPrimitiveRenderer.CreateFillPaint</c> point-for-point so the spooler prints
    /// what the PDF/PNG/SVG exporters render — before this, GDI silently flattened every gradient to its start
    /// colour. <see cref="BackgroundGradientType.Center"/> is a radial blend (start colour at the centre); the
    /// rest are linear along the same axes Skia uses.</summary>
    private static Brush CreateFillBrush(BrushStyle fill, RectangleF rect)
    {
        // No gradient, or a degenerate rect with no axis to blend along (GDI+ throws on a zero-length axis).
        if (!fill.HasGradient || fill.GradientEndColor is not { } end || rect.Width <= 0 || rect.Height <= 0)
        {
            return new GdiBrush(fill.Color.ToGdiColor());
        }

        if (fill.Gradient == BackgroundGradientType.Center)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(rect);
            return new PathGradientBrush(path)
            {
                CenterColor = fill.Color.ToGdiColor(),   // Skia puts colors[0] at the centre
                SurroundColors = [end.ToGdiColor()],
            };
        }

        var (from, to) = GradientAxis(fill.Gradient, rect);
        return new LinearGradientBrush(from, to, fill.Color.ToGdiColor(), end.ToGdiColor())
        {
            // GDI+ tiles past the axis by default and is known to fringe the first pixel column; flipping the
            // tile keeps that edge clean. The fill never exceeds the rect that defines the axis anyway.
            WrapMode = WrapMode.TileFlipXY,
        };
    }

    /// <summary>Start/end points of a linear gradient — the same axes as
    /// <c>SkiaPrimitiveRenderer.GradientStart/GradientEnd</c>, so both backends blend identically.</summary>
    private static (PointF From, PointF To) GradientAxis(BackgroundGradientType kind, RectangleF r) => kind switch
    {
        BackgroundGradientType.LeftRight     => (new PointF(r.Left, r.Top + (r.Height / 2f)), new PointF(r.Right, r.Top + (r.Height / 2f))),
        BackgroundGradientType.DiagonalLeft  => (new PointF(r.Left, r.Top), new PointF(r.Right, r.Bottom)),
        BackgroundGradientType.DiagonalRight => (new PointF(r.Right, r.Top), new PointF(r.Left, r.Bottom)),
        _                                    => (new PointF(r.Left + (r.Width / 2f), r.Top), new PointF(r.Left + (r.Width / 2f), r.Bottom)), // TopBottom + fallback
    };

    public void DrawImage(ReadOnlySpan<byte> imageData, ReportingRectangle bounds,
        Reporting.Elements.ImageSizing sizing = Reporting.Elements.ImageSizing.Fit)
    {
        EnsureGraphics();
        _bounds?.CountOperation();
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
        var ink = dest;
        if (p.Clip) ink.Intersect(bounds.ToRectF(_dpi));
        Track(ink);
        if (p.Clip)
        {
            var saved = _graphics!.Save();
            try
            {
                _graphics.SetClip(bounds.ToRectF(_dpi), CombineMode.Intersect);
                _graphics.DrawImage(image, dest, src, System.Drawing.GraphicsUnit.Pixel);
            }
            finally { _graphics.Restore(saved); }
        }
        else
        {
            _graphics!.DrawImage(image, dest, src, System.Drawing.GraphicsUnit.Pixel);
        }
    }

    public void DrawPath(Action<IPathBuilder> build, PenStyle? pen, BrushStyle? fill)
    {
        EnsureGraphics();
        _bounds?.CountOperation();
        ArgumentNullException.ThrowIfNull(build);
        var builder = new GdiPathBuilder(_dpi);
        using var path = builder.Path;
        build(builder);
        if (fill is not null && fill.IsVisible)
        {
            // The path's own bounding box anchors the gradient axis (Skia does the same for shape fills).
            using var brush = CreateFillBrush(fill, path.GetBounds());
            TrackPath(path);
            _graphics!.FillPath(brush, path);
        }
        if (pen is not null && pen.IsVisible)
        {
            using var gdiPen = CreatePen(pen);
            if (pen.Color.A > 0) TrackPath(path, gdiPen);
            _graphics!.DrawPath(gdiPen, path);
        }
    }

    public void PushClip(ReportingRectangle bounds, Unit cornerRadius)
    {
        EnsureGraphics();
        _bounds?.PushClip(bounds);
        _clipStack.Push(_graphics!.Save());
        var rect = bounds.ToRectF(_dpi);
        if (cornerRadius > Unit.Zero)
        {
            float r = Math.Min((float)cornerRadius.ToPixels(_dpi), Math.Min(rect.Width, rect.Height) / 2f);
            using var path = RoundedRectPath(rect, r);
            _graphics.SetClip(path, CombineMode.Intersect);
        }
        else
        {
            _graphics.SetClip(rect, CombineMode.Intersect);
        }
    }

    private static GraphicsPath RoundedRectPath(System.Drawing.RectangleF r, float radius)
    {
        float d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void PopClip()
    {
        if (_graphics is not null && _clipStack.Count > 0)
        {
            _graphics.Restore(_clipStack.Pop());
            _bounds?.PopClip();
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
        _ownsGraphics = false;
        _clipStack.Clear();
        ReleaseRecording();
        _currentPage = null;
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
        // A metafile graphics surface reports the reference device DPI, which can differ from
        // the requested bitmap DPI. Explicit pixel sizes keep recorded glyphs at the right scale.
        => _recording is not null
            ? new(font.Family, (float)font.Size * _dpi / 72f, font.Style.ToGdiFontStyle(), GraphicsUnit.Pixel)
            : new(font.Family, (float)font.Size, font.Style.ToGdiFontStyle(), GraphicsUnit.Point);

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
