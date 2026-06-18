using Bunit;
using FluentAssertions;
using Reporting.Designer.Blazor;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

public class ReportDesignerComponentTests : Bunit.BunitContext
{
    public ReportDesignerComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_shell_with_default_state()
    {
        var cut = Render<ReportDesigner>();
        cut.Find(".report-designer").Should().NotBeNull();
        cut.Markup.Should().Contain("Report Designer");
        // Should show the three seeded bands.
        cut.Markup.Should().Contain("Page Header");
        cut.Markup.Should().Contain("Detail");
        cut.Markup.Should().Contain("Page Footer");
    }

    [Fact]
    public void Toolbox_renders_the_basic_element_buttons()
    {
        var cut = Render<ReportDesigner>();
        // Basic element buttons carry a data-kind attribute; the system-field shortcuts share
        // the .toolbox-btn class but have no data-kind. There are 8 basic element types today
        // (Label, TextBox, Line, Rectangle, Ellipse, Picture, Barcode, QR Code).
        cut.FindAll(".toolbox-btn[data-kind]").Count.Should().Be(8);
    }

    [Fact]
    public void Adding_element_appears_in_canvas_and_marks_dirty()
    {
        var cut = Render<ReportDesigner>();
        // Click the first toolbox button (Label).
        cut.FindAll(".toolbox-btn")[0].Click();

        cut.Instance.State.History.CanUndo.Should().BeTrue();
        cut.Instance.State.IsDirty.Should().BeTrue();
        cut.Instance.State.SelectedElement.Should().NotBeNull();
        // The new element gets rendered as an HTML <div class="el …"> on the Detail band.
        cut.Markup.Should().Contain("data-element-id");
    }

    [Fact]
    public void Undo_button_disabled_until_action_performed()
    {
        var cut = Render<ReportDesigner>();
        var undo = cut.FindAll("button").FirstOrDefault(b => b.GetAttribute("title")?.StartsWith("Desfazer") ?? false);
        undo!.HasAttribute("disabled").Should().BeTrue();
        cut.FindAll(".toolbox-btn")[0].Click();
        cut.FindAll("button").First(b => b.GetAttribute("title")?.StartsWith("Desfazer") ?? false)
            .HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Undo_removes_last_added_element()
    {
        var cut = Render<ReportDesigner>();
        cut.FindAll(".toolbox-btn")[0].Click();
        cut.Instance.State.Report.FindBand(DesignerBandKind.Detail)!.Elements.Should().ContainSingle();
        // Find and click Undo.
        cut.FindAll("button").First(b => b.GetAttribute("title")?.StartsWith("Desfazer") ?? false).Click();
        cut.Instance.State.Report.FindBand(DesignerBandKind.Detail)!.Elements.Should().BeEmpty();
    }

    [Fact]
    public void Delete_button_disabled_when_no_selection()
    {
        var cut = Render<ReportDesigner>();
        var del = cut.FindAll("button").First(b => b.GetAttribute("title") == "Excluir (Del)");
        del.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Delete_button_removes_selected_element()
    {
        var cut = Render<ReportDesigner>();
        cut.FindAll(".toolbox-btn")[1].Click(); // TextBox
        cut.Instance.State.SelectedElement.Should().NotBeNull();
        cut.FindAll("button").First(b => b.GetAttribute("title") == "Excluir (Del)").Click();
        cut.Instance.State.Report.FindBand(DesignerBandKind.Detail)!.Elements.Should().BeEmpty();
    }

    [Fact]
    public void Dirty_indicator_visible_after_change()
    {
        var cut = Render<ReportDesigner>();
        cut.Markup.Should().NotContain("não salvo");
        cut.FindAll(".toolbox-btn")[0].Click();
        cut.Markup.Should().Contain("não salvo");
    }

    [Fact]
    public void Save_clears_dirty_flag_and_fires_OnSaved()
    {
        byte[]? capturedBytes = null;
        var cut = Render<ReportDesigner>(p => p
            .Add(d => d.OnSaved, b => { capturedBytes = b; }));
        cut.FindAll(".toolbox-btn")[0].Click();
        cut.Instance.State.IsDirty.Should().BeTrue();

        cut.FindAll("button").First(b => b.GetAttribute("title")?.StartsWith("Salvar") ?? false).Click();
        cut.WaitForState(() => capturedBytes is not null, TimeSpan.FromSeconds(5));

        capturedBytes.Should().NotBeNull();
        System.Text.Encoding.UTF8.GetString(capturedBytes!).Should().Contain("<Report");
        cut.Instance.State.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Status_bar_shows_element_count()
    {
        // The status bar renders the element count in a dedicated [data-sb='elements'] item
        // (key "Elementos" + the live value). Query that item's text so the assertion tracks
        // the real count rather than a fixed label format.
        var cut = Render<ReportDesigner>();
        cut.Find("[data-sb='elements']").TextContent.Should().Contain("0");
        cut.FindAll(".toolbox-btn")[0].Click();
        cut.Find("[data-sb='elements']").TextContent.Should().Contain("1");
        cut.FindAll(".toolbox-btn")[1].Click();
        cut.Find("[data-sb='elements']").TextContent.Should().Contain("2");
    }

    [Fact]
    public void Canvas_renders_both_horizontal_and_vertical_rulers()
    {
        // The ruler is a JS-driven dual-axis (H + V) canvas pair — the standard report-designer
        // layout. Here we just assert both canvases render and are wired; the tick/label drawing
        // itself is exercised in the browser by the JS engine.
        var cut = Render<ReportDesigner>();
        cut.FindAll("canvas.ruler-h-canvas").Count.Should().Be(1, "the horizontal ruler must render as a canvas");
        cut.FindAll("canvas.canvas-ruler-v").Count.Should().Be(1, "the vertical ruler must render as a canvas");
    }

    [Fact]
    public void Ruler_corner_cycles_the_display_unit()
    {
        // The corner button cycles cm → mm → pol → cm and pushes the unit to the JS ruler engine.
        var cut = Render<ReportDesigner>();
        cut.Find("button.corner").TextContent.Trim().Should().Be("cm");
        cut.Find("button.corner").Click();
        cut.Find("button.corner").TextContent.Trim().Should().Be("mm");
        cut.Find("button.corner").Click();
        cut.Find("button.corner").TextContent.Trim().Should().Be("pol");
        cut.Find("button.corner").Click();
        cut.Find("button.corner").TextContent.Trim().Should().Be("cm");
    }

    [Fact]
    public void Selection_displays_selection_ring_in_canvas()
    {
        var cut = Render<ReportDesigner>();
        cut.FindAll(".toolbox-btn")[0].Click();
        // The newly added element is auto-selected → selection-ring class appears.
        cut.Markup.Should().Contain("selection-ring");
    }

    [Fact]
    public void Property_grid_shows_selected_element_details()
    {
        var cut = Render<ReportDesigner>();
        cut.FindAll(".toolbox-btn")[1].Click(); // TextBox
        cut.Markup.Should().Contain("Identidade");
        cut.Markup.Should().Contain("Posição (mm)");
        cut.Markup.Should().Contain("Tipografia");
    }

    [Fact]
    public void Snap_toggle_can_be_disabled()
    {
        var cut = Render<ReportDesigner>();
        cut.Instance.State.SnapToGrid.Should().BeTrue();
        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.Change(false);
        cut.Instance.State.SnapToGrid.Should().BeFalse();
    }
}
