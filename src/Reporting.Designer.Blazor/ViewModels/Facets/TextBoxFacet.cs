using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Expression-bound text, optionally auto-sizing.</summary>
internal sealed class TextBoxFacet : ElementFacet
{
    internal override IReadOnlyList<DesignerElementKind> Kinds => [DesignerElementKind.TextBox];
    internal override bool Owns(ReportElement element) => element is TextBoxElement;

    internal override ReportElement Build(ElementViewModel vm) => new TextBoxElement
    {
        Expression = vm.Expression,
        Bounds = vm.Bounds,
        CanGrow = vm.CanGrow,
        CanShrink = vm.CanShrink,
        TextRuns = vm.TextRuns,
    };

    internal override void Read(ElementViewModel vm, ReportElement element)
    {
        var tb = (TextBoxElement)element;
        vm.Expression = tb.Expression;
        vm.CanGrow = tb.CanGrow;
        vm.CanShrink = tb.CanShrink;
        vm.TextRuns = tb.TextRuns; // preserve mixed-style runs across edit→save (no editor yet)
    }
}
