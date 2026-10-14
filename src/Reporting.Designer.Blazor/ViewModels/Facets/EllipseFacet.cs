using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Filled ellipse.</summary>
internal sealed class EllipseFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Ellipse];
    internal override bool Owns(ReportElement element) => element is EllipseElement;

    internal override ReportElement Build(ElementViewModel vm) =>
        new EllipseElement { Bounds = vm.Bounds, FillColor = vm.FillColor };

    internal override void Read(ElementViewModel vm, ReportElement element) =>
        vm.FillColor = ((EllipseElement)element).FillColor;
}
