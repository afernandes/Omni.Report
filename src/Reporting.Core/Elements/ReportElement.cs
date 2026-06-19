using Reporting.Common;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Elements;

/// <summary>Base type for any visual element on a band.</summary>
public abstract record ReportElement
{
    /// <summary>Stable identifier (used by designer and serialization).</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("n");

    /// <summary>Optional name for the element (helpful in the outline tree).</summary>
    public string? Name { get; init; }

    /// <summary>Position and size relative to the band's top-left corner.</summary>
    public Rectangle Bounds { get; init; }

    /// <summary>Static visibility flag. Combined with <see cref="VisibleExpression"/> at runtime.</summary>
    public bool Visible { get; init; } = true;

    /// <summary>Expression that, if non-empty, must evaluate to <c>true</c> for the element to render.</summary>
    public string? VisibleExpression { get; init; }

    public Style Style { get; init; } = Style.Default;

    /// <summary>Conditional format rules evaluated in order; matching rules layer onto <see cref="Style"/>.</summary>
    public EquatableArray<ConditionalFormat> ConditionalFormats { get; init; } = EquatableArray<ConditionalFormat>.Empty;

    /// <summary>Per-property expression bindings (SSRS-style): a property <b>path</b> → an expression
    /// evaluated per instance at render time, whose result overrides the property's static value (the
    /// static value is the fallback when no binding exists or the expression fails). The path may be
    /// nested: <c>"Direction"</c>, <c>"FillColor"</c>, <c>"Style.ForeColor"</c>, <c>"Style.Font.Size"</c>,
    /// <c>"Bounds.Width"</c>. Empty for the vast majority of elements. Independent of (and applied before)
    /// <see cref="ConditionalFormats"/>; both keep working. Code-first/low-level set static values
    /// directly and add bindings only when wanted.</summary>
    public EquatableDictionary<string, string> PropertyExpressions { get; init; } = EquatableDictionary<string, string>.Empty;

    // ── RDL-compatibility additions ──────────────────────────────────────────────

    /// <summary>RDL <c>&lt;Bookmark&gt;</c>: a unique string that other elements'
    /// <see cref="Action"/> with <see cref="ActionKind.BookmarkLink"/> can target. Acts as
    /// a navigation anchor inside the rendered document (PDF named destinations, HTML id).</summary>
    public string? Bookmark { get; init; }

    /// <summary>RDL <c>&lt;Label&gt;</c> (DocumentMap label): when non-null, the element
    /// contributes an entry to the report's Document Map (table-of-contents) shown by
    /// interactive viewers. Hierarchical maps are built from the band structure — siblings
    /// at the same band depth become siblings in the map.</summary>
    public string? DocumentMapLabel { get; init; }

    /// <summary>RDL <c>&lt;Action&gt;</c>: at most one action per element. Null = element
    /// is not interactive. See <see cref="ElementAction"/> for the supported flavours.</summary>
    public ElementAction? Action { get; init; }

    /// <summary>RDL <c>&lt;Visibility&gt;</c> <c>&lt;ToggleItem&gt;</c>: when non-null, the
    /// element identified by <see cref="ToggleItemId"/> (which must be a TextBox with a
    /// matching <see cref="Bookmark"/>) renders an expand/collapse chevron that toggles
    /// THIS element's visibility. Used for drill-down reports.</summary>
    public string? ToggleItemId { get; init; }

    /// <summary>When <see cref="ToggleItemId"/> is set, this controls whether the element
    /// renders open (false) or collapsed (true) initially. Mirrors RDL
    /// <c>&lt;Hidden&gt;</c> under <c>&lt;Visibility&gt;</c>.</summary>
    public bool InitiallyHidden { get; init; }
}
