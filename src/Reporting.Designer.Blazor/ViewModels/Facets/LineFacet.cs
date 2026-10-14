using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Straight rule.</summary>
internal sealed class LineFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.Line];
    internal override bool Owns(ReportElement element) => element is LineElement;

    internal override ReportElement Build(ElementViewModel vm) =>
        new LineElement { Bounds = vm.Bounds, Direction = vm.LineDir };

    internal override void Read(ElementViewModel vm, ReportElement element) =>
        vm.LineDir = ((LineElement)element).Direction;
}
