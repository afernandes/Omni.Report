using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Static caption.</summary>
internal sealed class LabelFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Label];
    internal override bool Owns(ReportElement element) => element is LabelElement;

    internal override ReportElement Build(ElementViewModel vm) =>
        new LabelElement { Text = vm.Text, Bounds = vm.Bounds };

    internal override void Read(ElementViewModel vm, ReportElement element) =>
        vm.Text = ((LabelElement)element).Text;
}
