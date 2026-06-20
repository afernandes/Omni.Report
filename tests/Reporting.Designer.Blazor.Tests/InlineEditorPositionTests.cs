using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The double-click inline editor must render INSIDE the element it edits, so it inherits the element's
/// exact on-screen box (band offset, gutters, zoom already applied to the .el). It used to be positioned
/// at the .page level with recomputed mm coordinates that drifted far out of place.
/// </summary>
public class InlineEditorPositionTests : BunitContext
{
    public InlineEditorPositionTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Double_clicking_opens_the_inline_editor_inside_that_element()
    {
        var vm = new ReportDefinitionViewModel("edit");
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        vm.AddBand(new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(20)));
        vm.Bands.Single().AddElement(new ElementViewModel(DesignerElementKind.TextBox, "t1")
        {
            Expression = "{Fields.X}",
            X = Unit.FromMm(30), Y = Unit.FromMm(5), Width = Unit.FromMm(40), Height = Unit.FromMm(8),
        });

        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));
        cut.Find(".el").DoubleClick();

        // The editor must be a DESCENDANT of the element (so it overlays it exactly) and fill it.
        var editor = cut.Find(".el .inline-editor");
        editor.GetAttribute("style").Should().Contain("inset:0", "the editor fills the element box it edits");
        cut.FindAll(".inline-editor").Count.Should().Be(1, "exactly one inline editor, on the edited element");
    }
}
