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
    private readonly int _columnCount;
    private readonly Unit _columnWidth;
    private readonly Unit _columnSpacing;
    private int _currentColumn;
    private Unit _columnTop;

    public PageAccumulator(PageSetup setup)
    {
        _setup = setup;
        // Continuous (thermal roll) paper never snakes — force a single column.
        _columnCount = setup.IsContinuous ? 1 : Math.Max(1, setup.Columns);
        _columnSpacing = _columnCount > 1 ? setup.ColumnSpacing : Unit.Zero;
        _columnWidth = _columnCount > 1
            ? (setup.ContentWidth - _columnSpacing * (_columnCount - 1)) / _columnCount
            : setup.ContentWidth;
        CurrentY = setup.Margins.Top;
        _columnTop = setup.Margins.Top;
    }

    public int PageNumber { get; private set; } = 1;
    public Unit CurrentY { get; private set; }
    public Unit ContentBottom { get; set; }

    /// <summary>Newspaper/snake column count (1 = normal single-column flow).</summary>
    public int ColumnCount => _columnCount;

    /// <summary>Usable width of a single column — the content width in multi-column mode.</summary>
    public Unit ColumnWidth => _columnWidth;

    // X is offset by the current column; Y flows within the column. Single-column → Margins.Left (unchanged).
    public Point Origin => new(_setup.Margins.Left + (_columnWidth + _columnSpacing) * _currentColumn, CurrentY);

    public IReadOnlyList<RenderedPage> Pages => _pages;

    public bool Fits(Unit height) => CurrentY + height <= ContentBottom;

    /// <summary>Vertical space left in the current column from the current Y to the content bottom.</summary>
    public Unit RemainingInColumn => ContentBottom - CurrentY;

    /// <summary>The full usable height of a fresh column (column top → content bottom) — the most a band slice
    /// can ever occupy. A band taller than this must be split across pages/columns.</summary>
    public Unit FullColumnHeight => ContentBottom - _columnTop;

    /// <summary>True when the current Y is at the top of the column (nothing emitted into it yet) — lets the
    /// band-split loop detect a single element that can't fit even in a fresh column (terminate, don't loop).</summary>
    public bool AtColumnTop => CurrentY <= _columnTop;

    /// <summary>Records where column content begins on the current physical page (after the report/page
    /// header). Snake columns reset their top to this Y on a column break, not to the page margin.</summary>
    public void MarkColumnTop() => _columnTop = CurrentY;

    /// <summary>In multi-column mode, advances to the next column on the same physical page (resetting Y to the
    /// column top) and returns true; returns false when the last column is full so the caller breaks the page.</summary>
    public bool AdvanceColumn()
    {
        if (_currentColumn >= _columnCount - 1)
        {
            return false;
        }
        _currentColumn++;
        CurrentY = _columnTop;
        return true;
    }

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
        _currentColumn = 0;
        _columnTop = _setup.Margins.Top;
    }
}
