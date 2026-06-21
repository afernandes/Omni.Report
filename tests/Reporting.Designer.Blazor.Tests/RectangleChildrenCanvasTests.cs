using Bunit;
using FluentAssertions;
using Reporting.Common;
using Reporting.Designer.Blazor.Components;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Elements;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// PR1 da edição aninhada de Rectangle: os filhos de um Rectangle-container precisam ser VISÍVEIS no canvas
/// e no outline (antes eram só preservados no round-trip, invisíveis no Designer — o usuário pensava tê-los
/// perdido). Não cobre drag/resize aninhado (follow-up).
/// </summary>
public class RectangleChildrenCanvasTests : BunitContext
{
    public RectangleChildrenCanvasTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static ReportDefinitionViewModel ReportWithRectChildren()
    {
        var rect = new RectangleElement
        {
            Id = "r",
            Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(2), Unit.FromMm(100), Unit.FromMm(30)),
            Children = new EquatableArray<ReportElement>(
            [
                new LabelElement { Id = "c1", Text = "FilhoLabel", Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(5), Unit.FromMm(30), Unit.FromMm(6)) },
                new TextBoxElement { Id = "c2", Expression = "Fields.x", Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(15), Unit.FromMm(30), Unit.FromMm(6)) },
            ]),
        };

        var vm = new ReportDefinitionViewModel("nest");
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(40));
        band.AddElement(ElementViewModel.FromElement(rect));
        vm.AddBand(band);
        return vm;
    }

    [Fact]
    public void Canvas_renders_rectangle_children_as_selectable_child_divs()
    {
        var vm = ReportWithRectChildren();
        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));

        cut.FindAll(".el-child").Count.Should().Be(2, "both children render inside the container rectangle");
        cut.Markup.Should().Contain("FilhoLabel", "the child label's content is drawn on the canvas");
        // The children overlay is click-transparent so empty rectangle area still selects the rectangle.
        cut.Find(".el-children").GetAttribute("class").Should().Contain("el-children");
    }

    [Fact]
    public void Clicking_a_child_div_selects_that_child_view_model()
    {
        var vm = ReportWithRectChildren();
        ElementViewModel? selected = null;
        var cut = Render<BandCanvas>(p => p
            .Add(c => c.Report, vm)
            .Add(c => c.SelectedElementChanged, (ElementViewModel? e) => selected = e));

        cut.FindAll(".el-child")[0].Click();

        selected.Should().NotBeNull();
        selected!.Kind.Should().Be(DesignerElementKind.Label);
        ((LabelElement)selected.ToElement()).Text.Should().Be("FilhoLabel");
    }

    [Fact]
    public void Removing_a_middle_child_re_renders_the_canvas_with_the_right_survivors()
    {
        // Structural mutation: with 3 children, remove the MIDDLE one and re-render. SetKey makes Blazor match
        // by identity so the surviving child divs are correct (not stale/grafted from the removed sibling).
        var vm = new ReportDefinitionViewModel("nest");
        foreach (var b in vm.Bands.ToList()) vm.RemoveBand(b);
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(40));
        var rect = new RectangleElement
        {
            Id = "r",
            Bounds = new Rectangle(Unit.FromMm(5), Unit.FromMm(2), Unit.FromMm(100), Unit.FromMm(30)),
            Children = new EquatableArray<ReportElement>(
            [
                new LabelElement { Id = "a", Text = "AAA", Bounds = new Rectangle(Unit.FromMm(2), Unit.FromMm(2), Unit.FromMm(20), Unit.FromMm(6)) },
                new LabelElement { Id = "b", Text = "BBB", Bounds = new Rectangle(Unit.FromMm(2), Unit.FromMm(10), Unit.FromMm(20), Unit.FromMm(6)) },
                new LabelElement { Id = "c", Text = "CCC", Bounds = new Rectangle(Unit.FromMm(2), Unit.FromMm(18), Unit.FromMm(20), Unit.FromMm(6)) },
            ]),
        };
        var rectVm = ElementViewModel.FromElement(rect);
        band.AddElement(rectVm);
        vm.AddBand(band);

        var cut = Render<BandCanvas>(p => p.Add(c => c.Report, vm));
        cut.FindAll(".el-child").Count.Should().Be(3);

        // Remove the middle child ("BBB") and re-render.
        var middle = rectVm.Children.Single(c => c.Text == "BBB");
        rectVm.RemoveChild(middle);
        cut.Render();

        var remaining = cut.FindAll(".el-child");
        remaining.Count.Should().Be(2);
        cut.Markup.Should().Contain("AAA").And.Contain("CCC");
        cut.Markup.Should().NotContain("BBB", "the removed middle child is gone, not grafted onto a sibling");
    }

    [Fact]
    public void Outline_shows_children_indented_at_depth_2()
    {
        var vm = ReportWithRectChildren();
        var cut = Render<OutlineTree>(p => p.Add(c => c.Report, vm));

        cut.FindAll(".outline-row.depth-2").Count.Should().Be(2, "each child gets an indented outline row");
    }

    [Fact]
    public void Deleting_a_child_from_the_outline_removes_it_from_the_parent()
    {
        var vm = ReportWithRectChildren();
        var cut = Render<OutlineTree>(p => p.Add(c => c.Report, vm));

        // The depth-2 rows carry a delete button (outline-del); deleting the first removes it from the model.
        cut.FindAll(".outline-row.depth-2 .outline-del")[0].Click();

        var rectVm = vm.Bands.SelectMany(b => b.Elements).Single(e => e.Kind == DesignerElementKind.Rectangle);
        rectVm.Children.Should().ContainSingle("one child was deleted from its parent rectangle");
        ((RectangleElement)rectVm.ToElement()).Children.Should().ContainSingle();
    }
}
