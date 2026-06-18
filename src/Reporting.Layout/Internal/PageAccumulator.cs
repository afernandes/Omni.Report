using Reporting.Common;
using Reporting.Geometry;
using Reporting.Layout.Primitives;
using Reporting.Paper;

namespace Reporting.Layout.Internal;

/// <summary>
/// Mutable accumulator that holds the primitives emitted on the current page and the
/// available vertical space.
/// </summary>
internal sealed class PageAccumulator
{
    private readonly List<LayoutPrimitive> _current = [];
    private readonly List<RenderedPage> _pages = [];
    private readonly PageSetup _setup;

    public PageAccumulator(PageSetup setup)
    {
        _setup = setup;
        CurrentY = setup.Margins.Top;
    }

    public int PageNumber { get; private set; } = 1;
    public Unit CurrentY { get; private set; }
    public Unit ContentBottom { get; set; }

    public Point Origin => new(_setup.Margins.Left, CurrentY);

    public IReadOnlyList<RenderedPage> Pages => _pages;

    public bool Fits(Unit height) => CurrentY + height <= ContentBottom;

    public void Emit(IEnumerable<LayoutPrimitive> primitives, Unit consumedHeight)
    {
        _current.AddRange(primitives);
        CurrentY += consumedHeight;
    }

    public void EmitFixed(IEnumerable<LayoutPrimitive> primitives)
    {
        // Fixed-position primitives (page footer drawn at bottom): doesn't advance Y.
        _current.AddRange(primitives);
    }

    /// <summary>Closes the current page and resets to a new one.</summary>
    public void Flush()
    {
        _pages.Add(new RenderedPage(
            PageNumber,
            _setup,
            new EquatableArray<LayoutPrimitive>(_current.ToArray())));
        _current.Clear();
        PageNumber++;
        CurrentY = _setup.Margins.Top;
    }
}
