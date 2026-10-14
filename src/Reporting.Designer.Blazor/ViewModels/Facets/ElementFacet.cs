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
