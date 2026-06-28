using FluentAssertions;
using Reporting.Designer.Blazor.ViewModels;
using Reporting.Geometry;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// Advanced state behaviours that the coverage audit flagged as risky:
/// compound undo chains, clipboard lifetime across tab switches, dirty propagation
/// from nested element property changes.
/// </summary>
public class DesignerStateAdvancedTests
{
    [Fact]
    public void Undo_chain_reverses_resize_then_move_then_property_in_LIFO_order()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        var el = new ElementViewModel(DesignerElementKind.TextBox, "el1")
        {
            X = Unit.FromMm(10), Y = Unit.FromMm(5),
            Width = Unit.FromMm(40), Height = Unit.FromMm(6),
            Expression = "{Fields.Original}",
        };
        detail.AddElement(el);

        // Apply 3 mutations through commands so each is undoable independently.
        state.History.Push(new ResizeElementCommand(el, Unit.FromMm(60), Unit.FromMm(8)));
        state.History.Push(new MoveElementCommand(el, Unit.FromMm(20), Unit.FromMm(10)));
        state.History.Push(new ChangePropertyCommand<string>("Edit expr",
            () => el.Expression, v => el.Expression = v, "{Fields.Edited}"));

        el.Width.ToMm().Should().BeApproximately(60, 0.1);
        el.X.ToMm().Should().BeApproximately(20, 0.1);
        el.Expression.Should().Be("{Fields.Edited}");

        // Undo three times, LIFO.
        state.History.Undo();  // un-property
        el.Expression.Should().Be("{Fields.Original}");
        el.X.ToMm().Should().BeApproximately(20, 0.1, "move still applied");

        state.History.Undo();  // un-move
        el.X.ToMm().Should().BeApproximately(10, 0.1);
        el.Width.ToMm().Should().BeApproximately(60, 0.1, "resize still applied");

        state.History.Undo();  // un-resize
        el.Width.ToMm().Should().BeApproximately(40, 0.1);
        el.Height.ToMm().Should().BeApproximately(6, 0.1);
    }

    [Fact]
    public void Redo_after_undo_reapplies_in_original_order()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        var el = new ElementViewModel(DesignerElementKind.Label, "lbl") { Text = "v1" };
        detail.AddElement(el);

        state.History.Push(new ChangePropertyCommand<string>("rename",
            () => el.Text, v => el.Text = v, "v2"));
        state.History.Push(new ChangePropertyCommand<string>("rename",
            () => el.Text, v => el.Text = v, "v3"));

        state.History.Undo();
        state.History.Undo();
        el.Text.Should().Be("v1");

        state.History.Redo();
        el.Text.Should().Be("v2");
        state.History.Redo();
        el.Text.Should().Be("v3");
    }

    [Fact]
    public void Dirty_flag_fires_when_nested_element_property_changes()
    {
        // Regression: changing ForeColor on a deep band/element should bubble up to State.IsDirty.
        var state = new DesignerState();
        state.IsDirty.Should().BeFalse();

        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        var el = new ElementViewModel(DesignerElementKind.Label, "x");
        detail.AddElement(el);
        state.IsDirty.Should().BeTrue("AddElement is a structural change");

        state.Save();
        state.IsDirty.Should().BeFalse();

        el.ForeColor = Color.FromHex("#C2410C");
        state.IsDirty.Should().BeTrue("changing a nested element prop must mark the tab dirty");
    }

    [Fact]
    public void Clipboard_survives_tab_switch()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        var el = new ElementViewModel(DesignerElementKind.Label, "to-copy") { Text = "Copia" };
        detail.AddElement(el);
        state.SelectedElement = el;

        // Simulate Copy (clipboard holds the whole selection — one element here).
        state.Clipboard = state.SelectedElements.Select(e => e.Clone()).ToList();
        var copied = state.Clipboard;
        copied.Should().ContainSingle();

        // Switch to a new tab.
        var newTab = state.OpenNewDocument("Outro");
        state.ActiveTab.Should().BeSameAs(newTab);
        state.SelectedElement.Should().BeNull("new tab starts with no selection");

        // Clipboard MUST survive — paste on a different document is a common pro-user flow
        // (same as Crystal Reports / SSRS report designer copy across reports).
        state.Clipboard.Should().BeSameAs(copied);
        state.Clipboard[0].Text.Should().Be("Copia");
    }

    [Fact]
    public void Clipboard_holds_a_multi_element_selection()
    {
        var state = new DesignerState();
        var detail = state.Report.FindBand(DesignerBandKind.Detail)!;
        var a = new ElementViewModel(DesignerElementKind.Label, "a") { Text = "A" };
        var b = new ElementViewModel(DesignerElementKind.Label, "b") { Text = "B" };
        detail.AddElement(a);
        detail.AddElement(b);
        state.SelectMany([a, b]);
        state.SelectedElements.Should().HaveCount(2);

        // Copy captures the WHOLE selection (the regression: clipboard used to hold only the anchor).
        state.Clipboard = state.SelectedElements.Select(e => e.Clone()).ToList();

        state.Clipboard.Should().HaveCount(2);
        state.Clipboard.Select(e => e.Text).Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public void Closing_active_tab_activates_a_neighbour_and_keeps_at_least_one_tab()
    {
        var state = new DesignerState();
        var tabA = state.ActiveTab;
        var tabB = state.OpenNewDocument("B");

        state.Tabs.Count.Should().Be(2);
        state.ActiveTab.Should().BeSameAs(tabB);

        state.CloseTab(tabB);
        state.Tabs.Count.Should().Be(1, "the only remaining tab is the original");
        state.ActiveTab.Should().BeSameAs(tabA);
    }

    [Fact]
    public void Closing_the_last_tab_is_a_noop()
    {
        var state = new DesignerState();
        var only = state.ActiveTab;
        state.CloseTab(only);
        state.Tabs.Should().ContainSingle("designer must always have one tab open");
        state.ActiveTab.Should().BeSameAs(only);
    }

    [Fact]
    public void Theme_toggle_flips_between_light_and_dark()
    {
        var state = new DesignerState();
        state.Theme.Should().Be("light");
        state.ToggleTheme();
        state.Theme.Should().Be("dark");
        state.ToggleTheme();
        state.Theme.Should().Be("light");
    }

    [Fact]
    public void Zoom_is_clamped_to_safe_range()
    {
        var state = new DesignerState();
        state.Zoom = 0.0001;
        state.Zoom.Should().BeGreaterOrEqualTo(0.25);
        state.Zoom = 999;
        state.Zoom.Should().BeLessOrEqualTo(4.0);
    }

    [Fact]
    public void CommandHistory_limit_drops_oldest_entries()
    {
        var history = new CommandHistory { Limit = 3 };
        var band = new BandViewModel(DesignerBandKind.Detail, Unit.FromMm(8));
        for (int i = 0; i < 5; i++)
        {
            history.Push(new AddElementCommand(band,
                new ElementViewModel(DesignerElementKind.Label, $"id{i}")));
        }
        // 5 pushes but limit 3 → only 3 undoable.
        int undos = 0;
        while (history.Undo()) undos++;
        undos.Should().Be(3);
    }
}
