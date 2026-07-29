using Reporting.Elements;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>
/// Owns the DOMAIN MAPPING for one family of report elements: how to build the immutable
/// <see cref="ReportElement"/> from the editor state, and how to read one back into it.
///
/// <para>Both directions live together, on purpose. They used to be two <c>switch</c> blocks ~250 lines
/// apart inside <see cref="ElementViewModel"/> — one over <see cref="DesignerElementKind"/> in
/// <c>ToElement</c>, one over the element type in <c>LoadFrom</c> — so every new element kind meant editing
/// the same 1.400-line class in two distant places, and it was easy to add one half and forget the other.
/// A facet makes each family a small, self-contained, independently testable unit.</para>
///
/// <para>Facets deliberately do NOT own the shared concerns (bounds, style, conditional formats, RDL
/// actions, property expressions): those apply to every element and stay in the view-model, applied around
/// the facet call.</para>
/// </summary>
internal abstract class ElementFacet
{
    /// <summary>Designer kinds this facet builds. Most facets own one; <see cref="BarcodeFacet"/> owns two
    /// (Barcode and QrCode share <see cref="BarcodeElement"/> but differ in defaults).</summary>
    internal abstract IReadOnlyList<DesignerElementKind> Kinds { get; }

    /// <summary>True when this facet knows how to read <paramref name="element"/> back into the editor.
    /// Matched by element TYPE, mirroring the original <c>switch (element)</c>: an element whose kind fell
    /// through to the TextBox catch-all is still only read by the facet that owns its real type.</summary>
    internal abstract bool Owns(ReportElement element);

    /// <summary>Builds the immutable element from the editor state. Bounds are set by the caller-facing
    /// properties the facet reads (<see cref="ElementViewModel.Bounds"/>).</summary>
    internal abstract ReportElement Build(ElementViewModel vm);

    /// <summary>Pulls <paramref name="element"/>'s own fields into the editor state.</summary>
    internal abstract void Read(ElementViewModel vm, ReportElement element);
}

/// <summary>Resolves the facet for a designer kind (build direction) or an element (read direction).
/// Adding an element family means adding one facet and registering it here — the view-model's two mapping
/// paths stay untouched.</summary>
internal static class ElementFacetRegistry
{
    private static readonly ElementFacet[] AllFacets =
    [
        new LabelFacet(),
        new TextBoxFacet(),
        new LineFacet(),
        new RectangleFacet(),
        new EllipseFacet(),
        new ImageFacet(),
        new BarcodeFacet(),
        new ChartFacet(),
    ];

    private static readonly Dictionary<DesignerElementKind, ElementFacet> ByKind =
        AllFacets.SelectMany(f => f.Kinds, (f, k) => (Kind: k, Facet: f))
                 .ToDictionary(x => x.Kind, x => x.Facet);

    /// <summary>The facet that builds <paramref name="kind"/>, or null when the kind has no dedicated
    /// mapping (the opaque-advanced elements, which round-trip through their preserved source element).</summary>
    internal static ElementFacet? ForKind(DesignerElementKind kind) =>
        ByKind.TryGetValue(kind, out var facet) ? facet : null;

    /// <summary>The facet that reads <paramref name="element"/>, or null when no facet owns its type —
    /// in which case the editor keeps only the shared fields, exactly as the original switch did.</summary>
    internal static ElementFacet? ForElement(ReportElement element)
    {
        foreach (var facet in AllFacets)
        {
            if (facet.Owns(element))
            {
                return facet;
            }
        }
        return null;
    }
}
