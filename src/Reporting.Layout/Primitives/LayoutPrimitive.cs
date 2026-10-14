using Reporting.Common;
using Reporting.Geometry;
using Reporting.Rendering;

namespace Reporting.Layout.Primitives;

/// <summary>Base type for any positioned drawing instruction emitted by the paginator.</summary>
public abstract record LayoutPrimitive
{
    /// <summary>Bounding rectangle in absolute page coordinates (after margins).</summary>
    public Rectangle Bounds { get; init; }

    /// <summary>Original element id (for traceability / hit-testing).</summary>
    public string? SourceElementId { get; init; }

    /// <summary>Navigation target propagated from the source element's <c>Action</c>: a resolved
    /// hyperlink URL, <c>"#bm-&lt;id&gt;"</c> for a bookmark link, or a drillthrough query. Exporters
    /// that support interactivity (HTML) wrap this primitive in a clickable link. Null = no action.</summary>
    public string? LinkTarget { get; init; }

    /// <summary>Anchor id this primitive defines, from the source element's <c>Bookmark</c>
    /// (prefixed <c>"bm-"</c>), so bookmark links can scroll to it. Null = not a bookmark target.</summary>
    public string? BookmarkId { get; init; }

    /// <summary>Document-map / outline label from the source element's <c>DocumentMapLabel</c>.
    /// Interactive viewers list these as a navigable table of contents linking to
    /// <see cref="BookmarkId"/>. Null = not a document-map entry.</summary>
    public string? DocMapLabel { get; init; }

    /// <summary>Clip rectangle (absolute page coords) this primitive is constrained to — set by the layout
    /// engine for the children of a container <c>Rectangle</c> so overflow is cut to the container. Null = no
    /// clip. Backends that support clipping (Skia raster/PDF/PNG/SVG, GDI) honour it; the HTML overlay inherits
    /// it via the embedded visual; text/data exporters ignore it.</summary>
    public Rectangle? ClipBounds { get; init; }

    /// <summary>Corner radius of <see cref="ClipBounds"/> when the container rectangle is rounded
    /// (<c>RectangleElement.CornerRadius</c>). Zero = a plain rectangular clip. Backends round the clip region
    /// to this radius; ignored when <see cref="ClipBounds"/> is null.</summary>
    public Unit ClipCornerRadius { get; init; } = Unit.Zero;

    /// <summary>True when this primitive belongs to a "visual" element (chart, gauge, sparkline, indicator,
    /// data bar, barcode, map) — vector graphics rather than tabular text. Table-only exporters (Word/Excel)
    /// use it to rasterise the whole visual as one image and keep its internal axis/legend text out of the
    /// data grid. Set by the layout engine; the raster/vector backends ignore it (they draw everything).</summary>
    public bool IsVisual { get; init; }
}
