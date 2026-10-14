using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Container: the only facet with children, materialised recursively as editable child VMs.</summary>
internal sealed class RectangleFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Rectangle];
    internal override bool Owns(ReportElement element) => element is RectangleElement;

    internal override ReportElement Build(ElementViewModel vm) => new RectangleElement
    {
        Bounds = vm.Bounds,
        FillColor = vm.FillColor,
        CornerRadius = Unit.FromMm(vm.CornerRadiusMm),
        Children = vm.Children.Count == 0
            ? EquatableArray<ReportElement>.Empty
            : new EquatableArray<ReportElement>(vm.Children.Select(c => c.ToElement()).ToArray()),
    };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var rect = (RectangleElement)element;
        vm.FillColor = rect.FillColor;
        vm.CornerRadiusMm = rect.CornerRadius.ToMm();
        // Materialise children into editable child VMs (recursive — a child Rectangle materialises its own
        // children in turn). The same FromElement path preserves opaque kinds, so depth is unbounded.
        foreach (var child in rect.Children)
        {
            vm.AttachChild(ElementViewModel.FromElement(child));
        }
    }
}
