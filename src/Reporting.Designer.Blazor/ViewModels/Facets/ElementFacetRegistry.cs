using Reporting.Elements;

namespace Reporting.Designer.Blazor.ViewModels;

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
