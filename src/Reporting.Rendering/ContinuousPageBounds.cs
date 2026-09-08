using Reporting.Geometry;
using Reporting.Paper;

namespace Reporting.Rendering;

// Bounds are in device units. Clip envelopes are conservative for rounded corners and curves:
// they can leave whitespace but never remove visible content. The recorder's cull rect is not ink.
internal sealed class ContinuousPageBounds
{
    private readonly PageSetup _setup;
    private readonly ContinuousPageOptions _options;
    private readonly double _dpi;
    private readonly Stack<(double Left, double Top, double Right, double Bottom)> _clips = new();
    private double _bottom;
    private int _operations;

    internal ContinuousPageBounds(PageSetup setup, double dpi, ContinuousPageOptions options)
    {
        options.Validate();
        if (!double.IsFinite(dpi) || dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi));
        if (setup.Orientation != Orientation.Portrait || setup.PageWidth.Mils <= 0 ||
            setup.Margins.Top.Mils < 0 || setup.Margins.Bottom.Mils < 0)
        {
            throw new ArgumentException("Continuous paper requires portrait orientation, positive width and non-negative vertical margins.", nameof(setup));
        }

        _setup = setup;
        _options = options;
        _dpi = dpi;
        if (MaxHeight > float.MaxValue || setup.PageWidth.ToPixels(dpi) > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dpi), "Continuous recording dimensions must fit native coordinates.");
        _bottom = setup.Margins.Top.ToPixels(dpi) + 1;
        _clips.Push((0, 0, setup.PageWidth.ToPixels(dpi), double.PositiveInfinity));
        ValidateHeight(_bottom);
    }

    internal double MaxHeight => _options.MaxHeight.ToPixels(_dpi);
    internal double Height => _bottom + _setup.Margins.Bottom.ToPixels(_dpi);

    internal void CountOperation()
    {
        if (_operations >= _options.MaxOperations)
            throw new InvalidOperationException("Continuous page exceeded MaxOperations.");
        _operations++;
    }

    internal void Include(double left, double top, double right, double bottom)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom))
            throw new ArgumentException("Drawing bounds must be finite.");
        var clip = _clips.Peek();
        left = Math.Max(left, clip.Left);
        top = Math.Max(top, clip.Top);
        right = Math.Min(right, clip.Right);
        bottom = Math.Min(bottom, clip.Bottom);
        if (right <= left || bottom <= top) return;
        ValidateHeight(bottom);
        _bottom = Math.Max(_bottom, bottom);
    }

    internal void PushClip(Rectangle bounds)
    {
        CountOperation();
        var clip = _clips.Peek();
        _clips.Push((Math.Max(clip.Left, bounds.X.ToPixels(_dpi)),
            Math.Max(clip.Top, bounds.Y.ToPixels(_dpi)),
            Math.Min(clip.Right, ((double)bounds.X.Mils + bounds.Width.Mils) * _dpi / 1000),
            Math.Min(clip.Bottom, ((double)bounds.Y.Mils + bounds.Height.Mils) * _dpi / 1000)));
    }

    internal void PopClip()
    {
        if (_clips.Count > 1)
        {
            _clips.Pop();
            CountOperation();
        }
    }

    internal (int Width, int Height) RasterSize()
    {
        double width = Math.Ceiling(_setup.PageWidth.ToPixels(_dpi));
        double height = Math.Ceiling(Height);
        if (width > int.MaxValue || height > int.MaxValue || width * height > _options.MaxRasterPixels)
            throw new InvalidOperationException("Continuous page exceeded MaxRasterPixels or bitmap dimension limits.");
        return (checked((int)width), checked((int)height));
    }

    private void ValidateHeight(double bottom)
    {
        if (bottom + _setup.Margins.Bottom.ToPixels(_dpi) > MaxHeight)
            throw new InvalidOperationException("Continuous page exceeded MaxHeight, including its bottom margin.");
    }
}
