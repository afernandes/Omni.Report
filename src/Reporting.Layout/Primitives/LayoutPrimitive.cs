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
}

public sealed record DrawTextPrimitive : LayoutPrimitive
{
    public required string Text { get; init; }
    public required TextStyle Style { get; init; }
}

public sealed record DrawLinePrimitive : LayoutPrimitive
{
    public required Point From { get; init; }
    public required Point To { get; init; }
    public required PenStyle Pen { get; init; }
}

public sealed record DrawRectanglePrimitive : LayoutPrimitive
{
    public PenStyle? Pen { get; init; }
    public BrushStyle? Fill { get; init; }
}

public sealed record DrawEllipsePrimitive : LayoutPrimitive
{
    public PenStyle? Pen { get; init; }
    public BrushStyle? Fill { get; init; }
}

public sealed record DrawImagePrimitive : LayoutPrimitive
{
    public required EquatableArray<byte> Data { get; init; }

    /// <summary>How the image fills <see cref="LayoutPrimitive.Bounds"/> — Stretch (distort), Fit (letterbox,
    /// the default), Fill (crop), or Native. Honoured by every backend via <c>ImageSizingMath</c>.</summary>
    public Reporting.Elements.ImageSizing Sizing { get; init; } = Reporting.Elements.ImageSizing.Fit;
}

/// <summary>A polyline or filled polygon defined by a vertex list. Charts emit these: a bar is
/// a closed 4-point box, a line series is an open polyline, a pie slice is a closed arc
/// approximated by many points. Backends draw it through <see cref="IPathBuilder"/>.</summary>
public sealed record DrawPolygonPrimitive : LayoutPrimitive
{
    /// <summary>Vertices in absolute page coordinates.</summary>
    public required EquatableArray<Point> Points { get; init; }

    /// <summary>When true the figure is closed (last point connects to the first) and fillable;
    /// when false it's an open polyline that is only stroked.</summary>
    public bool Closed { get; init; } = true;

    public PenStyle? Pen { get; init; }
    public BrushStyle? Fill { get; init; }

    /// <summary>Replays the vertices onto a path builder. Centralised so every backend
    /// (Skia/GDI via <c>DrawPath</c>, the PDF and SVG exporters) shares one
    /// move/line/close implementation.</summary>
    public void BuildPath(IPathBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Points.Count == 0)
        {
            return;
        }
        builder.MoveTo(Points[0]);
        for (int i = 1; i < Points.Count; i++)
        {
            builder.LineTo(Points[i]);
        }
        if (Closed)
        {
            builder.Close();
        }
    }
}
